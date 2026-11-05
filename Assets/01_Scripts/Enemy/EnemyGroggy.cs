using System;
using System.Collections;
using UnityEngine;

public class EnemyGroggy : MonoBehaviour
{
    public event Action OnGroggyStart;
    public event Action OnGroggyEnd;

    private Enemy _enemy;
    private EnemyStatus _status;


    [Header("Groggy Time Settings")]
    // 그로기 시간 연장 기믹을 위한 변수
    private float _currentGroggyTime = 0f;
    // 그로기 타이머가 돌고있을 코루틴의 주소를 담아둘 인스턴스
    private Coroutine _groggyTimerCoroutine;
    // UI에서 사용하기 위한 실시간으로 변하는 그로기 타이머의 최대치
    private float _currentMaxGroggyDuration = 0f;

    #region [프로퍼티 영역]

    public float CurrentGroggyTime => _currentGroggyTime;
    // UI에서 "그로기 남은 시간 바"를 갱신할 때 0.0f ~ 1.0f 비율로 편하게 쓰기 위한 프로퍼티
    public float GroggyTimeRatio =>
        _currentMaxGroggyDuration > 0f ? (_currentGroggyTime / _currentMaxGroggyDuration) : 0f;

    #endregion


    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _status = GetComponent<EnemyStatus>();
    }


    #region [그로기 영역]

    // Enemy에서 호출될 그로기 관리 통합 메서드
    public void GroggyLogic(HitInfo hitInfo)
    {
        if (_status.IsGroggy)
        {
            if (hitInfo.canExtendGroggy)
            {   // 그로기 상태에서 그로기 연장 기믹이 터지면 그로기 시간 연장
                ExtendGroggyTime(hitInfo.groggyExtendTime);
            }

            return;
        }
        // 비그로기 상태에서는 그로기 누적
        ApplyDazeAmount(hitInfo.dazeAmount);
    }

    /// <summary>
    /// 그로기 수치를 받고 그로기 게이지가 가득 차면 그로기를 발동시키는 메서드
    /// </summary>
    /// <param name="amount">받을 그로기 수치</param>
    public void ApplyDazeAmount(float amount)
    {
        if (_status.IsGroggy) return; // 이미 그로기 상태면 게이지 누적 X

        _status.ModifyGroggyGauge(amount);
        Debug.Log($"[{gameObject.name}] 그로기 누적: {_status.CurrentGroggyGauge} / {_status.MaxGroggyGauge}");

        if(_status.CurrentGroggyGauge >= _status.MaxGroggyGauge)
        {
            TriggerGroggy();
        }
    }

    public void TriggerGroggy()
    {
        Debug.Log($"{gameObject.name} 그로기 발동");

        _status.SetGroggyState(true);

        // ChainAttackCount 속성들 초기화
        InitializeChainAttackCount();

        // 처음 시작 시에는 기본 지속시간이 곧 최대치
        _currentGroggyTime = _status.BaseGroggyDuration;
        _currentMaxGroggyDuration = _status.BaseGroggyDuration; // UI용 필드 기준점 동기화

        // TODO: Enemy 애니메이터에게 Groggy 트리거 전달하여 그로기 모션 재생
        // -> 하단의 이벤트 방식으로 변경
        // _animator.SetTrigger("Groggy");

        // 구독 중인 Enemy에게 이벤트 전송
        OnGroggyStart?.Invoke();

        // 타이머 코루틴 구동
        if (_groggyTimerCoroutine != null) StopCoroutine(_groggyTimerCoroutine);
        _groggyTimerCoroutine = StartCoroutine(Co_GroggyTimer());
    }

    /// <summary>
    /// 그로기 남은 시간과 전체 기준 시간에 연장량을 더함.
    /// 기존 타이머를 유지하며 UI 비율은 갱신된 두 시간을 기준으로 계산함.
    /// </summary>
    /// <param name="bonusTime">연장하고 싶은 시간(초)</param>
    public void ExtendGroggyTime(float bonusTime)
    {
        if (!_status.IsGroggy || bonusTime <= 0f) return;

        Debug.Log($"[그로기 연장] 기존 남은 시간: {_currentGroggyTime:F1}초 -> {bonusTime}초 추가");

        _currentGroggyTime += bonusTime;
        _currentMaxGroggyDuration += bonusTime;
    }

    /// <summary>
    /// 실시간으로 타이머 변수를 깎아내려가는 코루틴
    /// </summary>
    private IEnumerator Co_GroggyTimer()
    {
        // 남은 시간이 0보다 큰 동안 매 프레임 루프를 돕니다.
        while (_currentGroggyTime > 0f)
        {
            // 유니티의 직전 프레임 소요 시간(Time.deltaTime)만큼 실시간 차감
            _currentGroggyTime -= Time.deltaTime;

            // 다음 프레임까지 대기
            yield return null;
        }

        ResetGroggy();
    }

    /// <summary>
    /// 그로기 정보를 초기화하고 정상 종료 이벤트를 전달함.
    /// </summary>
    public void ResetGroggy()
    {
        ClearGroggyState(true);
    }

    /// <summary>
    /// 사망 시 정상 종료 이벤트 없이 그로기와 타이머를 정리함.
    /// </summary>
    public void CancelGroggyOnDeath()
    {
        ClearGroggyState(false);
    }

    /// <summary>
    /// 진행 중인 타이머와 그로기 정보를 정리하고 필요 시 종료를 알림.
    /// </summary>
    private void ClearGroggyState(bool notifyEnd)
    {
        if (_groggyTimerCoroutine != null)
        {
            StopCoroutine(_groggyTimerCoroutine);
            _groggyTimerCoroutine = null;
        }

        _status.ResetGroggyGauge();
        _status.SetGroggyState(false);
        _currentGroggyTime = 0f;
        _currentMaxGroggyDuration = 0f;     // UI용 필드도 초기화

        // ChainAttack 관련 속성 초기화
        ResetChainAttackCount();

        // Enemy에게 이벤트 신호 전달
        if (notifyEnd) OnGroggyEnd?.Invoke();
    }

    #endregion


    #region [Chain Attack Count 영역]

    // 그로기 시작 시 적 등급과 생존 파티 인원으로 제한한 QTE 기회 수.
    public int MaxChainAttackCount { get; private set; }
    // Enemy에게 시전된 ChainAttack 시전 횟수
    public int ConsumedChainAttackCount { get; private set; }
    // 현재 Enemy에게 시전 가능한 ChainAttack 횟수
    public int RemaingChainAttackCount =>
        Mathf.Max(0, GetPartyLimitedChainAttackCount(MaxChainAttackCount) - ConsumedChainAttackCount);
    public bool CanConsumeChainAttack =>
        _status.IsGroggy && RemaingChainAttackCount > 0;

    /// <summary>
    /// 그로기 시작 시 적 등급과 생존 인원에 맞춰 QTE 기회 및 소비 횟수를 초기화함.
    /// </summary>
    private void InitializeChainAttackCount()
    {
        MaxChainAttackCount = _enemy.Rank switch
        {
            EnemyRank.Normal => 1,
            EnemyRank.Elite => 2,
            EnemyRank.Boss => 3,
            _ => 1
        };

        MaxChainAttackCount = GetPartyLimitedChainAttackCount(MaxChainAttackCount);
        ConsumedChainAttackCount = 0;
    }

    /// <summary>
    /// 생존자가 둘 이상일 때만 생존 인원 이내의 QTE 기회 한도를 반환함.
    /// 잔여 기회 조회에도 사용하여 그로기 도중 파티원 사망을 반영함.
    /// </summary>
    private int GetPartyLimitedChainAttackCount(int maxCount)
    {
        PartyStateManager partyStateManager = PartyStateManager.Instance;
        if (partyStateManager == null) return 0;

        int aliveCount = partyStateManager.GetAlivePartyCount();
        return aliveCount > 1 ? Mathf.Min(maxCount, aliveCount) : 0;
    }

    /// <summary>
    /// ConsumedChainAttackCount 증가 시도 메서드
    /// ChainAttackManager - Selection Phase에서 호출
    /// </summary>
    public bool TryConsumeChainAttackCount()
    {
        if (!CanConsumeChainAttack) return false;

        ConsumedChainAttackCount++;

        Debug.Log(
            $"[{gameObject.name} Chain 횟수 소비] " +
            $"{ConsumedChainAttackCount}/{MaxChainAttackCount}"
        );
        return true;
    }

    /// <summary>
    /// 그로기 종료 시 Chain Attack 횟수 0 초기화
    /// </summary>
    private void ResetChainAttackCount()
    {
        MaxChainAttackCount = 0;
        ConsumedChainAttackCount = 0;
    }

    #endregion

}
