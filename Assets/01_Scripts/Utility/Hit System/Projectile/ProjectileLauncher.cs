using UnityEngine;

// 투사체 생성·발사 방향 결정·초기화
public class ProjectileLauncher : MonoBehaviour
{
    [Header("Launch References")]
    [SerializeField]
    private ProjectileController _projectilePrefab;

    [SerializeField]
    private Transform _launchAnchor;

    [Header("Launch Settings")]
    [SerializeField, Min(0.01f)] private float _launchSpeed;
    [SerializeField, Min(0.01f)] private float _lifeTime;


    #region [투사체 발사]

    private void Start()
    {
        if (ProjectilePoolManager.Instance != null)
        {
            ProjectilePoolManager.Instance.RegisterPrefab(_projectilePrefab);
        }
    }

    /// <summary>
    /// 타격 프레임에 현재 타깃 방향으로 투사체를 생성하고 발사 정보를 전달한다.
    /// </summary>
    /// <returns>발사에 성공하면 true. 설정 오류나 발사 위치 차폐 시 false를 반환한다.</returns>
    public bool Launch(CombatEventContext context)
    {
        if (context == null || context.Attacker == null ||
            context.Attacker.AttackerTransform == null)
        {
            Debug.LogError($"[{name}] 투사체의 공격자 정보가 없습니다.", this);
            return false;
        }

        if (_projectilePrefab == null || _launchAnchor == null)
        {
            Debug.LogError($"[{name}] Projectile Prefab 또는 Launch Anchor가 연결되지 않았습니다.", this);
            return false;
        }

        if (_launchSpeed <= 0f || _lifeTime <= 0f)
        {
            Debug.LogError($"[{name}] 투사체 속도와 수명은 0보다 커야 합니다.", this);
            return false;
        }

        Vector3 launchPosition = _launchAnchor.position;
        Vector3 launchDirection = ResolveLaunchDirection(context, launchPosition);

        ProjectilePoolManager poolManager = ProjectilePoolManager.Instance;
        if (poolManager == null)
        {
            Debug.LogError($"[{name}] 씬에 ProjectilePoolManager가 없습니다.", this);
            return false;
        }

        return poolManager.Launch(
            _projectilePrefab, launchPosition, Quaternion.LookRotation(launchDirection),
            context, _launchSpeed, _lifeTime);
    }

    /// <summary>
    /// 여진은 고정 대상, 일반 공격은 현재 록온 대상의 몸통을 조준한다.
    /// 각 대상이 무효하면 공격자 전방을 반환한다.
    /// </summary>
    private Vector3 ResolveLaunchDirection(
        CombatEventContext context,
        Vector3 launchPosition)
    {
        Vector3 forward = context.Attacker.AttackerTransform.forward;

        if (context.Attacker is not Hero hero) return forward;

        Enemy target;

        // 여진 공격 처리
        if (hero.CurrentState == HeroState.AfterShock)
        {
            if (hero.Mechanic is not IAfterShockTargetProvider mechanic)
            {
                return forward;
            }

            Transform afterShockTarget = mechanic.AfterShockTarget;
            if (afterShockTarget == null ||
                !afterShockTarget.gameObject.activeInHierarchy)
            {
                return forward;
            }

            target = afterShockTarget.GetComponentInParent<Enemy>();
            if (target == null || target.Status == null ||
                !target.gameObject.activeInHierarchy || target.Status.IsDead)
            {
                return forward;
            }
        }
        // 일반 공격 처리
        else
        {
            LockOnManager lockOnManager = LockOnManager.Instance;
            if (lockOnManager == null || lockOnManager.MagneticTarget == null)
            {
                return forward;
            }

            target = lockOnManager.MagneticTarget.GetComponentInParent<Enemy>();
            if (!lockOnManager.IsValidTarget(target) ||
                !target.gameObject.activeInHierarchy)
            {
                return forward;
            }
        }

        // 유효한 대상은 기존 방식대로 몸통을 조준한다.
        Collider targetCollider = target.GetComponent<Collider>();
        Vector3 targetPosition = targetCollider != null && targetCollider.enabled
            ? targetCollider.bounds.center
            : target.CamPivot.position;

        Vector3 direction = targetPosition - launchPosition;
        if (direction.sqrMagnitude < 0.0001f) return forward;

        return direction.normalized;
    }

    #endregion
}
