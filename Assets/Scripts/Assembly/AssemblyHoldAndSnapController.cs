using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 以滑鼠游標射線選取並拿起；持握期間 Rigidbody 先沿「相機前方、固定深度的平面」跟著游標走（Plane.Raycast），進入 snap preview 後再套用鎖定的 offset。
    /// 拖曳期只做候選偵測與 preview pose；真正的 Align + Depenetrate + 第三件檢查 + FixedJoint 合併全部在「放開左鍵」那一刻一次做完，通過才提交、否則維持游標位置讓物理接手。
    /// 位移／旋轉以 Rigidbody.position／rotation 立即寫入，並在關鍵步驟後呼叫 Physics.SyncTransforms，使 ComputePenetration／ClosestPoint 讀到本幀最新姿態。
    /// 相機：RMB 拖曳環視；WASD 平移；Space 上升、Left Shift 下降。Q/E 繞世界 Y 軸旋轉；R/F（可於 Inspector 改鍵）或滾輪繞相機 right 軸俯仰，皆以質心為樞軸。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyHoldAndSnapController : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        [Tooltip("當拖曳平面與游標射線平行、無交點時的後備：沿射線離相機此距離放置。")]
        [SerializeField] float _holdDistance = 2.25f;
        [SerializeField] float _pickRayDistance = 80f;

        [Header("磁吸（只在放開左鍵時提交）")]
        [Tooltip("廣域篩選：他件質心與手持件質心距離大於此值時整件略過（避免成對碰撞測試）。")]
        [SerializeField] float _snapSearchRadius = 1.35f;
        [Tooltip("磁吸吸入門檻：未吸附狀態下，手持件與他件碰撞體的最近表面距離 ≤ 此值才開始預覽吸附（較小）。")]
        [SerializeField] float _snapAttachMaxDistance = 0.15f;
        [Tooltip("磁吸拉離門檻：已在預覽吸附狀態下，距離 > 此值才放開吸附（較大，提供 hysteresis 讓使用者可以拉開）。")]
        [SerializeField] float _snapReleaseDistance = 0.35f;
        [Tooltip("吸附後兩件之間的表面間距；為了『放開後直接接觸』預設為 0。")]
        [SerializeField] float _snapSurfacePadding = 0f;
        [Tooltip("與其他非目標件的穿透深度超過此值，視為卡進第三件而放棄磁吸。過小會永遠驗證不過。")]
        [SerializeField] float _snapThirdBodyPenetrationSlop = 0.03f;
        [Tooltip("磁吸參數的尺寸基準；手持件實際尺寸大於此值時，搜尋距離／吸附門檻／第三體 slop 會等比放大。")]
        [SerializeField] float _snapReferenceSize = 1f;
        [Tooltip("大型模型放大磁吸參數的上限倍率，避免搜尋範圍無限制擴大。")]
        [SerializeField] float _snapMaxScale = 6f;
        [Tooltip("整體放寬吸附觸發距離的倍率；大於 1 會讓預覽與提交更容易進入。")]
        [SerializeField] float _snapTriggerDistanceMultiplier = 1.35f;
        [Tooltip("已出現 preview 後，維持候選時額外放寬的倍率，避免圈圈因微小抖動頻繁消失。")]
        [SerializeField] float _snapPreviewStickinessMultiplier = 1.2f;

        [Tooltip("拖曳時手持件的 Bounds.min.y 不得低於此值（世界座標）。用來避免穿過地面。")]
        [SerializeField] float _floorY = 0f;
        [Tooltip("拖曳時以 Rigidbody.SweepTest 限制單幀位移，避免在高速拖拉下穿過其他 Collider。")]
        [SerializeField] bool _useSweepTest = true;
        [Tooltip("SweepTest 命中時保留的安全間距。")]
        [SerializeField] float _sweepSkin = 0.01f;
        [SerializeField] int _snapAlignIterations = 16;
        [SerializeField] int _depenetrationIterations = 12;
        [Tooltip("對目前場景內所有非 kinematic Rigidbody 施加的最大線速度，避免組裝或初始化時被彈飛。")]
        [SerializeField] float _connectedGroupMaxLinearSpeed = 3.5f;
        [Tooltip("對目前場景內所有非 kinematic Rigidbody 施加的最大角速度。")]
        [SerializeField] float _connectedGroupMaxAngularSpeed = 16f;

        [Header("相機與旋轉")]
        [SerializeField] float _orbitSensitivity = 3.5f;
        [SerializeField] Vector3 _orbitPivot = new Vector3(0f, 0.35f, 0f);
        [SerializeField] float _cameraMoveSpeed = 5f;
        [SerializeField] float _rotateSpeedDegrees = 110f;
        [Tooltip("持握時繞相機 right 軸俯仰（原滾輪）：無滾輪時按住此鍵。")]
        [SerializeField] KeyCode _pitchPositiveKey = KeyCode.R;
        [Tooltip("持握時俯仰反向。")]
        [SerializeField] KeyCode _pitchNegativeKey = KeyCode.F;
        [SerializeField] LayerMask _raycastMask = ~0;

        [Header("光暈（水平圓環）")]
        [SerializeField] int _haloSegments = 56;
        [SerializeField] float _haloLineWidth = 0.042f;
        [SerializeField] float _haloRadiusScale = 1.2f;
        [SerializeField] float _haloHeightAboveBoundsMin = 0.03f;
        [SerializeField] Color _haloColor = new Color(0.25f, 0.95f, 1f, 1f);
        [SerializeField] Color _snapPartnerHaloColor = new Color(1f, 0.55f, 0.15f, 1f);

        const int PickRaycastBufferSize = 32;
        readonly RaycastHit[] _raycastHitsBuffer = new RaycastHit[PickRaycastBufferSize];
        readonly RaycastHit[] _raycastHitsSorted = new RaycastHit[PickRaycastBufferSize];

        readonly List<Collider> _heldColliders = new List<Collider>(8);
        readonly List<Rigidbody> _heldGroupBodies = new List<Rigidbody>(8);
        readonly List<HeldBodySavedState> _heldGroupSavedStates = new List<HeldBodySavedState>(8);
        readonly List<HeldGroupOffset> _heldGroupOffsets = new List<HeldGroupOffset>(8);
        readonly List<RigidbodyDynamicState> _releaseFrozenTargetStates = new List<RigidbodyDynamicState>(8);

        struct HeldBodySavedState
        {
            public Rigidbody Rb;
            public bool WasKinematic;
            public bool WasUsingGravity;
        }

        struct HeldGroupOffset
        {
            public Rigidbody Rb;
            public Vector3 LocalPos;
            public Quaternion LocalRot;
        }

        struct RigidbodyDynamicState
        {
            public Rigidbody Rb;
            public bool WasKinematic;
            public bool WasUsingGravity;
            public Vector3 LinearVelocity;
            public Vector3 AngularVelocity;
        }
        readonly List<Collider> _otherCollidersScratch = new List<Collider>(8);
        readonly List<AssemblyPiece> _pieceBuffer = new List<AssemblyPiece>(32);
        readonly List<Rigidbody> _candidateGroupBodiesScratch = new List<Rigidbody>(16);
        readonly HashSet<Rigidbody> _evaluatedCandidateBodies = new HashSet<Rigidbody>();
        readonly HashSet<Rigidbody> _ignoredTargetGroupBodies = new HashSet<Rigidbody>();
        Rigidbody _heldBody;

        // 拖曳期偵測到的磁吸候選；進入 snap 後會鎖住 target 與 offset，避免每幀重算 preview pose 造成抖動。
        Rigidbody _snapCandidateBody;
        Collider _snapCandidateTargetCol;
        Collider _snapCandidateHeldCol;

        GameObject _heldHaloRoot;
        LineRenderer _heldHaloLine;

        GameObject _snapPartnerHaloRoot;
        LineRenderer _snapPartnerHaloLine;
        Vector3 _snapPreviewOffset;
        bool _snapPreviewValid;
        bool _snapActive;
        Vector3 _cursorLogicalPos;
        bool _hasCursorLogical;
        bool _hasReleaseFrozenTarget;
        /// <summary>拾取時快取：相機到拾取點沿相機 forward 的距離。拖曳平面每幀用相機當下的 forward + 此深度重建，orbit 後游標位置仍然精準。</summary>
        float _holdCameraDepth;

        /// <summary>目前是否有物件被拿起（含按住左鍵維持持握）。</summary>
        public bool IsHolding => _heldBody != null;

        void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            ApplyGlobalClampNow();
        }

        void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // 在 LateUpdate 偵測左鍵：確保與本幀物理／Transform 同步；避免與 FixedUpdate 順序造成射線漏擊。
            if (_heldBody == null && Input.GetMouseButtonDown(0))
                TryBeginHoldAtScreenPoint(Input.mousePosition);

            ApplyCameraMove();
            if (Input.GetMouseButton(1))
                OrbitCameraAroundPivot();

            if (_heldBody == null) return;

            if (Input.GetMouseButton(0))
            {
                MoveHeldAlongCursorRay();
                ApplyRotationAroundCenterOfMass();
                // orbit 可能重新擺放了 _heldBody.position；把游標邏輯位置同步過去，下一幀 sweep from 才正確。
                _cursorLogicalPos = _heldBody.position;
                SyncGroupToHeld();
                Physics.SyncTransforms();
                UpdateDragSnapPreview();
                if (_snapActive && _snapPreviewValid)
                {
                    // preview 進入 snap 後鎖住 offset，只在 release threshold 之外才解除，避免 cursor/snap 姿態跨幀來回切換。
                    _heldBody.position += _snapPreviewOffset;
                    SyncGroupToHeld();
                    Physics.SyncTransforms();
                }
                UpdateSelectionHalo();
                UpdateSnapPartnerHalo();
            }
            else
            {
                ReleaseHold();
            }
        }

        void OnDestroy()
        {
            DestroySelectionHalo();
            DestroySnapPartnerHalo();
        }

        void FixedUpdate()
        {
            ClampSceneRigidbodies();
        }

        void ApplyGlobalClampNow()
        {
            Physics.SyncTransforms();
            ClampSceneRigidbodies();
        }

        void ApplyCameraMove()
        {
            var dt = Time.deltaTime;
            var move = Vector3.zero;

            var forward = _camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f)
                forward.Normalize();
            else
                forward = Vector3.forward;

            var right = _camera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude > 1e-6f)
                right.Normalize();
            else
                right = Vector3.right;

            if (Input.GetKey(KeyCode.W)) move += forward;
            if (Input.GetKey(KeyCode.S)) move -= forward;
            if (Input.GetKey(KeyCode.D)) move += right;
            if (Input.GetKey(KeyCode.A)) move -= right;
            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.LeftShift)) move -= Vector3.up;

            if (move.sqrMagnitude < 1e-8f) return;

            _camera.transform.position += move.normalized * (_cameraMoveSpeed * dt);
        }

        void OrbitCameraAroundPivot()
        {
            var mx = Input.GetAxis("Mouse X") * _orbitSensitivity;
            var my = Input.GetAxis("Mouse Y") * _orbitSensitivity;
            _camera.transform.RotateAround(_orbitPivot, Vector3.up, mx);
            _camera.transform.RotateAround(_orbitPivot, _camera.transform.right, -my);
        }

        /// <summary>
        /// 自指定螢幕像素座標投射射線並嘗試拿起（與左鍵邏輯相同）。可用於觸控或自動化測試。
        /// </summary>
        /// <returns>是否成功開始持握。</returns>
        public bool TryBeginHoldAtScreenPoint(Vector2 screenPoint)
        {
            if (_heldBody != null) return false;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            Physics.SyncTransforms();

            var ray = _camera.ScreenPointToRay(screenPoint);
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHitsBuffer,
                _pickRayDistance,
                _raycastMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0) return false;

            for (var i = 0; i < hitCount; i++)
                _raycastHitsSorted[i] = _raycastHitsBuffer[i];
            System.Array.Sort(_raycastHitsSorted, 0, hitCount, RaycastHitDistanceComparer.Instance);

            RaycastHit? chosen = null;
            for (var i = 0; i < hitCount; i++)
            {
                var h = _raycastHitsSorted[i];
                if (!h.collider.enabled) continue;

                var piece = h.collider.GetComponentInParent<AssemblyPiece>();
                if (piece == null) continue;

                // attachedRigidbody 在部分階層（Collider 與 Rigidbody 不同節點）可能為 null，改向上尋找。
                var rb = h.collider.attachedRigidbody;
                if (rb == null)
                    rb = h.collider.GetComponentInParent<Rigidbody>();
                if (rb == null) continue;

                chosen = h;
                break;
            }

            if (chosen == null) return false;

            var hit = chosen.Value;
            var body = hit.collider.attachedRigidbody;
            if (body == null)
                body = hit.collider.GetComponentInParent<Rigidbody>();
            if (body == null) return false;

            _heldBody = body;
            BuildHeldGroup(body);
            CacheHeldColliders();
            BeginHoldGroupKinematic();
            CaptureGroupOffsets();

            _hasCursorLogical = false;
            ClearSnapCandidate();

            // 以「相機到拾取點」沿相機 forward 的投影為拖曳深度。之後拖曳平面每幀用相機當下的 forward + 此深度重建，
            // 這樣 orbit 相機後物件仍維持在使用者看到的同一景深，不會出現「游標動了、物件卻漂走」的錯位。
            var camT = _camera.transform;
            _holdCameraDepth = Mathf.Max(0.1f, Vector3.Dot(hit.point - camT.position, camT.forward));

            CreateSelectionHalo();
            UpdateSelectionHalo();
            return true;
        }

        void CreateSelectionHalo()
        {
            DestroySelectionHalo();

            var segments = Mathf.Clamp(_haloSegments, 12, 256);
            _heldHaloRoot = new GameObject("HeldSelectionHalo");
            _heldHaloLine = _heldHaloRoot.AddComponent<LineRenderer>();
            _heldHaloLine.loop = true;
            _heldHaloLine.positionCount = segments;
            _heldHaloLine.useWorldSpace = true;
            _heldHaloLine.widthMultiplier = 1f;
            _heldHaloLine.widthCurve = AnimationCurve.Constant(0f, 1f, _haloLineWidth);
            _heldHaloLine.numCornerVertices = 3;
            _heldHaloLine.numCapVertices = 3;
            _heldHaloLine.colorGradient = BuildHaloGradient(_haloColor);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = Color.white;
                _heldHaloLine.material = mat;
            }

            _heldHaloLine.shadowCastingMode = ShadowCastingMode.Off;
            _heldHaloLine.receiveShadows = false;
            _heldHaloLine.lightProbeUsage = LightProbeUsage.Off;
            _heldHaloLine.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        Gradient BuildHaloGradient(Color baseColor)
        {
            var g = new Gradient();
            var hi = Color.Lerp(baseColor, Color.white, 0.4f);
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(baseColor, 0f),
                    new GradientColorKey(hi, 0.5f),
                    new GradientColorKey(baseColor, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void UpdateHaloRingOnBody(LineRenderer line, Rigidbody body) =>
            UpdateHaloRingOnBody(line, body, Vector3.zero);

        void UpdateHaloRingOnBody(LineRenderer line, Rigidbody body, Vector3 worldOffset)
        {
            if (line == null || body == null) return;

            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;

            var b = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            var radius = Mathf.Max(0.1f, Mathf.Max(b.extents.x, b.extents.z) * _haloRadiusScale);
            var y = b.min.y + _haloHeightAboveBoundsMin;
            var center = new Vector3(b.center.x, y, b.center.z) + worldOffset;

            var n = line.positionCount;
            for (var i = 0; i < n; i++)
            {
                var ang = i / (float)n * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                line.SetPosition(i, center + offset);
            }
        }

        void UpdateSelectionHalo() => UpdateHaloRingOnBody(_heldHaloLine, _heldBody);

        void CreateSnapPartnerHalo()
        {
            DestroySnapPartnerHalo();

            var segments = Mathf.Clamp(_haloSegments, 12, 256);
            _snapPartnerHaloRoot = new GameObject("SnapPartnerSelectionHalo");
            _snapPartnerHaloLine = _snapPartnerHaloRoot.AddComponent<LineRenderer>();
            _snapPartnerHaloLine.loop = true;
            _snapPartnerHaloLine.positionCount = segments;
            _snapPartnerHaloLine.useWorldSpace = true;
            _snapPartnerHaloLine.widthMultiplier = 1f;
            _snapPartnerHaloLine.widthCurve = AnimationCurve.Constant(0f, 1f, _haloLineWidth);
            _snapPartnerHaloLine.numCornerVertices = 3;
            _snapPartnerHaloLine.numCapVertices = 3;
            _snapPartnerHaloLine.colorGradient = BuildHaloGradient(_snapPartnerHaloColor);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = Color.white;
                _snapPartnerHaloLine.material = mat;
            }

            _snapPartnerHaloLine.shadowCastingMode = ShadowCastingMode.Off;
            _snapPartnerHaloLine.receiveShadows = false;
            _snapPartnerHaloLine.lightProbeUsage = LightProbeUsage.Off;
            _snapPartnerHaloLine.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        void UpdateSnapPartnerHalo()
        {
            if (_snapCandidateBody == null)
            {
                DestroySnapPartnerHalo();
                return;
            }

            if (_snapPartnerHaloLine == null)
                CreateSnapPartnerHalo();

            UpdateHaloRingOnBody(_snapPartnerHaloLine, _snapCandidateBody);
        }

        void DestroySnapPartnerHalo()
        {
            if (_snapPartnerHaloRoot == null) return;
            Destroy(_snapPartnerHaloRoot);
            _snapPartnerHaloRoot = null;
            _snapPartnerHaloLine = null;
        }

        void DestroySelectionHalo()
        {
            if (_heldHaloRoot == null) return;
            Destroy(_heldHaloRoot);
            _heldHaloRoot = null;
            _heldHaloLine = null;
        }

        void CacheHeldColliders()
        {
            _heldColliders.Clear();
            for (var i = 0; i < _heldGroupBodies.Count; i++)
            {
                var rb = _heldGroupBodies[i];
                if (rb == null) continue;
                var buf = new List<Collider>(8);
                rb.GetComponentsInChildren(true, buf);
                _heldColliders.AddRange(buf);
            }
        }

        void BuildHeldGroup(Rigidbody root)
        {
            CollectFixedJointGroup(root, _heldGroupBodies);
        }

        void CollectFixedJointGroup(Rigidbody root, List<Rigidbody> output)
        {
            output.Clear();
            if (root == null) return;
            var allJoints = Object.FindObjectsByType<FixedJoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var visited = new HashSet<Rigidbody> { root };
            var queue = new Queue<Rigidbody>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var b = queue.Dequeue();
                output.Add(b);
                for (var i = 0; i < allJoints.Length; i++)
                {
                    var j = allJoints[i];
                    if (j == null) continue;
                    var owner = j.GetComponent<Rigidbody>();
                    var connected = j.connectedBody;
                    if (owner == null || connected == null) continue;
                    Rigidbody neighbor = null;
                    if (owner == b && !visited.Contains(connected)) neighbor = connected;
                    else if (connected == b && !visited.Contains(owner)) neighbor = owner;
                    if (neighbor != null)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        void BeginHoldGroupKinematic()
        {
            _heldGroupSavedStates.Clear();
            for (var i = 0; i < _heldGroupBodies.Count; i++)
            {
                var rb = _heldGroupBodies[i];
                if (rb == null) continue;
                _heldGroupSavedStates.Add(new HeldBodySavedState
                {
                    Rb = rb,
                    WasKinematic = rb.isKinematic,
                    WasUsingGravity = rb.useGravity,
                });
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        void RestoreHoldGroupDynamics()
        {
            for (var i = 0; i < _heldGroupSavedStates.Count; i++)
            {
                var s = _heldGroupSavedStates[i];
                if (s.Rb == null) continue;
                s.Rb.isKinematic = s.WasKinematic;
                s.Rb.useGravity = s.WasUsingGravity;
            }
            _heldGroupSavedStates.Clear();
        }

        void FreezeBodyForRelease(Rigidbody rb)
        {
            RestoreFrozenReleaseTarget();
            _releaseFrozenTargetStates.Clear();
            if (rb == null) return;

            var bodies = new List<Rigidbody>(8);
            CollectFixedJointGroup(rb, bodies);
            for (var i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body == null || IsInHeldGroup(body)) continue;

                _releaseFrozenTargetStates.Add(new RigidbodyDynamicState
                {
                    Rb = body,
                    WasKinematic = body.isKinematic,
                    WasUsingGravity = body.useGravity,
                    LinearVelocity = body.linearVelocity,
                    AngularVelocity = body.angularVelocity,
                });

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }

            if (_releaseFrozenTargetStates.Count == 0)
                return;

            _hasReleaseFrozenTarget = true;
            Physics.SyncTransforms();
        }

        void RestoreFrozenReleaseTarget(bool restoreVelocities = true)
        {
            if (!_hasReleaseFrozenTarget)
            {
                _releaseFrozenTargetStates.Clear();
                return;
            }

            _hasReleaseFrozenTarget = false;
            for (var i = 0; i < _releaseFrozenTargetStates.Count; i++)
            {
                var s = _releaseFrozenTargetStates[i];
                if (s.Rb == null) continue;
                s.Rb.isKinematic = s.WasKinematic;
                s.Rb.useGravity = s.WasUsingGravity;
                s.Rb.linearVelocity = restoreVelocities ? s.LinearVelocity : Vector3.zero;
                s.Rb.angularVelocity = restoreVelocities ? s.AngularVelocity : Vector3.zero;
            }
            _releaseFrozenTargetStates.Clear();
            Physics.SyncTransforms();
        }

        void ClampSceneRigidbodies()
        {
            var maxLinear = Mathf.Max(0f, _connectedGroupMaxLinearSpeed);
            var maxAngular = Mathf.Max(0f, _connectedGroupMaxAngularSpeed);
            if (maxLinear <= 0f && maxAngular <= 0f) return;

            var bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var scene = gameObject.scene;
            for (var i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                if (rb == null || rb.isKinematic) continue;
                if (scene.IsValid() && rb.gameObject.scene != scene) continue;

                if (maxLinear > 0f)
                {
                    var v = rb.linearVelocity;
                    var speed = v.magnitude;
                    if (speed > maxLinear && speed > 1e-6f)
                        rb.linearVelocity = v * (maxLinear / speed);
                }

                if (maxAngular > 0f)
                {
                    var w = rb.angularVelocity;
                    var angSpeed = w.magnitude;
                    if (angSpeed > maxAngular && angSpeed > 1e-6f)
                        rb.angularVelocity = w * (maxAngular / angSpeed);
                }
            }
        }

        void CaptureGroupOffsets()
        {
            _heldGroupOffsets.Clear();
            if (_heldBody == null) return;
            var invHeldRot = Quaternion.Inverse(_heldBody.rotation);
            var heldPos = _heldBody.position;
            for (var i = 0; i < _heldGroupBodies.Count; i++)
            {
                var rb = _heldGroupBodies[i];
                if (rb == null || rb == _heldBody) continue;
                _heldGroupOffsets.Add(new HeldGroupOffset
                {
                    Rb = rb,
                    LocalPos = invHeldRot * (rb.position - heldPos),
                    LocalRot = invHeldRot * rb.rotation,
                });
            }
        }

        void SyncGroupToHeld()
        {
            if (_heldGroupOffsets.Count == 0 || _heldBody == null) return;
            var heldPos = _heldBody.position;
            var heldRot = _heldBody.rotation;
            for (var i = 0; i < _heldGroupOffsets.Count; i++)
            {
                var o = _heldGroupOffsets[i];
                if (o.Rb == null) continue;
                o.Rb.position = heldPos + heldRot * o.LocalPos;
                o.Rb.rotation = heldRot * o.LocalRot;
            }
        }

        bool IsInHeldGroup(Rigidbody rb)
        {
            if (rb == null) return false;
            for (var i = 0; i < _heldGroupBodies.Count; i++)
                if (_heldGroupBodies[i] == rb) return true;
            return false;
        }

        void MoveHeldAlongCursorRay()
        {
            var camT = _camera.transform;
            var camForward = camT.forward;
            var camPos = camT.position;

            // 每幀重建拖曳平面：法線 = -camForward、通過相機前 _holdCameraDepth 公尺的錨點。
            // 好處：RMB 環視時，物件維持相機視角下的固定景深，游標仍能在螢幕上精準對位。
            var anchor = camPos + camForward * _holdCameraDepth;
            var plane = new Plane(-camForward, anchor);

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            Vector3 holdPos;
            if (plane.Raycast(ray, out var planeDist))
                holdPos = ray.GetPoint(planeDist);
            else
                holdPos = ray.origin + ray.direction * _holdDistance;

            holdPos = ClampAboveFloor(holdPos);

            // sweep 用「游標邏輯位置」當 from，不要用 _heldBody.position（上一幀結尾可能被吸附 teleport 成 snap pose，
            // 從 snap pose 出發的 SweepTest 會立刻撞到目標，把本幀 clamp 回 snap pose，造成閃爍）。
            var from = _hasCursorLogical ? _cursorLogicalPos : _heldBody.position;
            var to = SweepLimitedTarget(from, holdPos);
            _cursorLogicalPos = to;
            _hasCursorLogical = true;
            // 直接寫 .position：MovePosition 在 kinematic body 上是延遲到下一個 FixedUpdate 才生效。
            _heldBody.position = to;
        }

        Vector3 ClampAboveFloor(Vector3 targetPos)
        {
            if (_heldColliders.Count == 0) return targetPos;
            var currentPos = _heldBody.position;
            var delta = targetPos - currentPos;
            var boundsMinY = float.MaxValue;
            for (var i = 0; i < _heldColliders.Count; i++)
            {
                var c = _heldColliders[i];
                if (c == null || !c.enabled) continue;
                var by = c.bounds.min.y;
                if (by < boundsMinY) boundsMinY = by;
            }
            if (boundsMinY == float.MaxValue) return targetPos;
            var predictedMinY = boundsMinY + delta.y;
            if (predictedMinY < _floorY)
                targetPos.y += (_floorY - predictedMinY);
            return targetPos;
        }

        Vector3 SweepLimitedTarget(Vector3 from, Vector3 to)
        {
            if (!_useSweepTest) return to;
            var delta = to - from;
            var dist = delta.magnitude;
            if (dist < 1e-6f) return to;
            var dir = delta / dist;
            if (!_heldBody.SweepTest(dir, out var hit, dist + _sweepSkin, QueryTriggerInteraction.Ignore))
                return to;
            // 忽略手持件自身或其 joint 群組內的碰撞體（避免與自己卡住）。
            if (hit.collider != null && _heldColliders.Contains(hit.collider))
                return to;
            var safeDist = Mathf.Max(0f, hit.distance - _sweepSkin);
            return from + dir * safeDist;
        }

        void UpdateDragSnapPreview()
        {
            GatherOtherPieces();
            var hadSnapContext = _snapActive || _snapCandidateBody != null;

            if (TryKeepLockedSnapCandidate())
                return;

            EvaluateSnapCandidate(hadSnapContext ? GetReleaseBaseDistance() : _snapAttachMaxDistance);
        }

        float GetHeldSnapScale()
        {
            if (_heldBody == null || _heldColliders.Count == 0) return 1f;

            var hasBounds = false;
            var bounds = default(Bounds);
            for (var i = 0; i < _heldColliders.Count; i++)
            {
                var c = _heldColliders[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            if (!hasBounds) return 1f;

            var size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var reference = Mathf.Max(0.01f, _snapReferenceSize);
            return Mathf.Clamp(size / reference, 1f, Mathf.Max(1f, _snapMaxScale));
        }

        bool RecalculateSnapCandidateForRelease()
        {
            GatherOtherPieces();
            EvaluateSnapCandidate(GetReleaseBaseDistance());
            return _snapActive && _snapPreviewValid && _snapCandidateBody != null;
        }

        bool TryKeepLockedSnapCandidate()
        {
            if (!_snapActive || _snapCandidateBody == null)
                return false;

            var heldApprox = _heldBody.worldCenterOfMass;
            if (!TryMinColliderDistanceToHeldGroup(
                    _snapCandidateBody,
                    heldApprox,
                    float.PositiveInfinity,
                    out var dmin,
                    out var targetBody,
                    out var targetCol,
                    out var heldCol))
            {
                ClearSnapCandidate();
                return false;
            }

            if (dmin > GetEffectivePreviewKeepDistance())
            {
                ClearSnapCandidate();
                return false;
            }

            // 維持既有 preview offset，只更新目前最近的 collider pair，讓 release 時的正式對齊使用最新接觸面。
            _snapCandidateBody = targetBody;
            _snapCandidateTargetCol = targetCol;
            _snapCandidateHeldCol = heldCol;
            _snapPreviewValid = true;
            return true;
        }

        /// <summary>
        /// 只在尚未吸附，或既有鎖定目標已超出 release threshold 後，重新選一個新的 snap target。
        /// 一旦選中就鎖住 offset，直到拉離為止；拖曳期不再每幀重算 preview pose 或重跑第三體檢查。
        /// </summary>
        void EvaluateSnapCandidate(float threshold)
        {
            Rigidbody bestBody = null;
            Collider bestOtherCol = null;
            Collider bestHeldCol = null;
            var bestDist = float.MaxValue;
            var scale = GetHeldSnapScale();

            var broad = (_snapSearchRadius + GetEffectiveSnapDistance(threshold) + 0.85f) * scale;
            var broadSq = broad * broad;
            var heldApprox = _heldBody.worldCenterOfMass;
            var effectiveThreshold = GetEffectiveSnapDistance(threshold);
            _evaluatedCandidateBodies.Clear();

            for (var i = 0; i < _pieceBuffer.Count; i++)
            {
                var piece = _pieceBuffer[i];
                var rb = piece.GetComponent<Rigidbody>();
                if (rb == null || IsInHeldGroup(rb) || _evaluatedCandidateBodies.Contains(rb)) continue;

                if (!TryMinColliderDistanceToHeldGroup(rb, heldApprox, broadSq, out var dmin, out var candidateBody, out var oCol, out var hCol))
                    continue;

                if (dmin > effectiveThreshold)
                    continue;

                if (dmin < bestDist)
                {
                    bestDist = dmin;
                    bestBody = candidateBody;
                    bestOtherCol = oCol;
                    bestHeldCol = hCol;
                }
            }

            if (bestHeldCol == null || bestOtherCol == null || bestBody == null)
            {
                ClearSnapCandidate();
                return;
            }

            _snapCandidateBody = bestBody;
            _snapCandidateTargetCol = bestOtherCol;
            _snapCandidateHeldCol = bestHeldCol;

            var pT = bestOtherCol.ClosestPoint(bestHeldCol.bounds.center);
            var pH = bestHeldCol.ClosestPoint(pT);
            pT = bestOtherCol.ClosestPoint(pH);
            pH = bestHeldCol.ClosestPoint(pT);

            var delta = pT - pH;
            var d = delta.magnitude;
            _snapPreviewOffset = d > 1e-6f ? delta : Vector3.zero;
            _snapPreviewValid = true;
            _snapActive = true;
        }

        float GetReleaseBaseDistance() =>
            Mathf.Max(_snapReleaseDistance, _snapAttachMaxDistance * 1.15f);

        float GetEffectiveReleaseDistance() =>
            GetEffectiveSnapDistance(GetReleaseBaseDistance());

        float GetEffectivePreviewKeepDistance() =>
            GetEffectiveReleaseDistance() * Mathf.Max(1f, _snapPreviewStickinessMultiplier);

        float GetEffectiveSnapDistance(float baseDistance) =>
            baseDistance * GetHeldSnapScale() * Mathf.Max(1f, _snapTriggerDistanceMultiplier);

        void ClearSnapCandidate()
        {
            _snapCandidateBody = null;
            _snapCandidateTargetCol = null;
            _snapCandidateHeldCol = null;
            _snapPreviewOffset = Vector3.zero;
            _snapPreviewValid = false;
            _snapActive = false;
        }

        /// <summary>與他件所有啟用碰撞體與手持碰撞體的最小最近距離（近似；重疊時回 0）。</summary>
        bool TryMinColliderDistanceToHeld(Rigidbody other, out float minDist, out Collider closestOther, out Collider closestHeld)
        {
            minDist = float.MaxValue;
            closestOther = null;
            closestHeld = null;

            other.GetComponentsInChildren(true, _otherCollidersScratch);
            if (_otherCollidersScratch.Count == 0) return false;

            for (var oi = 0; oi < _otherCollidersScratch.Count; oi++)
            {
                var oCol = _otherCollidersScratch[oi];
                if (oCol == null || !oCol.enabled || oCol.isTrigger) continue;

                for (var hi = 0; hi < _heldColliders.Count; hi++)
                {
                    var hCol = _heldColliders[hi];
                    if (hCol == null || !hCol.enabled || hCol.isTrigger) continue;

                    var d = ApproxClosestDistanceBetweenColliders(hCol, oCol);
                    if (d < minDist)
                    {
                        minDist = d;
                        closestOther = oCol;
                        closestHeld = hCol;
                    }
                }
            }

            return closestOther != null && closestHeld != null;
        }

        bool TryMinColliderDistanceToHeldGroup(
            Rigidbody anyBodyInGroup,
            Vector3 heldApprox,
            float broadSq,
            out float minDist,
            out Rigidbody closestBody,
            out Collider closestOther,
            out Collider closestHeld)
        {
            minDist = float.MaxValue;
            closestBody = null;
            closestOther = null;
            closestHeld = null;

            _candidateGroupBodiesScratch.Clear();
            CollectFixedJointGroup(anyBodyInGroup, _candidateGroupBodiesScratch);
            if (_candidateGroupBodiesScratch.Count == 0) return false;

            var anyBodyInBroadRange = false;
            for (var i = 0; i < _candidateGroupBodiesScratch.Count; i++)
            {
                var body = _candidateGroupBodiesScratch[i];
                if (body == null) continue;
                _evaluatedCandidateBodies.Add(body);
                if (!anyBodyInBroadRange && (body.worldCenterOfMass - heldApprox).sqrMagnitude <= broadSq)
                    anyBodyInBroadRange = true;
            }

            if (!anyBodyInBroadRange)
                return false;

            for (var i = 0; i < _candidateGroupBodiesScratch.Count; i++)
            {
                var body = _candidateGroupBodiesScratch[i];
                if (body == null || IsInHeldGroup(body)) continue;
                if (!TryMinColliderDistanceToHeld(body, out var bodyDist, out var bodyOtherCol, out var bodyHeldCol))
                    continue;

                if (bodyDist < minDist)
                {
                    minDist = bodyDist;
                    closestBody = body;
                    closestOther = bodyOtherCol;
                    closestHeld = bodyHeldCol;
                }
            }

            return closestBody != null && closestOther != null && closestHeld != null;
        }

        /// <summary>
        /// 先用 AABB 判斷是否可能相交：若相交就用 Physics.ComputePenetration（對 primitive／convex mesh 為精確解）；
        /// 否則以 Collider.ClosestPoint 在兩面之間迭代 3 次收斂。比單次 ClosestPoint 穩定，能避免非凸 MeshCollider 退回 bounds 造成距離失真。
        /// </summary>
        static float ApproxClosestDistanceBetweenColliders(Collider a, Collider b)
        {
            if (a.bounds.Intersects(b.bounds))
            {
                if (Physics.ComputePenetration(
                        a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation,
                        out _, out _))
                {
                    return 0f;
                }
                // bounds 交集但實際未穿透：落入下方 ClosestPoint 迭代。
            }

            var p = b.ClosestPoint(a.bounds.center);
            var q = a.ClosestPoint(p);
            p = b.ClosestPoint(q);
            q = a.ClosestPoint(p);
            p = b.ClosestPoint(q);
            return Vector3.Distance(p, q);
        }

        /// <summary>
        /// release 對齊需要在同一幀立即生效；因此直接寫 Rigidbody.position，再 SyncTransforms 更新碰撞查詢。
        /// </summary>
        void AlignHeldSnapPair(Collider heldCol, Collider targetCol, Rigidbody heldRb)
        {
            var maxIter = Mathf.Clamp(_snapAlignIterations, 1, 48);
            var pad = Mathf.Max(0f, _snapSurfacePadding);

            for (var iter = 0; iter < maxIter; iter++)
            {
                Physics.SyncTransforms();

                if (Physics.ComputePenetration(
                        heldCol, heldCol.transform.position, heldCol.transform.rotation,
                        targetCol, targetCol.transform.position, targetCol.transform.rotation,
                        out var penDir, out var penDist))
                {
                    if (penDist > 1e-6f)
                        heldRb.position += penDir * (penDist + pad);
                    continue;
                }

                var pT = targetCol.ClosestPoint(heldCol.bounds.center);
                var pH = heldCol.ClosestPoint(pT);
                pT = targetCol.ClosestPoint(pH);

                var seg = pT - pH;
                var len = seg.magnitude;
                if (len <= pad + 0.0005f)
                    break;
                if (len < 1e-7f)
                    break;

                var n = seg / len;
                heldRb.position += n * (len - pad);
            }

            Physics.SyncTransforms();
        }

        bool TryDepenetratePair(Collider a, Collider b, Rigidbody rbA)
        {
            for (var i = 0; i < _depenetrationIterations; i++)
            {
                Physics.SyncTransforms();
                if (!Physics.ComputePenetration(
                        a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation,
                        out var dir, out var dist))
                    return true;

                if (dist <= 0f) return true;

                rbA.position += dir * dist;
            }

            Physics.SyncTransforms();
            return !Physics.ComputePenetration(
                a, a.transform.position, a.transform.rotation,
                b, b.transform.position, b.transform.rotation,
                out _, out _);
        }

        bool IsHeldPoseFreeAgainstOthers(Rigidbody ignoreTarget)
        {
            PopulateIgnoredTargetBodies(ignoreTarget);
            for (var i = 0; i < _pieceBuffer.Count; i++)
            {
                var piece = _pieceBuffer[i];
                var rb = piece.GetComponent<Rigidbody>();
                if (rb == null || IsInHeldGroup(rb) || _ignoredTargetGroupBodies.Contains(rb)) continue;

                piece.GetComponentsInChildren(true, _otherCollidersScratch);
                for (var hc = 0; hc < _heldColliders.Count; hc++)
                {
                    var hCol = _heldColliders[hc];
                    if (hCol == null || !hCol.enabled || hCol.isTrigger) continue;

                    for (var oc = 0; oc < _otherCollidersScratch.Count; oc++)
                    {
                        var oCol = _otherCollidersScratch[oc];
                        if (oCol == null || !oCol.enabled || oCol.isTrigger) continue;

                        if (Physics.ComputePenetration(
                                hCol, hCol.transform.position, hCol.transform.rotation,
                                oCol, oCol.transform.position, oCol.transform.rotation,
                                out _, out var dist) && dist > _snapThirdBodyPenetrationSlop * GetHeldSnapScale())
                            return false;
                    }
                }
            }

            return true;
        }

        void PopulateIgnoredTargetBodies(Rigidbody ignoreTarget)
        {
            _ignoredTargetGroupBodies.Clear();
            if (ignoreTarget == null) return;

            _candidateGroupBodiesScratch.Clear();
            CollectFixedJointGroup(ignoreTarget, _candidateGroupBodiesScratch);
            for (var i = 0; i < _candidateGroupBodiesScratch.Count; i++)
            {
                var body = _candidateGroupBodiesScratch[i];
                if (body != null)
                    _ignoredTargetGroupBodies.Add(body);
            }
        }

        void GatherOtherPieces()
        {
            _pieceBuffer.Clear();
            var found = Object.FindObjectsByType<AssemblyPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _pieceBuffer.AddRange(found);
        }

        void ApplyRotationAroundCenterOfMass()
        {
            var dt = Time.deltaTime;
            var yaw = 0f;
            if (Input.GetKey(KeyCode.E)) yaw += _rotateSpeedDegrees * dt;
            if (Input.GetKey(KeyCode.Q)) yaw -= _rotateSpeedDegrees * dt;
            if (Mathf.Abs(yaw) > 1e-3f)
            {
                var com = _heldBody.worldCenterOfMass;
                OrbitKinematicAroundWorldPoint(_heldBody, com, Vector3.up, yaw);
            }

            var pitch = 0f;
            var pitchRate = _rotateSpeedDegrees * 1.25f;
            if (Input.GetKey(_pitchPositiveKey)) pitch += pitchRate * dt;
            if (Input.GetKey(_pitchNegativeKey)) pitch -= pitchRate * dt;
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 1e-4f)
                pitch += scroll * pitchRate;

            if (Mathf.Abs(pitch) > 1e-4f)
            {
                var axis = _camera.transform.right;
                if (axis.sqrMagnitude > 1e-10f)
                {
                    var com = _heldBody.worldCenterOfMass;
                    OrbitKinematicAroundWorldPoint(_heldBody, com, axis, pitch);
                }
            }
        }

        /// <summary>
        /// Kinematic 剛體必須用 MovePosition／MoveRotation；若用 transform.RotateAround 會與 MovePosition 不同步，
        /// 再搭配 SyncTransforms 時容易造成本幀位置被拉回、看起來不跟游標。
        /// </summary>
        static void OrbitKinematicAroundWorldPoint(Rigidbody rb, Vector3 pivotWorld, Vector3 axisWorld, float angleDegrees)
        {
            if (Mathf.Abs(angleDegrees) < 1e-6f || rb == null) return;
            if (axisWorld.sqrMagnitude < 1e-12f) return;
            var axis = axisWorld.normalized;
            var delta = Quaternion.AngleAxis(angleDegrees, axis);
            var pos = rb.position;
            var rot = rb.rotation;
            var newPos = pivotWorld + delta * (pos - pivotWorld);
            var newRot = delta * rot;
            // 直接寫 .position／.rotation 以保證同步生效（同 MoveHeldAlongCursorRay 的理由）。
            rb.position = newPos;
            rb.rotation = newRot;
        }

        void ReleaseHold()
        {
            if (_heldBody == null) return;

            // 先嘗試提交 snap：成功才真的寫入 snap 姿態並裝 FixedJoint；任一檢查失敗就還原到游標位置、讓物理接手掉落。
            var committedTarget = TryCommitSnapOnRelease();

            DestroySelectionHalo();
            DestroySnapPartnerHalo();

            RestoreHoldGroupDynamics();

            if (committedTarget != null)
            {
                EnsureFixedJointConnection(_heldBody, committedTarget);
            }

            RestoreFrozenReleaseTarget(restoreVelocities: committedTarget == null);

            _heldBody = null;
            _heldColliders.Clear();
            _heldGroupBodies.Clear();
            _heldGroupOffsets.Clear();
            _hasCursorLogical = false;
            ClearSnapCandidate();
        }

        void EnsureFixedJointConnection(Rigidbody owner, Rigidbody target)
        {
            if (owner == null || target == null || owner == target) return;

            var joints = owner.GetComponents<FixedJoint>();
            for (var i = 0; i < joints.Length; i++)
            {
                var joint = joints[i];
                if (joint != null && joint.connectedBody == target)
                    return;
            }

            var newJoint = owner.gameObject.AddComponent<FixedJoint>();
            newJoint.connectedBody = target;
            newJoint.enableCollision = false;
            newJoint.breakForce = Mathf.Infinity;
            newJoint.breakTorque = Mathf.Infinity;
        }

        /// <summary>
        /// 只在放開左鍵那一刻執行的完整提交流程：對齊最近面 → 去穿透 → 確認沒卡進第三件。
        /// 任一關失敗就把手持件還原到游標位置、回傳 null；成功回傳要連接的目標剛體。
        /// </summary>
        Rigidbody TryCommitSnapOnRelease()
        {
            if (!RecalculateSnapCandidateForRelease())
                return null;

            var target = _snapCandidateBody;
            if (target == null || !_snapPreviewValid)
                return null;

            var savedPos = _heldBody.position;
            var savedRot = _heldBody.rotation;

            // release 正式提交期間先固定 target，避免 held body 對齊/放手瞬間把對方撞飛。
            FreezeBodyForRelease(target);

            // 放手當下重新套一次最新 snap offset，讓正式提交使用 release frame 的候選幾何。
            _heldBody.position = savedPos + _snapPreviewOffset;
            SyncGroupToHeld();
            Physics.SyncTransforms();

            if (!TryMinColliderDistanceToHeldGroup(
                    target,
                    _heldBody.worldCenterOfMass,
                    float.PositiveInfinity,
                    out _,
                    out var resolvedTargetBody,
                    out var targetCol,
                    out var heldCol))
            {
                _heldBody.position = savedPos;
                _heldBody.rotation = savedRot;
                SyncGroupToHeld();
                Physics.SyncTransforms();
                RestoreFrozenReleaseTarget(restoreVelocities: true);
                return null;
            }

            target = resolvedTargetBody;

            // release 前用重算後的預覽姿態找最近 collider pair，再進入正式對齊流程。
            Physics.SyncTransforms();
            AlignHeldSnapPair(heldCol, targetCol, _heldBody);
            SyncGroupToHeld();
            Physics.SyncTransforms();

            if (!TryDepenetratePair(heldCol, targetCol, _heldBody))
            {
                _heldBody.position = savedPos;
                _heldBody.rotation = savedRot;
                SyncGroupToHeld();
                Physics.SyncTransforms();
                RestoreFrozenReleaseTarget(restoreVelocities: true);
                return null;
            }

            // TryDepenetratePair 可能再次動了 _heldBody.position；把 group 同步到最新 pose 後再檢查第三件。
            SyncGroupToHeld();
            Physics.SyncTransforms();

            // 確保 _pieceBuffer 是最新的（drag 期每幀有 GatherOtherPieces，但 release 路徑保險起見重抓一次）。
            GatherOtherPieces();
            if (!IsHeldPoseFreeAgainstOthers(target))
            {
                _heldBody.position = savedPos;
                _heldBody.rotation = savedRot;
                SyncGroupToHeld();
                Physics.SyncTransforms();
                RestoreFrozenReleaseTarget(restoreVelocities: true);
                return null;
            }

            return target;
        }

        sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
