using System.Collections;
using UnityEngine;

public class QuickAssistManager : MonoBehaviour
{
    public event System.Action<QuickAssistCloseReason> OnWindowClosed;

    public static QuickAssistManager Instance { get; private set; }

    [SerializeField] private PartyStateManager _partyStateManager;

    private int _activeSessionId = 0;               // 빠른지원 Window 식별용 세션 ID
    // 대상 사망·제거 이후에도 사용할 창 개방 시점의 위치와 방향.
    private Vector3 _assistTargetPosition;
    private Vector3 _assistTargetForward;
    public bool IsWindowOpen { get; private set; }
    public Hero TriggerHero { get; private set; }   // 빠른지원 발동 Hero
    public Hero AssistHero { get; private set; }    // 빠른지원 등장 Hero
    public Enemy AssistTarget { get; private set; } // 빠른지원 등장 공격할 Target
    public SwapDirection CurrentSwapDirection { get; private set; }      // 교대 방향

    [Header("Quick Assist Window Duration")]
    [SerializeField, Min(0.1f)]
    private float _windowDuration = 1.5f;
    private Coroutine _windowCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ResolvePartyStateManager();
    }


    public bool TryOpen(Hero triggerHero, IAttacker assistTarget, SwapDirection direction)
    {
        ResolvePartyStateManager();

        if (_partyStateManager == null) return false;
        if (triggerHero == null) return false;
        if (triggerHero.Status == null || triggerHero.Status.IsDead) return false;
        if (_partyStateManager.CurrentHero == null) return false;
        if (assistTarget is not Enemy enemy) return false;
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.Status == null || enemy.Status.IsDead)
            return false;

        Hero assistHero = GetAssistHero(direction);

        if (assistHero == null || assistHero == _partyStateManager.CurrentHero)
        {
            return false;
        }

        if (assistHero.Status == null || assistHero.Status.IsDead || assistHero.SwapSystem == null)
        {
            return false;
        }


        // 앞선 QuickAssist 윈도우가 열려있었다면 닫고 교체 Replaced 기록
        if (IsWindowOpen)
        {
            CloseWindow(QuickAssistCloseReason.Replaced);
        }

        _activeSessionId++;
        TriggerHero = triggerHero;
        AssistHero = assistHero;
        AssistTarget = enemy;
        _assistTargetPosition = enemy.transform.position;
        _assistTargetForward = enemy.transform.forward;
        CurrentSwapDirection = direction;
        IsWindowOpen = true;

        StartWindowTimer();
        return true;
    }

    public bool TryGetExecutionData(out QuickAssistContext context)
    {
        context = default;

        if (!IsWindowOpen) return false;

        ResolvePartyStateManager();

        if (_partyStateManager == null ||
            _partyStateManager.CurrentHero == null)
        {
            CloseWindow(QuickAssistCloseReason.ContextInvalidated);
            return false;
        }

        if (TriggerHero == null ||
            TriggerHero.Status == null)
        {
            CloseWindow(QuickAssistCloseReason.ContextInvalidated);
            return false;
        }

        if (TriggerHero.Status.IsDead)
        {
            CloseWindow(QuickAssistCloseReason.TriggerHeroDead);
            return false;
        }

        if (AssistHero == null ||
            AssistHero.Status == null ||
            AssistHero.Status.IsDead ||
            AssistHero.SwapSystem == null)
        {
            CloseWindow(QuickAssistCloseReason.ContextInvalidated);
            return false;
        }

        Hero currentAssistHero = GetAssistHero(CurrentSwapDirection);

        if (currentAssistHero != AssistHero)
        {
            CloseWindow(QuickAssistCloseReason.ContextInvalidated);
            return false;
        }

        Vector3 targetPosition = _assistTargetPosition;
        Vector3 targetForward = _assistTargetForward;
        if (AssistTarget != null && AssistTarget.isActiveAndEnabled &&
            AssistTarget.Status != null && !AssistTarget.Status.IsDead)
        {
            targetPosition = AssistTarget.transform.position;
            targetForward = AssistTarget.transform.forward;
        }

        context = new QuickAssistContext(
            _activeSessionId,
            TriggerHero,
            AssistHero,
            AssistTarget,
            CurrentSwapDirection,
            targetPosition,
            targetForward
        );

        return true;
    }

    private void ResolvePartyStateManager()
    {
        if (_partyStateManager != null) return;

        _partyStateManager = PartyStateManager.Instance;

        if (_partyStateManager == null)
        {
            _partyStateManager = FindFirstObjectByType<PartyStateManager>();
        }
    }

    private Hero GetAssistHero(SwapDirection direction)
    {
        if (_partyStateManager == null) return null;

        return direction == SwapDirection.Next
            ? _partyStateManager.GetNextAliveHero() : _partyStateManager.GetPrevAliveHero();
    }


    #region [Quick Assist Window 영역]

    private void StartWindowTimer()
    {
        if (_windowCoroutine != null)
        {
            StopCoroutine(_windowCoroutine);
        }

        _windowCoroutine = StartCoroutine(
            CloseWindowAfterDelay()
        );
    }

    private IEnumerator CloseWindowAfterDelay()
    {
        yield return new WaitForSeconds(_windowDuration);

        _windowCoroutine = null;
        CloseWindow(QuickAssistCloseReason.Expired);
    }

    public void CloseWindow(QuickAssistCloseReason closeReason)
    {
        if (!IsWindowOpen) return;

        if (_windowCoroutine != null)
        {
            StopCoroutine(_windowCoroutine);
            _windowCoroutine = null;
        }

        IsWindowOpen = false;

        TriggerHero = null;
        AssistHero = null;
        AssistTarget = null;
        _assistTargetPosition = Vector3.zero;
        _assistTargetForward = Vector3.forward;

        OnWindowClosed?.Invoke(closeReason);
    }

    public void Complete(QuickAssistContext context)
    {
        if (!IsWindowOpen) return;

        if (context.SessionId != _activeSessionId)
        {
            return;
        }

        if (context.TriggerHero != TriggerHero ||
            context.AssistHero != AssistHero)
        {
            return;
        }

        CloseWindow(QuickAssistCloseReason.Consumed);
    }

    #endregion

}
