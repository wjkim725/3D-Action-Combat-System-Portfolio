using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyPatternDataSO 를 직접 사용하면
/// LastExecutedTime를 통한 런타임 데이터 관리가 불가능함.
/// 때문에 EnemyAttackPattern 를 거쳐서 몇가지 헬퍼메서드를 추가해서 사용.
/// </summary>
public class EnemyAttackPattern
{
    public EnemyPatternDataSO Data { get; private set; }

    // 적 개체마다 별도로 관리되어야 하는 런타임 데이터
    public float LastExecutedTime { get; private set; } = float.NegativeInfinity;

    public EnemyAttackPattern(EnemyPatternDataSO data)
    {
        Data = data;
    }

    public bool IsInAttackRange(float distanceToTarget)
    {
        return distanceToTarget >= Data.minAttackRange &&
               distanceToTarget <= Data.maxAttackRange;
    }

    public bool IsCooldownReady(float currentTime)
    {
        return currentTime >= LastExecutedTime + Data.cooldown;
    }

    /// <summary>
    /// 기본 선호도, 거리 적합도와 직전 패턴 반복 보정으로 선택 점수를 계산함.
    /// 사용 이력과 쿨타임은 변경하지 않음.
    /// </summary>
    public float EvaluateUtility(
        float distanceToTarget,
        bool isPreviousPattern)
    {
        float normalizedDistance = Mathf.InverseLerp(
            Data.minAttackRange,
            Data.maxAttackRange,
            distanceToTarget);

        float distanceUtility = 1f;

        if (Data.distanceUtilityCurve != null &&
            Data.distanceUtilityCurve.length > 0)
        {
            distanceUtility = Mathf.Clamp01(
                Data.distanceUtilityCurve.Evaluate(normalizedDistance));
        }

        float repeatMultiplier = isPreviousPattern
            ? Mathf.Clamp01(Data.repeatPenaltyMultiplier)
            : 1f;

        return Mathf.Max(0f, Data.basePreference) *
               distanceUtility *
               repeatMultiplier;
    }

    // LastExecutedTime 기록 메서드
    public void MarkExecuted(float currentTime)
    {
        LastExecutedTime = currentTime;
    }
}


public class EnemyPattern : MonoBehaviour
{
    private Enemy _enemy;

    private readonly List<EnemyAttackPattern> _attackPatterns = new();

    // 정상 완료 또는 중단으로 마지막에 종료된 패턴을 보관함.
    private EnemyAttackPattern _lastEndedPattern;

    // 가장 최근 종료된 패턴부터 최대 3회 사용 이력을 보관함.
    private readonly List<EnemyAttackPattern> _recentPatterns = new();
    private readonly List<EnemyAttackPattern> _candidatePatterns = new();
    private readonly List<float> _candidateWeights = new();

    private const int RecentPatternHistoryCount = 3;
    private const float SecondRecentPatternMultiplier = 0.65f;
    private const float ThirdRecentPatternMultiplier = 0.85f;

    [Header("Global Cooldown Settings")]
    [SerializeField]
    private float _globalCooldownTime = 3f;
    private float _lastGlobalExecutedTime = float.NegativeInfinity;

    // 패턴 간 전역 쿨타임 / 패턴 개별 쿨타임X
    public bool IsGlobalCooldownActive =>
        Time.time < _lastGlobalExecutedTime + _globalCooldownTime;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void Start()
    {
        InitializePatterns();
    }

    /// <summary>
    /// 등록된 데이터로 개체별 패턴을 구성하고 직전 패턴 정보를 초기화함.
    /// </summary>
    private void InitializePatterns()
    {
        _attackPatterns.Clear();
        _lastEndedPattern = null;
        _recentPatterns.Clear();
        _candidatePatterns.Clear();
        _candidateWeights.Clear();

        if (_enemy.PatternDataList == null ||  _enemy.PatternDataList.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: 등록된 EnemyPatternDataSO가 없습니다.");
            return;
        }

        // 등록된 패턴마다 독립적인 실행 이력을 구성함.
        foreach (EnemyPatternDataSO patternData in _enemy.PatternDataList)
        {
            if (patternData == null)
                continue;

            _attackPatterns.Add(new EnemyAttackPattern(patternData));
        }
    }


    #region [패턴 선택 및 사용 이력]

    /// <summary>
    /// 사거리와 쿨타임을 만족하는 패턴을 대상으로
    /// Utility 점수와 최근 사용 이력을 반영해 가중치 추첨함.
    /// </summary>
    public EnemyAttackPattern GetAttackPattern(float distanceToTarget)
    {
        if (IsGlobalCooldownActive)
            return null;

        _candidatePatterns.Clear();
        _candidateWeights.Clear();

        float currentTime = Time.time;
        float totalWeight = 0f;

        foreach (EnemyAttackPattern pattern in _attackPatterns)
        {
            if (!pattern.IsInAttackRange(distanceToTarget))
                continue;

            if (!pattern.IsCooldownReady(currentTime))
                continue;

            float weight = pattern.EvaluateUtility(
                distanceToTarget,
                pattern == _lastEndedPattern);

            weight *= GetRecentPatternMultiplier(pattern);

            if (weight <= 0f)
                continue;

            _candidatePatterns.Add(pattern);
            _candidateWeights.Add(weight);
            totalWeight += weight;
        }

        if (_candidatePatterns.Count == 0)
            return null;

        if (_candidatePatterns.Count == 1)
            return _candidatePatterns[0];

        float randomWeight = Random.value * totalWeight;

        for (int i = 0; i < _candidatePatterns.Count; i++)
        {
            randomWeight -= _candidateWeights[i];

            if (randomWeight < 0f)
                return _candidatePatterns[i];
        }

        // Random.value의 상한값과 부동소수점 오차를 보정함.
        return _candidatePatterns[_candidatePatterns.Count - 1];
    }

    /// <summary>
    /// 직전 패턴 감점은 EvaluateUtility에서 처리하며,
    /// 두 번째·세 번째 최근 사용에 대한 추가 배율을 반환함.
    /// </summary>
    private float GetRecentPatternMultiplier(EnemyAttackPattern pattern)
    {
        int historyIndex = _recentPatterns.IndexOf(pattern);

        switch (historyIndex)
        {
            case 1:
                return SecondRecentPatternMultiplier;
            case 2:
                return ThirdRecentPatternMultiplier;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// 패턴 종료 시각과 최근 사용 이력을 기록하여 개별·전역 쿨타임과 반복 보정에 반영함.
    /// 정상 완료와 중단 모두에 적용함.
    /// </summary>
    public void MarkPatternEnded(EnemyAttackPattern pattern)
    {
        if (pattern == null)
            return;

        float currentTime = Time.time;

        pattern.MarkExecuted(currentTime);
        _lastGlobalExecutedTime = currentTime;

        _lastEndedPattern = pattern;

        _recentPatterns.Insert(0, pattern);

        if (_recentPatterns.Count > RecentPatternHistoryCount)
        {
            _recentPatterns.RemoveAt(_recentPatterns.Count - 1);
        }
    }

    #endregion
}

