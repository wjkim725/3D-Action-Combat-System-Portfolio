using System;
using UnityEngine;

// 투사체 한 발의 직선 이동, 수명, 충돌 결과 처리 및 풀 반환
[RequireComponent(typeof(Rigidbody))]
public class ProjectileController : MonoBehaviour
{
    [Header("Collision Sensor")]
    [SerializeField]
    private ProjectileCollisionSensor _collisionSensor;

    [Header("Projectile Trail")]
    [SerializeField] private SwordTrailController _swordTrail;

    [Header("Projectile Particles")]
    [SerializeField] private ParticleSystem _flightParticles;

    [Header("Explosion VFX")]
    [SerializeField] private ParticleVFXController _explosionVFXPrefab;

    [Header("Spawn Clearance")]
    [SerializeField, Min(0f)] private float _maxSpawnCorrectionDistance = 0.5f;

    private const int MaxSpawnCorrectionIterations = 4;
    private const float SpawnSeparationMargin = 0.01f;

    private Rigidbody _rigidbody;
    private CombatEventContext _context;
    private bool _hasResolvedCollision;
    private float _launchSpeed;
    private float _remainingLifeTime;
    private Vector3 _flightDirection;
    private Vector3 _previousPosition;
    private Action<ProjectileController> _returnToPool;

    #region [초기화 및 이동]

    private void Awake()
    {
        CacheComponents();
    }

    /// <summary>
    /// 비활성 프리팹도 첫 발사 전에 Rigidbody와 Sensor 참조를 준비한다.
    /// </summary>
    private void CacheComponents()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_collisionSensor != null)
        {
            _collisionSensor.Initialize(this);
        }
    }

    private void FixedUpdate()
    {
        if (_context == null || _hasResolvedCollision) return;

        if (_context.Attacker.AttackerTransform == null || _remainingLifeTime <= 0f)
        {
            FinishProjectile();
            return;
        }

        // 마지막 물리 틱에서는 남은 수명만큼만 이동한다.
        float moveTime = Mathf.Min(Time.fixedDeltaTime, _remainingLifeTime);
        _previousPosition = _rigidbody.position;
        _rigidbody.MovePosition(
            _previousPosition + _flightDirection * (_launchSpeed * moveTime));
        _remainingLifeTime -= Time.fixedDeltaTime;
    }

    /// <summary>
    /// Launcher의 전투 정보·속도·수명을 저장하고 비행이 유지되면 궤적 재생을 시작함.
    /// </summary>
    /// <returns>발사 설정과 위치가 유효하면 true. 생성 즉시 적중한 경우도 성공으로 처리함.</returns>
    public bool Initialize(CombatEventContext context, float launchSpeed, float lifeTime)
    {
        CacheComponents();
        if (context == null || context.Attacker == null ||
            context.Attacker.AttackerTransform == null ||
            launchSpeed <= 0f || lifeTime <= 0f ||
            context.HitRadius < 0f || float.IsNaN(context.HitRadius) ||
            float.IsInfinity(context.HitRadius))
        {
            Debug.LogError($"[{name}] 투사체 발사 정보가 올바르지 않습니다.", this);
            FinishProjectile();
            return false;
        }

        Collider sensorCollider = _collisionSensor != null
            ? _collisionSensor.GetComponent<Collider>() : null;

        if (_rigidbody == null || !_rigidbody.isKinematic ||
            sensorCollider == null || !sensorCollider.enabled || !sensorCollider.isTrigger ||
            sensorCollider.GetComponentInParent<Rigidbody>(true) != _rigidbody)
        {
            Debug.LogError(
                $"[{name}] 루트의 Kinematic Rigidbody와 연결된 Sensor의 Trigger Collider를 확인하세요.", this);
            FinishProjectile();
            return false;
        }

        _context = context;
        _hasResolvedCollision = false;
        _launchSpeed = launchSpeed;
        _remainingLifeTime = lifeTime;
        _flightDirection = transform.forward;
        _rigidbody.position = transform.position;
        _rigidbody.rotation = transform.rotation;
        _previousPosition = _rigidbody.position;

        // 상태를 모두 교체한 뒤 Collider를 활성화하여 이전 발사의 정보가 사용되지 않게 한다.
        gameObject.SetActive(true);

        // 첫 물리 이동 전에 지형 겹침을 해소하고 밀착한 적을 판정한다.
        Physics.SyncTransforms();
        if (!TryResolveSpawnPosition())
        {
            Debug.LogWarning($"[{name}] 발사 위치의 지형 겹침 또는 차폐를 해소하지 못해 발사를 종료합니다.", this);
            FinishProjectile();
            return false;
        }

        _previousPosition = _rigidbody.position;
        ResolveInitialTargetOverlap();

        // 초기 겹침 판정에서 이미 종료된 투사체의 궤적 재생을 방지함.
        if (_context != null && !_hasResolvedCollision && gameObject.activeInHierarchy)
        {
            _swordTrail?.Play();
            if (_flightParticles != null)
            {
                _flightParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _flightParticles.Play(true);
            }
        }

        return true;
    }

    #endregion

    #region [발사 직후 겹침 처리]

    /// <summary>
    /// 지형에서 최소 거리로 분리하되 보정 총거리와 횟수를 제한하고 벽 반대편 발사를 차단한다.
    /// </summary>
    private bool TryResolveSpawnPosition()
    {
        float correctionDistance = 0f;

        for (int iteration = 0; iteration <= MaxSpawnCorrectionIterations; iteration++)
        {
            Vector3 separationDirection = Vector3.zero;
            float separationDistance = 0f;

            foreach (Collider other in _collisionSensor.GetOverlapCandidates())
            {
                if (!IsBlockingSurface(other)) continue;
                if (!_collisionSensor.TryGetPenetration(other, out Vector3 direction, out float distance)) continue;

                // 한 번에 가장 깊은 겹침부터 해소하고, 새 위치에서 다른 지형을 다시 검사한다.
                if (distance <= separationDistance) continue;
                separationDirection = direction;
                separationDistance = distance;
            }

            if (separationDistance <= 0f)
            {
                Transform attacker = _context.Attacker.AttackerTransform;
                Collider attackerCollider = attacker.GetComponent<Collider>();
                Vector3 attackerOrigin = attackerCollider != null && attackerCollider.enabled
                    ? attackerCollider.bounds.center : attacker.position;

                return !IsPathBlocked(attackerOrigin, _rigidbody.position);
            }

            float stepDistance = separationDistance + SpawnSeparationMargin;
            if (iteration == MaxSpawnCorrectionIterations ||
                correctionDistance + stepDistance > _maxSpawnCorrectionDistance)
            {
                return false;
            }

            Vector3 position = _rigidbody.position + separationDirection * stepDistance;
            _rigidbody.position = position;
            transform.position = position;
            correctionDistance += stepDistance;
            Physics.SyncTransforms();
        }

        return false;
    }

    /// <summary>
    /// 보정된 Collider와 겹친 유효 대상 중 가장 가까운 한 명을 기존 적중 경로로 처리한다.
    /// </summary>
    private void ResolveInitialTargetOverlap()
    {
        Collider closestTarget = null;
        float closestDistance = float.PositiveInfinity;

        foreach (Collider other in _collisionSensor.GetOverlapCandidates())
        {
            if (IsIgnoredCollider(other)) continue;
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || !IsValidDamageTarget(target, other)) continue;
            if (!_collisionSensor.TryGetPenetration(other, out _, out _)) continue;

            Vector3 targetPoint = other.ClosestPoint(_rigidbody.position);
            if (IsPathBlocked(_rigidbody.position, targetPoint)) continue;

            float distance = (targetPoint - _rigidbody.position).sqrMagnitude;
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestTarget = other;
        }

        if (closestTarget != null) HandleCollision(closestTarget);
    }

    /// <summary>
    /// 자기 자신·공격자·다른 투사체 Collider를 판정 후보에서 제외한다.
    /// </summary>
    private bool IsIgnoredCollider(Collider other)
    {
        return other == null ||
            other.transform.IsChildOf(_context.Attacker.AttackerTransform) ||
            other.GetComponentInParent<ProjectileController>() != null;
    }

    /// <summary>
    /// 기존 충돌 규칙과 동일하게 피해 대상이 아닌 일반 Collider를 막는 지형으로 취급한다.
    /// </summary>
    private bool IsBlockingSurface(Collider other)
    {
        return !IsIgnoredCollider(other) && !other.isTrigger &&
            other.GetComponentInParent<IDamageable>() == null;
    }

    /// <summary>
    /// 보정 위치 또는 초기 적중 대상까지의 경로를 지형이 막고 있는지 확인한다.
    /// </summary>
    private bool IsPathBlocked(Vector3 origin, Vector3 destination)
    {
        Vector3 offset = destination - origin;
        float distance = offset.magnitude;
        if (distance < 0.0001f) return false;

        foreach (RaycastHit hit in Physics.RaycastAll(
            origin, offset / distance, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsBlockingSurface(hit.collider)) return true;
        }

        return false;
    }

    #endregion

    #region [충돌 판정 및 피해 전달]

    /// <summary>
    /// Sensor의 첫 유효 접촉에서 반경이 양수면 폭발, 0이면 단일 타격을 처리함.
    /// </summary>
    public void HandleCollision(Collider other)
    {
        if (_context == null || _hasResolvedCollision || other == null) return;

        Transform attackerTransform = _context.Attacker.AttackerTransform;
        if (attackerTransform == null)
        {
            FinishProjectile();
            return;
        }

        if (IsIgnoredCollider(other))
        {
            return;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            if (!IsValidDamageTarget(target, other)) return;
        }
        else if (other.isTrigger)
        {
            return;
        }

        // 피해 처리 중 추가 접촉이 발생해도 한 발은 한 번만 처리함.
        _hasResolvedCollision = true;

        try
        {
            CombatManager combatManager = CombatManager.Instance;
            if (combatManager == null)
            {
                Debug.LogError($"[{name}] CombatManager가 없습니다.", this);
                return;
            }

            PopulateImpactData(other);
            // 대상별 표면 정보가 Context를 덮어쓰기 전에 폭발 중심을 확정함.
            Vector3 explosionOrigin = _context.ImpactPoint;
            Vector3 explosionDirection = _context.SurfaceNormal;
            bool isHitSuccess = false;
            if (_context.HitRadius > 0f)
            {
                isHitSuccess = ExecuteExplosion(explosionOrigin, target != null ? other : null);
                PlayExplosionVFX(explosionOrigin, explosionDirection);
            }
            else if (target != null)
            {
                _context.Target = target;
                ResolveReactionDirection(other);
                isHitSuccess = combatManager.ProcessHit(_context);
            }

            if (isHitSuccess)
            {
                combatManager.PayBackCombatResource(_context);
            }
        }
        finally
        {
            // 무적 등으로 피해가 거부되어도 유효한 대상에 접촉한 한 발은 종료한다.
            FinishProjectile();
        }
    }

    /// <summary>
    /// 접촉 지점의 반경 안에 폭발 피해를 전달하고 실제 타격 성공 여부를 반환함.
    /// </summary>
    private bool ExecuteExplosion(Vector3 origin, Collider directContact)
    {
        LayerMask targetLayerMask = Physics.AllLayers;
        if (_context.Attacker is Hero hero)
        {
            targetLayerMask = hero.EnemyLayerMask;
        }
        else if (_context.Attacker is Enemy enemy)
        {
            targetLayerMask = enemy.HeroLayerMask;
        }

        return AreaHitUtility.Execute(_context, origin, _context.HitRadius,
            targetLayerMask, _flightDirection, directContact);
    }

    /// <summary>
    /// 투사체와 독립된 풀에서 접촉 지점의 폭발을 한 번 재생함.
    /// </summary>
    private void PlayExplosionVFX(Vector3 position, Vector3 direction)
    {
        if (_explosionVFXPrefab == null) return;

        ParticleVFXManager particleManager = ParticleVFXManager.Instance;
        if (particleManager == null)
        {
            Debug.LogWarning($"[{name}] 폭발 VFX를 재생할 ParticleVFXManager가 없습니다.", this);
            return;
        }

        particleManager.PlayProjectileExplosion(_explosionVFXPrefab, position, direction);
    }

    /// <summary>
    /// 공격자의 기존 적대 레이어 설정을 사용하고 자기 자신·아군·사망 대상을 제외한다.
    /// </summary>
    private bool IsValidDamageTarget(IDamageable target, Collider targetCollider)
    {
        if (ReferenceEquals(target, _context.Attacker) ||
            target.Status == null || target.Status.IsDead)
        {
            return false;
        }

        int targetLayer = 1 << targetCollider.gameObject.layer;
        if (_context.Attacker is Hero hero)
        {
            return target is not Hero && (hero.EnemyLayerMask.value & targetLayer) != 0;
        }

        if (_context.Attacker is Enemy enemy)
        {
            return target is not Enemy && (enemy.HeroLayerMask.value & targetLayer) != 0;
        }

        return true;
    }

    /// <summary>
    /// 접촉점이 없는 Trigger 이벤트를 위해 이전 위치 기준 최근접점과 근사 법선을 구성한다.
    /// </summary>
    private void PopulateImpactData(Collider targetCollider)
    {
        Vector3 impactPoint = targetCollider.ClosestPoint(_previousPosition);
        Vector3 surfaceNormal = _previousPosition - impactPoint;
        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = -_flightDirection;
        }

        _context.ImpactPoint = impactPoint;
        _context.ImpactDirection = _flightDirection;
        _context.SurfaceNormal = surfaceNormal.normalized;
        _context.SlashDirection = _flightDirection;
        _context.HasImpactData = true;
    }

    /// <summary>
    /// Auto는 투사체 진행 방향을 사용하고 명시된 피격 방향 설정은 그대로 따른다.
    /// </summary>
    private void ResolveReactionDirection(Collider targetCollider)
    {
        _context.ClearReactionDirection();

        switch (_context.ReactionDirectionMode)
        {
            case HitReactionDirectionMode.Auto:
            case HitReactionDirectionMode.AttackMotion:
                _context.SetReactionDirection(_flightDirection);
                break;

            case HitReactionDirectionMode.RadialFromOrigin:
                _context.SetReactionDirection(
                    targetCollider.bounds.center - _rigidbody.position);
                break;

            case HitReactionDirectionMode.AttackerToTarget:
                _context.SetReactionDirection(
                    targetCollider.bounds.center - _context.Attacker.AttackerTransform.position);
                break;

            case HitReactionDirectionMode.FixedFront:
                break;
        }
    }

    #endregion

    #region [투사체 종료]

    /// <summary>
    /// 풀에서 인스턴스를 생성할 때 반환할 경로를 한 번 연결한다.
    /// </summary>
    public void SetPoolReturnHandler(Action<ProjectileController> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    /// <summary>
    /// 판정·이동·궤적을 중단하고 이전 발사의 전투 참조 및 비행 상태를 비움.
    /// </summary>
    public void ResetForPool()
    {
        _hasResolvedCollision = true;
        _context = null;
        _launchSpeed = 0f;
        _remainingLifeTime = 0f;
        _flightDirection = Vector3.zero;
        _previousPosition = Vector3.zero;
        _swordTrail?.StopImmediate();
        if (_flightParticles != null)
        {
            _flightParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 충돌 또는 수명 종료 시 판정을 중단하고 풀로 반환한다.
    /// </summary>
    private void FinishProjectile()
    {
        _hasResolvedCollision = true;
        if (_returnToPool != null)
        {
            _returnToPool(this);
            return;
        }

        ResetForPool();
        Destroy(gameObject);
    }

    #endregion
}
