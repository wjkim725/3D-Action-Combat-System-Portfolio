using System.Collections.Generic;
using UnityEngine;

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
}