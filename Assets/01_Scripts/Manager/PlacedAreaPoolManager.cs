using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 설치형 공격 프리팹별 인스턴스 생성과 대여 및 반환을 관리함.
/// </summary>
public class PlacedAreaPoolManager : MonoBehaviour
{
    public static PlacedAreaPoolManager Instance { get; private set; }

    [Header("Placed Area Pool")]
    [SerializeField] private Transform _placedAreaPoolRoot;
    [SerializeField, Min(0)] private int _initialPoolSize = 10;

    [Header("Ultimate Prewarm")]
    [FormerlySerializedAs("_initialUltimatePoolSize")]
    [SerializeField, Min(0)] private int _ultimatePrewarmCount = 2;
    private PartyStateManager _partyStateManager;

    private readonly Dictionary<PlacedAreaController, Queue<PlacedAreaController>> _placedAreaPools = new();

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

    private void Start()
    {
        _partyStateManager = PartyStateManager.Instance;
        if (_partyStateManager == null) return;

        _partyStateManager.OnPartyMembersChanged += PrewarmPartyUltimateAreas;
        PrewarmPartyUltimateAreas();
    }

    private void OnDestroy()
    {
        if (_partyStateManager != null)
            _partyStateManager.OnPartyMembersChanged -= PrewarmPartyUltimateAreas;
        if (Instance == this) Instance = null;
        _placedAreaPools.Clear();
    }

    /// <summary>
    /// 현재 파티의 궁극기 설치형 공격을 일반 풀 수량과 별도로 사전 생성함.
    /// </summary>
    private void PrewarmPartyUltimateAreas()
    {
        if (_partyStateManager == null || _partyStateManager.PartyMembers == null) return;
        foreach (Hero hero in _partyStateManager.PartyMembers)
        {
            if (hero == null || hero.CombatData == null || hero.CombatData.ultAttackSequences == null) continue;
            foreach (var sequence in hero.CombatData.ultAttackSequences)
            {
                if (sequence.placedAreaPrefab != null)
                    GetOrCreatePool(sequence.placedAreaPrefab, _ultimatePrewarmCount);
            }
        }
    }

    /// <summary>
    /// 사용할 프리팹의 풀을 최초 한 번 준비함.
    /// </summary>
    public void RegisterPrefab(PlacedAreaController prefab)
    {
        if (prefab != null) GetOrCreatePool(prefab);
    }

    #endregion

    #region [설치형 공격 대여 및 반환]

    /// <summary>
    /// 확정한 위치와 방향에 공격을 설치하며 실제 적중이 아닌 설치 성공 여부를 반환함.
    /// </summary>
    public bool Place(
        PlacedAreaController prefab, Vector3 position, Quaternion rotation,
        IReadOnlyList<CombatEventContext> hitContexts, IReadOnlyList<float> hitTimes,
        LayerMask targetLayerMask)
    {
        if (prefab == null) return false;
        Queue<PlacedAreaController> pool = GetOrCreatePool(prefab);
        PlacedAreaController placedArea = null;
        while (pool.Count > 0 && placedArea == null)
        {
            placedArea = pool.Dequeue();
        }
        if (placedArea == null) placedArea = CreateInstance(prefab);

        placedArea.transform.SetPositionAndRotation(position, rotation);
        placedArea.transform.localScale = prefab.transform.localScale;
        if (placedArea.Initialize(hitContexts, hitTimes, targetLayerMask)) return true;

        // 잘못된 생성 요청에서도 대여한 객체를 풀에 돌려 누수를 방지함.
        ReturnPlacedArea(prefab, placedArea);
        return false;
    }

    /// <summary>
    /// 프리팹별 대기열을 만들고 설정한 수만큼 미리 생성함.
    /// </summary>
    private Queue<PlacedAreaController> GetOrCreatePool(PlacedAreaController prefab, int initialSize = -1)
    {
        if (_placedAreaPools.TryGetValue(prefab, out Queue<PlacedAreaController> pool)) return pool;
        pool = new Queue<PlacedAreaController>();
        _placedAreaPools.Add(prefab, pool);
        int count = initialSize >= 0 ? initialSize : _initialPoolSize;
        for (int i = 0; i < count; i++)
        {
            pool.Enqueue(CreateInstance(prefab));
        }
        return pool;
    }

    /// <summary>
    /// 캐릭터와 독립된 풀 루트에 생성하고 반환 경로를 연결함.
    /// </summary>
    private PlacedAreaController CreateInstance(PlacedAreaController prefab)
    {
        Transform root = _placedAreaPoolRoot != null ? _placedAreaPoolRoot : transform;
        PlacedAreaController placedArea = Instantiate(prefab, root);
        placedArea.SetPoolReturnHandler(instance => ReturnPlacedArea(prefab, instance));
        placedArea.ResetForPool();
        return placedArea;
    }

    /// <summary>
    /// 종료된 공격을 초기화하고 해당 프리팹 대기열에 한 번 반환함.
    /// </summary>
    private void ReturnPlacedArea(PlacedAreaController prefab, PlacedAreaController placedArea)
    {
        if (placedArea == null || !_placedAreaPools.TryGetValue(prefab, out Queue<PlacedAreaController> pool)) return;
        if (pool.Contains(placedArea)) return;

        placedArea.ResetForPool();
        Transform root = _placedAreaPoolRoot != null ? _placedAreaPoolRoot : transform;
        placedArea.transform.SetParent(root, false);
        placedArea.transform.localPosition = Vector3.zero;
        placedArea.transform.localRotation = Quaternion.identity;
        placedArea.transform.localScale = prefab.transform.localScale;
        pool.Enqueue(placedArea);
    }

    #endregion
}
