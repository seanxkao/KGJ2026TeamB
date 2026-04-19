using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 組裝完成後展演並切場。【階段二】離開時呼叫 <see cref="AssemblySceneNavigator.LoadSceneWithSnapshotAsync"/>，將組裝狀態帶入對戰等下一場。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyCompletionTransition : MonoBehaviour
    {
        [SerializeField] AssemblyHoldAndSnapController _holdController;
        [SerializeField] Camera _camera;
        [SerializeField] string _nextSceneName = "Battle";
        [SerializeField, Min(2)] int _minimumPieceCount = 2;
        [SerializeField, Min(0.05f)] float _completionStableTime = 0.35f;
        [SerializeField, Min(0.05f)] float _pollIntervalSeconds = 0.1f;
        [SerializeField, Min(0.1f)] float _showcaseMoveDuration = 0.45f;
        [SerializeField, Min(0.1f)] float _showcaseSpinDuration = 4f;
        [SerializeField, Min(0.5f)] float _showcaseBaseDistance = 3.25f;
        [SerializeField, Min(0f)] float _showcaseBoundsDistanceFactor = 1.35f;
        [SerializeField] Vector3 _showcaseViewOffset = new Vector3(0f, -0.15f, 0f);
        [SerializeField] Vector3 _showcaseRotationEuler = new Vector3(-12f, 250f, 10f);

        readonly List<Rigidbody> _allBodiesScratch = new List<Rigidbody>(16);
        readonly List<Rigidbody> _groupBodiesScratch = new List<Rigidbody>(16);
        readonly List<RigidbodyPoseState> _poseStates = new List<RigidbodyPoseState>(16);
        readonly HashSet<Rigidbody> _groupVisited = new HashSet<Rigidbody>();
        readonly Queue<Rigidbody> _groupQueue = new Queue<Rigidbody>();

        float _completedSince = -1f;
        bool _transitionStarted;

        struct RigidbodyPoseState
        {
            public Rigidbody Rb;
            public bool WasKinematic;
            public bool WasUsingGravity;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        void Awake()
        {
            if (_holdController == null)
                _holdController = GetComponent<AssemblyHoldAndSnapController>();
            if (_camera == null)
                _camera = Camera.main;
        }

        void Start()
        {
            MonitorAssemblyCompletionAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid MonitorAssemblyCompletionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_transitionStarted)
            {
                await UniTask.Delay(
                    Mathf.Max(1, Mathf.RoundToInt(_pollIntervalSeconds * 1000f)),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    cancellationToken);

                if (_transitionStarted)
                    break;

                if (_holdController != null && _holdController.IsHolding)
                {
                    _completedSince = -1f;
                    continue;
                }

                if (!TryGetUnifiedAssemblyGroup(_groupBodiesScratch))
                {
                    _completedSince = -1f;
                    continue;
                }

                if (_completedSince < 0f)
                {
                    _completedSince = Time.unscaledTime;
                    continue;
                }

                if (Time.unscaledTime - _completedSince < _completionStableTime)
                    continue;

                _transitionStarted = true;
                await PlayShowcaseAndLoadNextSceneAsync(cancellationToken);
            }
        }

        bool TryGetUnifiedAssemblyGroup(List<Rigidbody> output)
        {
            output.Clear();

            var pieces = Object.FindObjectsByType<AssemblyPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (pieces == null || pieces.Length < _minimumPieceCount)
                return false;

            _allBodiesScratch.Clear();
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece == null || piece.gameObject.scene != gameObject.scene)
                    continue;

                var rb = piece.EnsureRuntimeRigidbody();
                if (rb != null)
                    _allBodiesScratch.Add(rb);
            }

            if (_allBodiesScratch.Count < _minimumPieceCount)
                return false;

            CollectFixedJointGroup(_allBodiesScratch[0], output);
            return output.Count == _allBodiesScratch.Count;
        }

        void CollectFixedJointGroup(Rigidbody root, List<Rigidbody> output)
        {
            output.Clear();
            _groupVisited.Clear();
            _groupQueue.Clear();

            if (root == null)
                return;

            var joints = Object.FindObjectsByType<FixedJoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _groupVisited.Add(root);
            _groupQueue.Enqueue(root);

            while (_groupQueue.Count > 0)
            {
                var body = _groupQueue.Dequeue();
                output.Add(body);

                for (var i = 0; i < joints.Length; i++)
                {
                    var joint = joints[i];
                    if (joint == null || joint.gameObject.scene != gameObject.scene)
                        continue;

                    var owner = joint.GetComponent<Rigidbody>() ?? joint.GetComponentInParent<Rigidbody>();
                    var connected = joint.connectedBody;
                    if (owner == null || connected == null)
                        continue;

                    Rigidbody neighbor = null;
                    if (owner == body && !_groupVisited.Contains(connected))
                        neighbor = connected;
                    else if (connected == body && !_groupVisited.Contains(owner))
                        neighbor = owner;

                    if (neighbor == null)
                        continue;

                    _groupVisited.Add(neighbor);
                    _groupQueue.Enqueue(neighbor);
                }
            }
        }

        async UniTask PlayShowcaseAndLoadNextSceneAsync(CancellationToken cancellationToken)
        {
            if (_groupBodiesScratch.Count == 0)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (_holdController != null)
                _holdController.enabled = false;

            var startPivotPosition = CalculateGroupVisualCenter(_groupBodiesScratch);
            var startPivotRotation = Quaternion.identity;
            CaptureAndFreezeGroup(startPivotPosition, startPivotRotation, _groupBodiesScratch);
            var loadedNextScene = false;

            try
            {
                var targetPivotPosition = GetShowcasePivotPosition(_groupBodiesScratch);
                await AnimateGroupAsync(
                    startPivotPosition,
                    startPivotRotation,
                    targetPivotPosition,
                    startPivotRotation,
                    _showcaseMoveDuration,
                    Vector3.zero,
                    cancellationToken);

                await AnimateGroupAsync(
                    targetPivotPosition,
                    startPivotRotation,
                    targetPivotPosition,
                    startPivotRotation,
                    _showcaseSpinDuration,
                    _showcaseRotationEuler,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(_nextSceneName))
                {
                    await AssemblySceneNavigator.LoadSceneWithSnapshotAsync(_nextSceneName, cancellationToken);
                    loadedNextScene = true;
                }
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                RestoreGroupDynamicStates();
                if (!loadedNextScene)
                {
                    _transitionStarted = false;
                    _completedSince = -1f;
                    if (_holdController != null)
                        _holdController.enabled = true;
                }
            }
        }

        void CaptureAndFreezeGroup(Vector3 pivotPosition, Quaternion pivotRotation, List<Rigidbody> bodies)
        {
            _poseStates.Clear();
            var invPivotRotation = Quaternion.Inverse(pivotRotation);

            for (var i = 0; i < bodies.Count; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;

                _poseStates.Add(new RigidbodyPoseState
                {
                    Rb = rb,
                    WasKinematic = rb.isKinematic,
                    WasUsingGravity = rb.useGravity,
                    LocalPosition = invPivotRotation * (rb.position - pivotPosition),
                    LocalRotation = invPivotRotation * rb.rotation,
                });

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            Physics.SyncTransforms();
        }

        void RestoreGroupDynamicStates()
        {
            for (var i = 0; i < _poseStates.Count; i++)
            {
                var state = _poseStates[i];
                if (state.Rb == null)
                    continue;

                state.Rb.linearVelocity = Vector3.zero;
                state.Rb.angularVelocity = Vector3.zero;
                state.Rb.isKinematic = state.WasKinematic;
                state.Rb.useGravity = state.WasUsingGravity;
            }

            _poseStates.Clear();
            Physics.SyncTransforms();
        }

        async UniTask AnimateGroupAsync(
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 endPosition,
            Quaternion endRotation,
            float duration,
            Vector3 extraRotationEuler,
            CancellationToken cancellationToken)
        {
            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;

            while (elapsed < safeDuration && !cancellationToken.IsCancellationRequested)
            {
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var eased = t * t * (3f - 2f * t);
                var pivotPosition = Vector3.Lerp(startPosition, endPosition, eased);
                var pivotRotation = Quaternion.Slerp(startRotation, endRotation, eased);
                if (extraRotationEuler.sqrMagnitude > 1e-6f)
                    pivotRotation = Quaternion.Euler(extraRotationEuler * eased) * pivotRotation;

                ApplyFrozenGroupPose(pivotPosition, pivotRotation);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.unscaledDeltaTime;
            }

            var finalRotation = endRotation;
            if (extraRotationEuler.sqrMagnitude > 1e-6f)
                finalRotation = Quaternion.Euler(extraRotationEuler) * finalRotation;
            ApplyFrozenGroupPose(endPosition, finalRotation);
        }

        void ApplyFrozenGroupPose(Vector3 pivotPosition, Quaternion pivotRotation)
        {
            for (var i = 0; i < _poseStates.Count; i++)
            {
                var state = _poseStates[i];
                if (state.Rb == null)
                    continue;

                state.Rb.position = pivotPosition + pivotRotation * state.LocalPosition;
                state.Rb.rotation = pivotRotation * state.LocalRotation;
            }

            Physics.SyncTransforms();
        }

        Vector3 GetShowcasePivotPosition(List<Rigidbody> bodies)
        {
            if (_camera == null)
                return CalculateGroupVisualCenter(bodies);

            var center = CalculateGroupVisualCenter(bodies);
            var radius = CalculateGroupBoundsRadius(bodies, center);
            var distance = Mathf.Max(_showcaseBaseDistance, radius * Mathf.Max(1f, _showcaseBoundsDistanceFactor));
            var cameraTransform = _camera.transform;
            return cameraTransform.position
                 + cameraTransform.forward * distance
                 + cameraTransform.TransformVector(_showcaseViewOffset);
        }

        static Vector3 CalculateGroupVisualCenter(List<Rigidbody> bodies)
        {
            var hasBounds = false;
            var bounds = default(Bounds);

            for (var i = 0; i < bodies.Count; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;

                var renderers = rb.GetComponentsInChildren<Renderer>();
                for (var r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || !renderer.enabled)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (hasBounds)
                return bounds.center;

            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < bodies.Count; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;
                sum += rb.worldCenterOfMass;
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        static float CalculateGroupBoundsRadius(List<Rigidbody> bodies, Vector3 fallbackCenter)
        {
            var hasBounds = false;
            var bounds = default(Bounds);

            for (var i = 0; i < bodies.Count; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;

                var renderers = rb.GetComponentsInChildren<Renderer>();
                for (var r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || !renderer.enabled)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (hasBounds)
                return bounds.extents.magnitude;

            var radius = 0.5f;
            for (var i = 0; i < bodies.Count; i++)
            {
                var rb = bodies[i];
                if (rb == null)
                    continue;
                radius = Mathf.Max(radius, Vector3.Distance(fallbackCenter, rb.worldCenterOfMass));
            }
            return radius;
        }
    }
}
