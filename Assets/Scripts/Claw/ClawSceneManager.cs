using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

public class ClawSceneManager : MonoBehaviour
{
    [SerializeField]
    private float duration = 30f;

    [SerializeField]
    private Claw claw;

    [SerializeField]
    private float clawMaxMoveDis = 4f;

    [SerializeField]
    private float clawHorizontalSpeed = 1f;

    [SerializeField]
    private float clawDownSpeed = 1f;

    [SerializeField]
    private float clawUpSpeed = 1f;

    private ClawState nowState = ClawState.WaitStart;

    private Vector3 clawOriginPosition;

    private float nowTime;

    private DefaultActions action;

    private Tweener moveTween;

    private float moveSpeed;

    private float moveUpDownSpeed;

    [SerializeField]
    private TextMeshProUGUI timeText;

    [SerializeField]
    private GameObject startPanel;

    [SerializeField]
    private GameObject endPanel;

    private ClawToy nowToy;

    [SerializeField]
    private GameObject toyRoot;

    private List<ClawToy> allToys = new List<ClawToy>();

    [SerializeField]
    private ModelConfig modelConfig;

    [SerializeField]
    private float clawResize = 1f;

    private List<string> catchIds = new List<string>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        clawOriginPosition = claw.transform.position;
        nowTime = 0f;
        action = new DefaultActions();
        action.Enable();
        allToys = toyRoot.GetComponentsInChildren<ClawToy>().ToList();
        var datas = modelConfig.GetAllModels();
        foreach (var toy in allToys)
        {
            toy.RegisterOnHole(OnHole);
            var modelIdx = Random.Range(0, datas.Count);
            toy.SetModel(datas[modelIdx], clawResize);
        }
        timeText.text = $"{duration:00.00}";
    }

    // Update is called once per frame
    void Update()
    {
        if (nowState == ClawState.WaitStart)
        {
            return;
        }
        nowTime += Time.deltaTime;
        if (nowTime >= duration &&
            nowState != ClawState.End)
        {
            nowTime = duration;
            nowState = ClawState.End;
            timeText.text = "00.00";
            endPanel.SetActive(true);
        }
        if (nowState == ClawState.End)
        {
            return;
        }
        timeText.text = $"{(duration - nowTime):00.00}";
        HandleState(nowState);
    }

    private void FixedUpdate()
    {
        if (nowState != ClawState.Catch || moveSpeed > 0f)
        {
            return;
        }
        claw.GetComponent<Rigidbody>().MovePosition(claw.transform.position + moveUpDownSpeed * Time.fixedDeltaTime * Vector3.down);
    }

    private void OnDestroy()
    {
        if (claw != null)
        {
            claw.UnregisterOnCatch(OnCatch);
        }
        foreach (var toy in allToys)
        {
            if (toy == null)
            {
                continue;
            }
            toy.UnregisterOnHole(OnHole);
        }
        action?.Disable();
    }

    private void HandleState(ClawState state)
    {
        switch (state)
        {
            case ClawState.WaitRight:
                if (action.Player.Jump.WasPressedThisFrame())
                {
                    moveSpeed = 0f;
                    moveTween = DOTween.To(() => moveSpeed, s => moveSpeed = s, clawHorizontalSpeed, 0.1f);
                    LogAndSwitchState(nowState, ClawState.MovingRight);
                }
                break;
            case ClawState.MovingRight:
                claw.transform.Translate(moveSpeed * Time.deltaTime * Vector3.forward);
                var moveDis = claw.transform.position.z - clawOriginPosition.z;
                if (!action.Player.Jump.IsPressed() ||
                    moveDis >= clawMaxMoveDis)
                {
                    moveTween = DOTween.To(() => moveSpeed, s => moveSpeed = s, 0, 0.1f);
                    LogAndSwitchState(nowState, ClawState.WaitFront);
                }
                break;
            case ClawState.WaitFront:
                if (moveSpeed > 0f)
                {
                    claw.transform.Translate(moveSpeed * Time.deltaTime * Vector3.forward);
                }
                if (action.Player.Jump.WasPressedThisFrame())
                {
                    moveTween?.Kill();
                    moveSpeed = 0f;
                    moveTween = DOTween.To(() => moveSpeed, s => moveSpeed = s, clawHorizontalSpeed, 0.1f);
                    LogAndSwitchState(nowState, ClawState.MovingFront);
                }
                break;
            case ClawState.MovingFront:
                claw.transform.Translate(moveSpeed * Time.deltaTime * Vector3.left);
                moveDis = Mathf.Abs(claw.transform.position.x - clawOriginPosition.x);
                if (!action.Player.Jump.IsPressed() ||
                    moveDis >= clawMaxMoveDis)
                {
                    moveTween = DOTween.To(() => moveSpeed, s => moveSpeed = s, 0, 0.1f);
                    moveUpDownSpeed = 0;
                    LogAndSwitchState(nowState, ClawState.Catch);
                }
                break;
            case ClawState.Catch:
                if (moveSpeed > 0f)
                {
                    claw.transform.Translate(moveSpeed * Time.deltaTime * Vector3.left);
                }
                else
                {
                    if (moveUpDownSpeed == 0)
                    {
                        claw.RegisterOnCatch(OnCatch);
                        moveTween?.Kill();
                        moveUpDownSpeed = 1e-3f;
                        moveTween = DOTween.To(() => moveUpDownSpeed, s => moveUpDownSpeed = s, clawDownSpeed, 0.1f);
                    }
                }
                break;
            case ClawState.Up:
                claw.transform.Translate(moveUpDownSpeed * Time.deltaTime * Vector3.up);
                if (claw.transform.position.y >= clawOriginPosition.y)
                {
                    moveTween?.Kill();
                    moveTween = null;
                    LogAndSwitchState(nowState, ClawState.Return);
                }
                break;
            case ClawState.Return:
                if (moveTween == null)
                {
                    moveTween = claw.transform.DOMove(clawOriginPosition, 2f).SetEase(Ease.InOutQuad);
                    moveTween.onComplete += () =>
                    {
                        moveTween?.Kill();
                        moveTween = null;
                        if (nowToy != null)
                        {
                            nowToy.DisableJoint();
                            nowToy = null;
                        }
                        LogAndSwitchState(nowState, ClawState.WaitRight);
                    };
                }
                break;
            default:
                Debug.LogError($"[ClawSceneManager] Unknown state {nowState}");
                break;
        }
    }

    private void LogAndSwitchState(ClawState from, ClawState to)
    {
        Debug.Log($"[ClawSceneManager] State: {from} -> {to}");
        nowState = to;
    }

    private void OnCatch(GameObject toy, Vector3 point)
    {
        claw.UnregisterOnCatch(OnCatch);
        if (toy != null)
        {
            nowToy = toy.GetComponent<ClawToy>();
        }
        Debug.Log($"[ClawSceneManager] Catch toy: {toy}, ID: {nowToy?.Id}, point: {point}");
        if (nowToy != null)
        {
            var joint = nowToy.GetComponent<ConfigurableJoint>();
            joint.connectedBody = claw.GetComponent<Rigidbody>();
            joint.anchor = nowToy.transform.InverseTransformPoint(point);
            nowToy.GetComponent<ClawToy>().EnableJoint();
        }
        moveTween?.Kill();
        moveUpDownSpeed = 0;
        moveTween = DOTween.To(() => moveUpDownSpeed, s => moveUpDownSpeed = s, clawUpSpeed, 0.1f);
        LogAndSwitchState(nowState, ClawState.Up);
    }

    private void OnHole(ClawToy toy)
    {
        Debug.Log($"[ClawSceneManager] Get toy, ID: {toy.Id}");
        catchIds.Add(toy.Id);
        toy.UnregisterOnHole(OnHole);
        allToys.Remove(toy);
    }

    public void OnClickStart()
    {
        nowState = ClawState.WaitRight;
        startPanel.SetActive(false);
    }

    /// <summary>【流程 · 階段一】爪機結果寫入 Manager 並進組裝場景。</summary>
    public void OnClickEnd()
    {
        if (MainFlowManager.Instance != null)
        {
            MainFlowManager.Instance.SetClawToyIds(catchIds);
            MainFlowManager.Instance.StartAssembly();
        }
    }
}
