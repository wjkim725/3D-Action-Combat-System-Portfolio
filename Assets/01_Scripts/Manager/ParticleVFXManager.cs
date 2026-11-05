using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ParticleVFXManager : MonoBehaviour
{
    public static ParticleVFXManager Instance { get; private set; }

    [Header("Pooling VFX Object Root")]
    [SerializeField] private Transform _particleVFXPoolRoot;

    [Header("Hit Spark")]
    [SerializeField] private ParticleVFXController _hitSparkPrefab;
    [SerializeField, Min(0)] private int _initialHitSparkPoolSize = 10;
    [SerializeField, Min(0f)] private float _hitSparkSpawnOffset = 0.05f;
    private readonly Queue<ParticleVFXController> _hitSparkPool = new();

    [Header("Parry Particle")]
    [SerializeField] private ParticleVFXController _parryParticlePrefab;
    [SerializeField, Min(0)] private int _initialParryParticlePoolSize = 3;
    [SerializeField, Range(0f, 1f)]
    private float _parryCameraDirectionWeight = 0.7f;
    [SerializeField, Min(0f)]
    private float _parryCameraOffset = 0.05f;
    private readonly Queue<ParticleVFXController> _parryParticlePool = new();

    [Header("Hit Reaction VFX")]
    [SerializeField, Min(0)] private int _initialHitReactionVFXPoolSize = 10;

    private readonly Dictionary<ParticleVFXController, Queue<ParticleVFXController>> 
        _hitReactionVFXPools = new();

    [Header("Projectile Explosion")]
    [SerializeField, Min(0)] private int _initialProjectileExplosionPoolSize = 5;

    private readonly Dictionary<ParticleVFXController, Queue<ParticleVFXController>>
        _projectileExplosionPools = new();



    [Header("Ultimate Prewarm")]
    [FormerlySerializedAs("_initialUltimatePoolSize")]
    [SerializeField, Min(0)] private int _ultimatePrewarmCount = 2;
    private PartyStateManager _partyStateManager;

    private readonly Dictionary<AttackVFXController, Queue<AttackVFXController>>
        _attackVFXPools = new();

    private void Awake()
    {
        // 싱글톤 예외 처리 및 인스턴스 지정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeHitSparkPool();
        InitializeParryParticlePool();
    }


    #region [궁극기 풀 사전 준비]

    private void Start()
    {
        _partyStateManager = PartyStateManager.Instance;
        if (_partyStateManager == null) return;

        _partyStateManager.OnPartyMembersChanged += PrewarmPartyUltimateVFX;
        PrewarmPartyUltimateVFX();
    }

    private void OnDestroy()
    {
        if (_partyStateManager != null)
            _partyStateManager.OnPartyMembersChanged -= PrewarmPartyUltimateVFX;
        if (Instance == this) Instance = null;
        _attackVFXPools.Clear();
    }

    /// <summary>
    /// 현재 파티의 궁극기 근접 연출을 프리팹별로 최초 한 번 사전 생성함.
    /// </summary>
    private void PrewarmPartyUltimateVFX()
    {
        if (_partyStateManager == null || _partyStateManager.PartyMembers == null) return;
        foreach (Hero hero in _partyStateManager.PartyMembers)
        {
            if (hero == null || hero.CombatData == null || hero.CombatData.ultAttackSequences == null) continue;
            foreach (var sequence in hero.CombatData.ultAttackSequences)
            {
                if (sequence.hits == null) continue;
                foreach (var hit in sequence.hits)
                {
                    AttackVFXController prefab = hit.hitVFXPrefab;
                    if (hit.hitDetectionType != HitDetectionType.MeleeOverlap || prefab == null ||
                        _attackVFXPools.ContainsKey(prefab)) continue;

                    var pool = new Queue<AttackVFXController>();
                    _attackVFXPools.Add(prefab, pool);
                    for (int i = 0; i < _ultimatePrewarmCount; i++)
                        pool.Enqueue(CreateAttackVFXInstance(prefab));
                }
            }
        }
    }

    #endregion

    #region [공격 연출 VFX]

    /// <summary>
    /// 프리팹별 풀에서 공격 연출을 대여하여 전투 판정과 독립적으로 재생함.
    /// </summary>
    public void PlayAttackVFX(AttackVFXController prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;
        if (!_attackVFXPools.TryGetValue(prefab, out Queue<AttackVFXController> pool))
        {
            pool = new Queue<AttackVFXController>();
            _attackVFXPools.Add(prefab, pool);
        }

        AttackVFXController instance = null;
        while (pool.Count > 0 && instance == null) instance = pool.Dequeue();
        if (instance == null)
            instance = CreateAttackVFXInstance(prefab);
        instance.transform.localScale = prefab.transform.localScale;
        instance.gameObject.SetActive(true);
        instance.Play(position, rotation);
    }

    /// <summary>
    /// 공격 연출을 생성하고 반환 경로를 연결한 뒤 비활성 상태로 준비함.
    /// </summary>
    private AttackVFXController CreateAttackVFXInstance(AttackVFXController prefab)
    {
        Transform root = _particleVFXPoolRoot != null ? _particleVFXPoolRoot : transform;
        AttackVFXController instance = Instantiate(prefab, root);
        instance.Initialize(vfx => ReturnAttackVFX(prefab, vfx));
        instance.gameObject.SetActive(false);
        return instance;
    }

    /// <summary>
    /// 종료한 공격 연출을 비활성화하고 프리팹별 풀에 한 번 반환함.
    /// </summary>
    private void ReturnAttackVFX(AttackVFXController prefab, AttackVFXController instance)
    {
        if (instance == null || !_attackVFXPools.TryGetValue(prefab, out Queue<AttackVFXController> pool)
            || pool.Contains(instance)) return;

        instance.gameObject.SetActive(false);
        Transform root = _particleVFXPoolRoot != null ? _particleVFXPoolRoot : transform;
        instance.transform.SetParent(root, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = prefab.transform.localScale;
        pool.Enqueue(instance);
    }

    #endregion

    #region [투사체 폭발 VFX]

    /// <summary>
    /// 프리팹별 풀에서 폭발을 대여하여 투사체와 독립된 위치와 수명으로 재생함.
    /// </summary>
    public void PlayProjectileExplosion(
        ParticleVFXController prefab, Vector3 position, Vector3 direction)
    {
        if (prefab == null) return;

        Queue<ParticleVFXController> pool = GetOrCreateProjectileExplosionPool(prefab);
        ParticleVFXController particle = null;
        while (pool.Count > 0 && particle == null)
        {
            particle = pool.Dequeue();
        }

        if (particle == null) particle = CreateProjectileExplosionInstance(prefab);

        particle.transform.SetPositionAndRotation(position, Quaternion.identity);
        particle.transform.localScale = prefab.transform.localScale;
        particle.gameObject.SetActive(true);
        particle.Play(position, direction);
    }

    /// <summary>
    /// 폭발 프리팹의 대기열을 최초 한 번 만들고 인스턴스를 준비함.
    /// </summary>
    private Queue<ParticleVFXController> GetOrCreateProjectileExplosionPool(
        ParticleVFXController prefab)
    {
        if (_projectileExplosionPools.TryGetValue(prefab, out Queue<ParticleVFXController> pool))
        {
            return pool;
        }

        pool = new Queue<ParticleVFXController>();
        _projectileExplosionPools.Add(prefab, pool);
        for (int i = 0; i < _initialProjectileExplosionPoolSize; i++)
        {
            pool.Enqueue(CreateProjectileExplosionInstance(prefab));
        }

        return pool;
    }

    /// <summary>
    /// 폭발 인스턴스를 공통 VFX 루트에 만들고 해당 프리팹 풀의 반환 경로를 연결함.
    /// </summary>
    private ParticleVFXController CreateProjectileExplosionInstance(ParticleVFXController prefab)
    {
        Transform root = _particleVFXPoolRoot != null ? _particleVFXPoolRoot : transform;
        ParticleVFXController particle = Instantiate(prefab, root);
        particle.Initialize(instance => ReturnProjectileExplosion(prefab, instance));
        particle.gameObject.SetActive(false);
        return particle;
    }

    /// <summary>
    /// 재생이 끝난 폭발을 비활성화하고 프리팹별 풀에 한 번 반환함.
    /// </summary>
    private void ReturnProjectileExplosion(ParticleVFXController prefab, ParticleVFXController particle)
    {
        if (particle == null ||
            !_projectileExplosionPools.TryGetValue(prefab, out Queue<ParticleVFXController> pool) ||
            pool.Contains(particle)) return;

        particle.gameObject.SetActive(false);
        Transform root = _particleVFXPoolRoot != null ? _particleVFXPoolRoot : transform;
        particle.transform.SetParent(root, false);
        particle.transform.localPosition = Vector3.zero;
        particle.transform.localRotation = Quaternion.identity;
        particle.transform.localScale = prefab.transform.localScale;
        pool.Enqueue(particle);
    }

    #endregion


    #region [Hit Spark 영역]

    /// <summary>
    /// HitSpark Pool Queue 초기화
    /// </summary>
    private void InitializeHitSparkPool()
    {
        if (_hitSparkPrefab == null)
        {
            Debug.LogError(
                "ParticleVFXManager: HitSpark 프리팹이 지정되지 않았습니다."
            );

            return;
        }

        for (int i = 0; i < _initialHitSparkPoolSize; i++)
        {
            _hitSparkPool.Enqueue(CreateHitSparkInstance());
        }
    }

    /// <summary>
    /// HitSpark 프리팹 인스턴스 생성
    /// </summary>
    private ParticleVFXController CreateHitSparkInstance()
    {
        Transform parent = _particleVFXPoolRoot != null
            ? _particleVFXPoolRoot
            : transform;

        ParticleVFXController newHitSpark =
            Instantiate(_hitSparkPrefab, parent);

        newHitSpark.Initialize(ReturnHitSpark);
        newHitSpark.gameObject.SetActive(false);

        return newHitSpark;
    }

    /// <summary>
    /// 타격 지점에 HitSpark 재생
    /// </summary>
    public void PlayHitSpark(
        IAttacker attacker,
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        if (attacker == null || _hitSparkPrefab == null)
        {
            return;
        }

        ParticleVFXController hitSpark =
            _hitSparkPool.Count > 0
                ? _hitSparkPool.Dequeue()
                : CreateHitSparkInstance();

        hitSpark.gameObject.SetActive(true);

        Vector3 outwardDirection = -impactDirection.normalized;
        Vector3 spawnPosition =
            impactPoint +
            outwardDirection * _hitSparkSpawnOffset;

        hitSpark.Play(
            spawnPosition,
            outwardDirection
        );
    }

    /// <summary>
    /// HitSpark Queue 반환
    /// </summary>
    private void ReturnHitSpark(ParticleVFXController hitSpark)
    {
        if (hitSpark == null) return;
        if (_hitSparkPool.Contains(hitSpark)) return;

        hitSpark.gameObject.SetActive(false);

        Transform parent = _particleVFXPoolRoot != null
            ? _particleVFXPoolRoot
            : transform;

        hitSpark.transform.SetParent(parent);
        _hitSparkPool.Enqueue(hitSpark);
    }

    #endregion


    #region [Parry Particle 영역]
    private void InitializeParryParticlePool()
    {
        if (_parryParticlePrefab == null)
        {
            Debug.LogError(
                "ParticleVFXManager: ParryParticle 프리팹이 지정되지 않았습니다."
            );

            return;
        }

        for (int i = 0; i < _initialParryParticlePoolSize; i++)
        {
            _parryParticlePool.Enqueue(
                CreateParryParticleInstance()
            );
        }
    }

    private ParticleVFXController CreateParryParticleInstance()
    {
        Transform parent = _particleVFXPoolRoot != null
            ? _particleVFXPoolRoot
            : transform;

        ParticleVFXController newParticle =
            Instantiate(_parryParticlePrefab, parent);

        newParticle.Initialize(ReturnParryParticle);
        newParticle.gameObject.SetActive(false);

        return newParticle;
    }

    public void PlayParryParticle(
    IAttacker attacker,
    Vector3 parryPoint,
    Vector3 impactDirection)
    {
        if (attacker == null || _parryParticlePrefab == null)
        {
            return;
        }

        Vector3 physicalDirection =
            impactDirection.sqrMagnitude > 0.0001f
                ? -impactDirection.normalized
                : Vector3.forward;

        Vector3 cameraDirection = physicalDirection;
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 directionToCamera =
                mainCamera.transform.position - parryPoint;

            if (directionToCamera.sqrMagnitude > 0.0001f)
            {
                cameraDirection = directionToCamera.normalized;
            }
        }

        Vector3 parryDirection = Vector3.Lerp(
            physicalDirection,
            cameraDirection,
            _parryCameraDirectionWeight
        ).normalized;

        Vector3 spawnPosition =
            parryPoint +
            cameraDirection * _parryCameraOffset;

        ParticleVFXController particle =
            _parryParticlePool.Count > 0
                ? _parryParticlePool.Dequeue()
                : CreateParryParticleInstance();

        particle.gameObject.SetActive(true);
        particle.Play(spawnPosition, parryDirection);
    }

    private void ReturnParryParticle(
    ParticleVFXController particle)
    {
        if (particle == null) return;
        if (_parryParticlePool.Contains(particle)) return;

        particle.gameObject.SetActive(false);

        Transform parent = _particleVFXPoolRoot != null
            ? _particleVFXPoolRoot
            : transform;

        particle.transform.SetParent(parent);
        _parryParticlePool.Enqueue(particle);
    }

    #endregion


    #region [Hit Reaction VFX 영역]

    /// <summary>
    /// 피격자가 소유한 HitReactionVFX 재생
    /// </summary>
    public void PlayHitReactionVFX(
        Enemy target,
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        if (target == null || target.VFX == null)
        {
            return;
        }

        ParticleVFXController prefab =
            target.VFX.HitReactionVFXPrefab;

        if (prefab == null)
        {
            return;
        }

        Queue<ParticleVFXController> pool =
            GetOrCreateHitReactionVFXPool(prefab);

        ParticleVFXController particle =
            pool.Count > 0
                ? pool.Dequeue()
                : CreateHitReactionVFXInstance(prefab);

        Vector3 reactionDirection =
            impactDirection.sqrMagnitude > 0.0001f
                ? -impactDirection.normalized
                : target.transform.forward;

        particle.gameObject.SetActive(true);

        particle.Play(
            impactPoint,
            reactionDirection
        );
    }

    /// <summary>
    /// HitReactionVFX 프리팹별 Pool 반환
    /// </summary>
    private Queue<ParticleVFXController>
        GetOrCreateHitReactionVFXPool(
            ParticleVFXController prefab)
    {
        if (_hitReactionVFXPools.TryGetValue(
            prefab,
            out Queue<ParticleVFXController> pool))
        {
            return pool;
        }

        pool = new Queue<ParticleVFXController>();

        _hitReactionVFXPools.Add(
            prefab,
            pool
        );

        for (int i = 0;
             i < _initialHitReactionVFXPoolSize;
             i++)
        {
            pool.Enqueue(
                CreateHitReactionVFXInstance(prefab)
            );
        }

        return pool;
    }

    /// <summary>
    /// HitReactionVFX 프리팹 인스턴스 생성
    /// </summary>
    private ParticleVFXController
        CreateHitReactionVFXInstance(
            ParticleVFXController prefab)
    {
        Transform parent = _particleVFXPoolRoot != null
            ? _particleVFXPoolRoot
            : transform;

        ParticleVFXController newParticle =
            Instantiate(prefab, parent);

        newParticle.Initialize(
            particle =>
                ReturnHitReactionVFX(
                    prefab,
                    particle
                )
        );

        newParticle.gameObject.SetActive(false);

        return newParticle;
    }

    /// <summary>
    /// HitReactionVFX를 해당 프리팹의 Pool로 반환
    /// </summary>
    private void ReturnHitReactionVFX(
        ParticleVFXController prefab,
        ParticleVFXController particle)
    {
        if (prefab == null || particle == null)
        {
            return;
        }

        if (!_hitReactionVFXPools.TryGetValue(
            prefab,
            out Queue<ParticleVFXController> pool))
        {
            return;
        }

        if (pool.Contains(particle))
        {
            return;
        }

        particle.gameObject.SetActive(false);

        Transform parent = _particleVFXPoolRoot != null
            ? _particleVFXPoolRoot
            : transform;

        particle.transform.SetParent(parent);
        pool.Enqueue(particle);
    }

    #endregion

}
