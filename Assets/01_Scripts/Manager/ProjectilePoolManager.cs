using System.Collections.Generic;
using UnityEngine;

// 씬 안에서 투사체 프리팹별 인스턴스 생성·대여·반환을 관리한다.
public class ProjectilePoolManager : MonoBehaviour
{
    public static ProjectilePoolManager Instance { get; private set; }

    [Header("Projectile Pool")]
    [SerializeField] private Transform _projectilePoolRoot;
    [SerializeField, Min(0)] private int _initialPoolSize = 10;

    private readonly Dictionary<ProjectileController, Queue<ProjectileController>> _pools = new();

    #region [초기화]

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _pools.Clear();
    }

    /// <summary>
    /// Launcher가 사용하는 프리팹의 풀을 최초 한 번만 준비한다.
    /// </summary>
    public void RegisterPrefab(ProjectileController prefab)
    {
        if (prefab == null) return;
        GetOrCreatePool(prefab);
    }

    #endregion

    #region [투사체 대여 및 반환]

    /// <summary>
    /// 대기 중인 투사체를 재사용하고 부족하면 추가 생성하여 발사한다.
    /// </summary>
    /// <returns>투사체 초기화가 발사를 승인하면 true를 반환한다.</returns>
    public bool Launch(
        ProjectileController prefab, Vector3 position, Quaternion rotation,
        CombatEventContext context, float launchSpeed, float lifeTime)
    {
        if (prefab == null) return false;

        Queue<ProjectileController> pool = GetOrCreatePool(prefab);
        ProjectileController projectile = null;
        while (pool.Count > 0 && projectile == null)
        {
            projectile = pool.Dequeue();
        }

        if (projectile == null) projectile = CreateInstance(prefab);

        projectile.transform.SetPositionAndRotation(position, rotation);
        projectile.transform.localScale = prefab.transform.localScale;

        // Initialize 내부에서 즉시 적중·반환될 수 있으므로 이후 활성화 처리를 하지 않는다.
        return projectile.Initialize(context, launchSpeed, lifeTime);
    }

    /// <summary>
    /// 프리팹별 대기열을 만들고 설정한 수만큼 투사체를 미리 생성한다.
    /// </summary>
    private Queue<ProjectileController> GetOrCreatePool(ProjectileController prefab)
    {
        if (_pools.TryGetValue(prefab, out Queue<ProjectileController> pool)) return pool;

        pool = new Queue<ProjectileController>();
        _pools.Add(prefab, pool);
        for (int i = 0; i < _initialPoolSize; i++)
        {
            pool.Enqueue(CreateInstance(prefab));
        }

        return pool;
    }

    /// <summary>
    /// 캐릭터와 독립된 풀 루트에 생성하고 최초 발사 전에 반환 경로를 연결한다.
    /// </summary>
    private ProjectileController CreateInstance(ProjectileController prefab)
    {
        Transform root = _projectilePoolRoot != null ? _projectilePoolRoot : transform;
        ProjectileController projectile = Instantiate(prefab, root);
        projectile.SetPoolReturnHandler(instance => ReturnProjectile(prefab, instance));
        projectile.ResetForPool();
        return projectile;
    }

    /// <summary>
    /// 종료된 투사체의 전투 정보를 비우고 해당 프리팹 대기열에 한 번만 반환한다.
    /// </summary>
    private void ReturnProjectile(ProjectileController prefab, ProjectileController projectile)
    {
        if (projectile == null || !_pools.TryGetValue(prefab, out Queue<ProjectileController> pool)) return;
        if (pool.Contains(projectile)) return;

        projectile.ResetForPool();
        Transform root = _projectilePoolRoot != null ? _projectilePoolRoot : transform;
        projectile.transform.SetParent(root, false);
        projectile.transform.localPosition = Vector3.zero;
        projectile.transform.localRotation = Quaternion.identity;
        projectile.transform.localScale = prefab.transform.localScale;
        pool.Enqueue(projectile);
    }

    #endregion
}
