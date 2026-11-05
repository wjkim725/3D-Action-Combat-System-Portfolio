using System;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

/// <summary>
/// 전투 액션 과정에서 연계 공격(Combo), 타수(Hit) 수치를 전담 관리하는 컴포넌트
/// </summary>
public class HeroActionRegistry : MonoBehaviour
{
    private Hero _hero;


    #region [프로퍼티 영역]
    
    /// <summary>
    /// 액션 무적 또는 CombatSandBox 무적에 따른 최종 무적 판정 여부를 반환함.
    /// </summary>
    public bool IsInvincibilityActive => _isInvincible || _isSandboxInvincible;
    /// <summary>
    /// CombatSandBox 무적을 제외한 액션 자체의 무적 여부를 반환함.
    /// </summary>
    public bool IsActionInvincible => _isInvincible;
    public bool IsSuperArmorActive => _isSuperArmorActive;
    public bool IsComboLinkable => _isComboLinkable;
    public bool IsComboReserved => _isComboReserved;
    public int CurrentComboIndex => _currentComboIndex;
    public int CurrentHitIndex => _currentHitIndex;
    public bool IsAttackButtonHolding => _isAttackButtonHolding;
    public bool IsSkillButtonHolding => _isSkillButtonHolding;
    public bool IsCancelWindowOpen => _isCancelWindowOpen;
    public bool IsManualDodgeBlocked => _isManualDodgeBlocked;
    public bool IsAnimMoveActive => _isAnimMoveActive;
    public bool IsTargetTrackingActive => _isTargetTrackingActive;
    public bool IsOffFieldActionActive => _isOffFieldActionActive;
    public bool IsParryCounterReserved => _isParryCounterReserved;
    public bool IsParryCounterWindowOpen => _isParryCounterWindowOpen;
    public bool CanPerfectDodgeCounter => _canPerfectDodgeCounter;

    #endregion


    [Header("IsInvincible")]
    private bool _isInvincible = false;             // 무적 판정 여부
    private bool _isSandboxInvincible; // CombatSandBox 기능: 액션 종료와 무관하게 유지되는 무적 플래그.

    [Header("IsSuperArmorActive")]
    private bool _isSuperArmorActive = false;       // 슈퍼아머 여부

    [Header("Combo Fields")]
    private int _currentComboIndex = 0;             // 현재 콤보 단계
    private bool _isComboLinkable = false;          // 콤보 연계 가능 여부
    private bool _isComboReserved = false;          // 다음 콤보 예약 상태

    [Header("Hit Fields")]
    /// 하나의 콤보(모션)에 다단히트 Index
    /// 콤보가 리셋되거나 시작할 때 무조건 0 으로 리셋
    private int _currentHitIndex = 0;               

    [Header("Charge Fields")]
    private bool _isAttackButtonHolding = false;    // 공격 버튼 유지 여부
    private bool _isSkillButtonHolding = false;     // 스킬 버튼 유지 여부

    [Header("CancleWindow Fields")]
    private bool _isCancelWindowOpen = false;       // 기존 모션 캔슬 가능 여부

    [Header("AnimMovement Fields")]
    private bool _isAnimMoveActive = false;         // 공격 애니메이션 중 이 플래그가 켜져있으면 Movement에서 이동처리
    private bool _isTargetTrackingActive = false;   // 애니메이션 이동 방향을 타겟 기준으로 지속 갱신할지 여부

    [Header("Swap Fields")]
    private bool _isOffFieldActionActive = false;
    
    private bool _isParryCounterReserved = false;
    private bool _isParryCounterWindowOpen = false;

    [Header("Perfect Dodge Activate Flag")]
    private bool _canPerfectDodgeCounter = false;

    [Header("Dodge Counter Reservation")]
    private bool _isDodgeCounterReserved;
    public bool IsDodgeCounterReserved => _isDodgeCounterReserved;

    [Header("Manual Dodge")]
    private bool _isManualDodgeBlocked = false;



    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }

    #region [필드 관리 메서드 영역]

    /// <summary>
    /// 수동 회피 입력의 차단 여부를 설정함.
    /// </summary>
    public void SetIsManualDodgeBlocked(bool isBlocked) => _isManualDodgeBlocked = isBlocked;

    public void SetIsInvincible(bool isInvincible)
    {
        if (_isInvincible == isInvincible) return;
        _isInvincible = isInvincible;
    }

    /// <summary>
    /// CombatSandBox 기능의 무적 여부를 기존 액션 무적과 독립적으로 설정함.
    /// </summary>
    public void SetSandboxInvincible(bool isEnabled)
    {
        _isSandboxInvincible = isEnabled;
    }

    public void SetIsSuperArmorActive(bool isActive)
    {
        if (_isSuperArmorActive == isActive) return;
        _isSuperArmorActive = isActive;
    }
    public void IncrementComboIndex() 
    { 
        _currentComboIndex++;
        _currentHitIndex = 0;   // HitIndex 규칙 준수

        if (_hero != null && _hero.Animation != null)
        {
            _hero.Animation.SetComboIndex(_currentComboIndex);
        }
    }
    public void ResetComboIndex() 
    { 
        _currentComboIndex = 0;
        _currentHitIndex = 0;   // HitIndex 규칙 준수

        if (_hero != null && _hero.Animation != null)
        {
            _hero.Animation.SetComboIndex(0);
        }
    }
    /// <summary>
    /// 명시적으로 콤보 인덱스를 정해줘야 하는 경우 사용됨 
    /// ex) DashAttack -> Attack (2타부터)
    /// </summary>
    /// <param name="index"></param>
    public void SetComboIndex(int index)
    {
        _currentComboIndex = index;
        _currentHitIndex = 0;   // HitIndex 규칙 준수

        if (_hero != null && _hero.Animation != null)
        {
            _hero.Animation.SetComboIndex(index);
        }
    }
    public void SetIsComboLinkable(bool isComboLinkable) => _isComboLinkable = isComboLinkable;
    public void SetIsComboReserved(bool isComboReserved)
    {
        // 중복처리 방어 코드
        if (_isComboReserved == isComboReserved) return;

        _isComboReserved = isComboReserved;

        if (_hero != null && _hero.Animation != null)
        {
            _hero.Animation.SetIsComboReserved(isComboReserved);
        }
    }
    /// <summary>공격 버튼의 유지 여부를 기록한다.</summary>
    public void SetIsAttackButtonHolding(bool isHolding) => _isAttackButtonHolding = isHolding;

    /// <summary>스킬 버튼의 유지 여부를 기록한다.</summary>
    public void SetIsSkillButtonHolding(bool isHolding) => _isSkillButtonHolding = isHolding;
    public void OpenCancelWindow() => _isCancelWindowOpen = true;
    public void CloseCancelWindow() => _isCancelWindowOpen = false;
    public void ActivateAnimMove()
    {
        _isAnimMoveActive = true;
    }
    public void DeactivateAnimMove()
    {
        _isAnimMoveActive = false;
        _isTargetTrackingActive = false;
    }
    public void SetIsTargetTrackingActive(bool isActive) => _isTargetTrackingActive = isActive;
    public void SetOffFieldActionActive(bool isActive) => _isOffFieldActionActive = isActive;
    public void SetIsParryCounterResereved(bool isReserved)
    {
        if (_isParryCounterReserved == isReserved) return;

        _isParryCounterReserved = isReserved;

        if (_hero != null && _hero.Animation != null)
        {
            _hero.Animation.SetIsParryCounterReserved(isReserved);
        }
    }
    public void SetIsParryCounterWindowOpen(bool isOpen)
    {
        if (_isParryCounterWindowOpen == isOpen) return;
        _isParryCounterWindowOpen = isOpen;
    }

    public void SetCanPerfectDodgeCounter(bool canPerfectDodge)
    {
        if (_canPerfectDodgeCounter == canPerfectDodge) return;
        _canPerfectDodgeCounter = canPerfectDodge;
    }

    #endregion


    #region [보조 헬퍼 메서드 영역]

    /// <summary>
    /// 애니메이션 히트 프레임 이벤트가 올 때 호출
    /// 현재 장부 번호(HitIndex)를 먼저 반환한 뒤, 다음 타격을 위해 카운트를 1 증가
    /// </summary>
    public int GetHitIndexAndAdvance()
    {
        int indexToReturn = _currentHitIndex;
        _currentHitIndex++;
        return indexToReturn;
    }

    /// <summary>
    /// 패링 반격 예약과 즉시 반격 허용 창을 초기화함.
    /// </summary>
    public void ClearParryCounter()
    {
        SetIsParryCounterResereved(false);
        SetIsParryCounterWindowOpen(false);
    }

    public void ResetCombo()
    {
        ResetComboIndex();
        SetIsComboReserved(false);
        SetIsComboLinkable(false);
        SetIsParryCounterResereved(false);
        DeactivateAnimMove();
    }

    /// <summary>
    /// 대시, 피격, 혹은 공격 판정이 완전히 끝나 원점으로 돌아갈 때 모든 장부를 청소
    /// </summary>
    public void ResetActionRegistry()
    {
        ClearDodgeCounter();
        ClearParryCounter();
        _currentComboIndex = 0;
        _currentHitIndex = 0; // 히트 인덱스 청소
        _isComboReserved = false;
        _isComboLinkable = false;
        _isCancelWindowOpen = false;
        _isManualDodgeBlocked = false;
        _isAttackButtonHolding = false;
        _isSkillButtonHolding = false;
        _isInvincible = false; // 무적 상태도 안전하게 꺼줌
        _isSuperArmorActive = false;
        DeactivateAnimMove();
    }

    /// <summary>
    /// 극한회피 반격 권한을 소비하고 회피 종료 후 실행할 반격을 예약함.
    /// </summary>
    public bool TryReserveDodgeCounter()
    {
        if (_isDodgeCounterReserved || !_canPerfectDodgeCounter)
        {
            return false;
        }

        _canPerfectDodgeCounter = false;
        _isDodgeCounterReserved = true;
        return true;
    }

    /// <summary>
    /// 예약된 회피반격을 소비하고 실행 여부를 반환함.
    /// </summary>
    public bool ConsumeReservedDodgeCounter()
    {
        if (!_isDodgeCounterReserved)
        {
            return false;
        }

        _isDodgeCounterReserved = false;
        return true;
    }

    /// <summary>
    /// 회피 종료 또는 중단 시 남은 반격 권한과 예약을 정리함.
    /// </summary>
    public void ClearDodgeCounter()
    {
        _canPerfectDodgeCounter = false;
        _isDodgeCounterReserved = false;
    }

    #endregion
}
