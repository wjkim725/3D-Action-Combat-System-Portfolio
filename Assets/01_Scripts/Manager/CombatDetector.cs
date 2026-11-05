using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatDetector : MonoBehaviour
{
    public static CombatDetector Instance { get; private set; }

    [Header("Detection Settings")]
    [SerializeField] private float _detectRange = 10f;
    [SerializeField] private float _combatExitDelay = 5f;
    [SerializeField] private LayerMask _enemyLayerMask;
    [SerializeField] private float _checkInterval = 0.2f;

    private float _lastCombatTimer = 0f;
    private bool _isPartyInCombat = false;
    private float _combatHoldUntil;
    private PartyStateManager _partyStateManager;

    // 공격 상태와 독립적으로 에너지 자동회복 종료 유예를 계산하는 마지막 적 감지 시각.
    private float _lastEnemyDetectedTime = 0f;

    /// <summary>
    /// 적 감지와 종료 유예에 따른 파티 에너지 자동회복 허용 여부를 반환함.
    /// 개별 Hero의 비조작 여부, 생존 여부와 에너지 잔량은 HeroStatus에서 별도 확인이 필요함.
    /// </summary>
    public bool CanRecoverEnergy { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (Instance != this) return;
        _partyStateManager = PartyStateManager.Instance;
        if (_partyStateManager != null)
        {
            _partyStateManager.OnPartyMembersChanged += SyncPartyCombatState;
            SyncPartyCombatState();
        }
        // 매니저에서 단 하나의 루틴만 구동 (파티원 수와 상관없이 연산량 고정)
        StartCoroutine(CombatCheckRoutine());
    }

    private void OnDestroy()
    {
        if (_partyStateManager != null)
            _partyStateManager.OnPartyMembersChanged -= SyncPartyCombatState;
        if (Instance == this) Instance = null;
    }

    #region [전투 상태 판정 영역]

    /// <summary>
    /// 지정한 게임 시간 동안 파티의 전투 상태를 유지하며 적 감지·에너지 회복 판정은 변경하지 않음.
    /// </summary>
    public void HoldCombatState(float duration)
    {
        if (duration <= 0f) return;
        _combatHoldUntil = Mathf.Max(_combatHoldUntil, Time.time + duration);
        SetPartyCombatState(true);
    }

    /// <summary>
    /// 조작 캐릭터를 기준으로 전투 연출 상태와 에너지 자동회복 허용 여부를 주기적으로 갱신함.
    /// </summary>
    private IEnumerator CombatCheckRoutine()
    {
        while (true)
        {
            if (PartyStateManager.Instance.CurrentHero != null)
            {
                // 온필드 캐릭터 기준으로 물리 체크
                bool enemyDetected = Physics.CheckSphere
                    (PartyStateManager.Instance.CurrentHero.transform.position, _detectRange, _enemyLayerMask);

                // 전투 연출 상태가 유지되는 중에도 적 감지 기준의 회복 판정을 독립적으로 갱신함.
                UpdateEnergyRecoveryState(enemyDetected);

                // 온필드 캐릭터의 상태 체크
                bool anyCombatAction = PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.Attack ||
                PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.ChargeAttack ||
                                     PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.Skill ||
                                     PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.Ult;

                if (enemyDetected || anyCombatAction)
                {
                    _lastCombatTimer = Time.time;
                    SetPartyCombatState(true);
                }
                else
                {
                    if (Time.time >= _combatHoldUntil &&
                        Time.time - _lastCombatTimer > _combatExitDelay)
                    {
                        SetPartyCombatState(false);
                    }
                }
            }
            else
            {
                // 감지 기준이 되는 조작 캐릭터가 없으면 자동회복을 허용하지 않음.
                CanRecoverEnergy = false;
            }

            yield return new WaitForSeconds(_checkInterval);
        }
    }

    /// <summary>
    /// 적 감지 시 자동회복을 허용하고, 마지막 적 감지 이후 종료 유예 시간이 지나면 해제함.
    /// 공격 상태는 회복 시작 조건이나 종료 유예 갱신에 사용하지 않음.
    /// </summary>
    private void UpdateEnergyRecoveryState(bool enemyDetected)
    {
        if (enemyDetected)
        {
            _lastEnemyDetectedTime = Time.time;
            CanRecoverEnergy = true;
            return;
        }

        if (CanRecoverEnergy && Time.time - _lastEnemyDetectedTime > _combatExitDelay)
        {
            CanRecoverEnergy = false;
        }
    }

    /// <summary>
    /// 전투 연출 상태가 변경되면 파티원 전체의 Hero 전투 상태를 동기화함.
    /// </summary>
    private void SetPartyCombatState(bool isCombat)
    {
        if (_isPartyInCombat == isCombat) return;
        _isPartyInCombat = isCombat;

        SyncPartyCombatState();
    }

    /// <summary>
    /// 파티 구성 변경 시 새로 합류한 Hero에도 현재 전투 판정을 적용함.
    /// </summary>
    private void SyncPartyCombatState()
    {
        // 초기 입장 요청은 이 컴포넌트의 Start보다 먼저 도착할 수 있음.
        PartyStateManager party = _partyStateManager != null
            ? _partyStateManager : PartyStateManager.Instance;
        if (party == null || party.PartyMembers == null) return;

        foreach (Hero hero in party.PartyMembers)
        {
            if (hero != null) hero.IsCombat = _isPartyInCombat;
        }
    }

    #endregion
}
