using UnityEngine;

public class EnemyHitReaction : MonoBehaviour
{
    private Enemy _enemy;

    [Header("Direction Settings")]
    [SerializeField]
    [Range(0f, 90f)]
    private float _frontDirectionHalfAngle = 75f;

    [Header("Reaction Cooldown Settings")]
    [SerializeField]
    [Min(0f)]
    private float _lightReactionCooldown = 0.3f;

    [SerializeField]
    [Min(0f)]
    private float _heavyReactionCooldown = 0.6f;

    private float _reactionCooldownEndTime;
    private HitType _cooldownSourceHitType = HitType.None;

    public bool IsReactionCooldownActive =>
        _cooldownSourceHitType != HitType.None &&
        Time.time < _reactionCooldownEndTime;

    public HitType CooldownSourceHitType => _cooldownSourceHitType;

    public float RemainingReactionCooldown =>
        IsReactionCooldownActive ? Mathf.Max(0f,_reactionCooldownEndTime - Time.time) : 0f;


    private HitReactionResistanceLevel _activeSuperArmorLevel = HitReactionResistanceLevel.None;
    public HitReactionResistanceLevel ActiveSuperArmorLevel => _activeSuperArmorLevel;
    public HitReactionResistanceLevel BaseResistanceLevel => ResolveBaseResistanceLevel();
    public HitReactionResistanceLevel EffectiveResistanceLevel => ResolveEffectiveResistanceLevel();
    public bool IsSuperArmorActive =>
        _activeSuperArmorLevel != HitReactionResistanceLevel.None;
    


    public HitType CurrentHitType => _currentHitType;
    private HitType _currentHitType = HitType.None;

    public IAttacker CurrentReactionAttacker => _currentReactionAttacker;
    private IAttacker _currentReactionAttacker;

    public bool IsReacting => _enemy != null && _enemy.CurrentState == EnemyState.Hit;


    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }


    #region [Hit Reaction 영역]

    /// <summary>
    /// 현재 Enemy가 해당 피격 반응을 시작할 수 있는지 검사하고,
    /// 가능하다면 Hit 상태로 전환하고 피격 애니메이션과 반응음을 재생함.
    /// </summary>
    public bool TryStartReaction(HitInfo hitInfo)
    {
        HitType incomingHitType = hitInfo.hitType;

        if (!CanStartReaction(incomingHitType))
        {
            return false;
        }

        /*
         * 새 경직 진입이 최종 승인되었으므로
         * 이전 경직에서 남아 있던 내부 쿨타임을 초기화
         *
         * 대표 사례:
         * Light 경직 쿨타임 도중 Heavy 경직 허용
         * → 기존 Light 쿨타임 제거
         * → Heavy 경직 종료 후 Heavy 쿨타임으로 교체
         */
        ClearReactionCooldown();

        HitReactionDirection reactionDirection =
            ResolveReactionDirection(hitInfo);

        _currentHitType = incomingHitType;
        _currentReactionAttacker = hitInfo.attacker;

        Debug.Log($"HitDirection : {reactionDirection}");

        /*
         * Light → Heavy는 둘 다 EnemyState.Hit이므로
         * 이미 Hit 상태라면 FSM을 재진입 X
         *
         * Heavy 피격 애니메이션만 새로 재생하여
         * 현재 Light 경직을 덮어씀
         */
        if (_enemy.CurrentState != EnemyState.Hit)
        {
            _enemy.ChangeState(EnemyState.Hit);
        }

        _enemy.Animation.PlayHitReaction(
            incomingHitType,
            reactionDirection);
        _enemy.Audio?.PlayHit();

        return true;
    }

    private bool CanStartReaction(HitType incomingHitType)
    {
        if (_enemy == null)
        {
            return false;
        }

        if (_enemy.CurrentState == EnemyState.Dead ||
            _enemy.CurrentState == EnemyState.Groggy)
        {
            return false;
        }

        if (!IsSupportedReactionType(incomingHitType))
        {
            return false;
        }

        if (IsReactionBlockedByResistance(incomingHitType))
        {
            return false;
        }

        // 이미 경직 중이라면 쿨타임이 아니라 경직 우선순위로 판단한다.
        // Light → Heavy는 허용하고, Light → Light / Heavy → 모든 경직은 차단한다.
        if (IsReacting)
        {
            return GetReactionPriority(incomingHitType) >
                   GetReactionPriority(_currentHitType);
        }

        // 경직이 끝난 뒤에만 내부 쿨타임 정책을 적용한다.
        if (IsReactionBlockedByCooldown(incomingHitType))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// EnemyState.Hit에서 빠져나갈 때
    /// 현재 피격 정보를 초기화
    /// </summary>
    public void OnExitReactionState()
    {
        _currentHitType = HitType.None;
        _currentReactionAttacker = null;
    }

    /// <summary>
    /// 피격 애니메이션 종료 시 Hit 상태를 마무리
    /// </summary>
    public void CompleteReaction(
    HitType completedHitType)
    {
        if (_enemy == null ||
            _enemy.CurrentState != EnemyState.Hit)
        {
            return;
        }

        // 이전 피격 애니메이션에서 뒤늦게 들어온 종료 Event 방어
        if (_currentHitType != completedHitType)
        {
            return;
        }

        /*
         * 상태를 변경하기 전에 완료된 경직 기준으로
         * 종료 후 내부 쿨타임을 시작한다.
         *
         * ChangeState(Idle) 실행 과정에서
         * OnExitReactionState()가 현재 경직 정보를 초기화하므로
         * 반드시 상태 전환보다 먼저 실행해야 한다.
         */
        StartReactionCooldown(completedHitType);

        _enemy.ChangeState(EnemyState.Idle);
    }

    /// <summary>
    /// HitInfo의 월드 피격 방향을
    /// Enemy 로컬 기준 Front, Left, Right로 변환
    /// </summary>
    private HitReactionDirection ResolveReactionDirection(
    HitInfo hitInfo)
    {
        if (!hitInfo.hasReactionDirection)
        {
            return HitReactionDirection.Front;
        }

        Vector3 planarDirection =
            Vector3.ProjectOnPlane(
                hitInfo.reactionDirection,
                transform.up);

        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            return HitReactionDirection.Front;
        }

        float signedAngle =
            Vector3.SignedAngle(
                transform.forward,
                planarDirection.normalized,
                transform.up);

        if (Mathf.Abs(signedAngle) <=
            _frontDirectionHalfAngle)
        {
            return HitReactionDirection.Front;
        }

        return signedAngle < 0f
            ? HitReactionDirection.Left
            : HitReactionDirection.Right;
    }

    private bool IsSupportedReactionType(HitType hitType)
    {
        return hitType == HitType.Light || hitType == HitType.Heavy;
    }

    private int GetReactionPriority(HitType hitType)
    {
        return hitType switch
        {
            HitType.Light => 1,
            HitType.Heavy => 2,
            _ => 0
        };
    }

    #endregion


    #region [경직 저항(슈퍼아머) 영역]

    /// <summary>
    /// EnemyRank에 따라 해당 Enemy가 상시 보유하는
    /// 기본 피격 경직 저항 등급을 반환
    /// </summary>
    private HitReactionResistanceLevel ResolveBaseResistanceLevel()
    {
        if (_enemy == null)
        {
            return HitReactionResistanceLevel.None;
        }

        return _enemy.Rank switch
        {
            EnemyRank.Normal =>
                HitReactionResistanceLevel.None,

            EnemyRank.Elite =>
                HitReactionResistanceLevel.Light,

            EnemyRank.Boss =>
                HitReactionResistanceLevel.Light,

            _ =>
                HitReactionResistanceLevel.None
        };
    }

    /// <summary>
    /// Rank 기반 기본 저항과 현재 활성화된 슈퍼아머 중
    /// 더 높은 등급을 실제 경직 저항으로 반환
    /// </summary>
    private HitReactionResistanceLevel ResolveEffectiveResistanceLevel()
    {
        HitReactionResistanceLevel baseResistance =
            ResolveBaseResistanceLevel();

        return
            (int)_activeSuperArmorLevel > (int)baseResistance
                ? _activeSuperArmorLevel : baseResistance;
    }

    /// <summary>
    /// 현재 Enemy의 최종 경직 저항 등급을 기준으로
    /// 전달된 피격 타입의 경직 반응을 차단할지 판단
    /// </summary>
    private bool IsReactionBlockedByResistance(
        HitType incomingHitType)
    {
        HitReactionResistanceLevel resistanceLevel =
            ResolveEffectiveResistanceLevel();

        switch (resistanceLevel)
        {
            case HitReactionResistanceLevel.None:
                return false;

            case HitReactionResistanceLevel.Light:
                return incomingHitType ==
                       HitType.Light;

            case HitReactionResistanceLevel.Heavy:
                return incomingHitType ==
                           HitType.Light ||
                       incomingHitType ==
                           HitType.Heavy;

            case HitReactionResistanceLevel.Max:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 현재 Enemy에게 일시적 슈퍼아머를 적용
    /// 일반적으로 EnemyState.Attack 진입 시
    /// PatternData 접근해서 적용
    /// </summary>
    public void ActivateSuperArmor(
        HitReactionResistanceLevel resistanceLevel)
    {
        _activeSuperArmorLevel = resistanceLevel;
    }

    /// <summary>
    /// 현재 활성화된 일시적 슈퍼아머를 해제.
    /// EnemyRank 기반 기본 저항에는 영향 X
    /// </summary>
    public void DeactivateSuperArmor()
    {
        _activeSuperArmorLevel = HitReactionResistanceLevel.None;
    }

    #endregion


    #region [경직 내부 쿨타임 관리 영역]

    /// <summary>
    /// 정상적으로 종료된 경직 타입에 대응하는
    /// 종료 후 내부 쿨타임을 반환
    /// </summary>
    private float GetReactionCooldownDuration(
        HitType hitType)
    {
        return hitType switch
        {
            HitType.Light =>
                _lightReactionCooldown,

            HitType.Heavy =>
                _heavyReactionCooldown,

            _ =>
                0f
        };
    }

    /// <summary>
    /// 정상적으로 완료된 경직 타입을 기준으로
    /// 종료 후 내부 쿨타임을 시작
    /// </summary>
    private void StartReactionCooldown(
        HitType completedHitType)
    {
        float cooldownDuration =
            GetReactionCooldownDuration(
                completedHitType
            );

        if (cooldownDuration <= 0f)
        {
            ClearReactionCooldown();
            return;
        }

        _cooldownSourceHitType =
            completedHitType;

        _reactionCooldownEndTime =
            Time.time +
            cooldownDuration;
    }

    /// <summary>
    /// 현재 진행 중인 경직 내부 쿨타임 정보를 초기화
    /// 더 높은 등급의 새 경직이 허용됐을 때 사용
    /// </summary>
    private void ClearReactionCooldown()
    {
        _cooldownSourceHitType =
            HitType.None;

        _reactionCooldownEndTime =
            0f;
    }

    /// <summary>
    /// 쿨타임 종료 시각이 지났다면
    /// 남아 있는 쿨타임 정보를 초기화
    /// </summary>
    private void ClearExpiredReactionCooldown()
    {
        if (_cooldownSourceHitType ==
            HitType.None)
        {
            return;
        }

        if (Time.time <
            _reactionCooldownEndTime)
        {
            return;
        }

        ClearReactionCooldown();
    }

    /// <summary>
    /// 현재 진행 중인 내부 쿨타임의 기준 경직과
    /// 새로 들어온 경직 타입을 비교하여 차단 여부를 반환
    /// </summary>
    private bool IsReactionBlockedByCooldown(
        HitType incomingHitType)
    {
        ClearExpiredReactionCooldown();

        if (!IsReactionCooldownActive)
        {
            return false;
        }

        switch (_cooldownSourceHitType)
        {
            case HitType.Light:
                /*
                 * Light 종료 쿨타임에서는
                 * Light만 차단하고 Heavy는 허용
                 */
                return incomingHitType ==
                       HitType.Light;

            case HitType.Heavy:
                /*
                 * Heavy 종료 쿨타임에서는
                 * Light와 Heavy를 모두 차단
                 */
                return incomingHitType ==
                           HitType.Light ||
                       incomingHitType ==
                           HitType.Heavy;

            default:
                return false;
        }
    }

    #endregion

}