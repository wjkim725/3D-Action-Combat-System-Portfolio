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

    [Header("Global Cooldown Settings")]
    [SerializeField]
    private float _globalCooldownTime = 5f;
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

    private void InitializePatterns()
    {
        _attackPatterns.Clear();

        if (_enemy.PatternDataList == null ||  _enemy.PatternDataList.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: 등록된 EnemyPatternDataSO가 없습니다.");
            return;
        }

        // 현재는 priority를 사용하지 않고
        // Inspector에 등록한 순서대로 패턴을 검사
        foreach (EnemyPatternDataSO patternData in _enemy.PatternDataList)
        {
            if (patternData == null)
                continue;

            _attackPatterns.Add(new EnemyAttackPattern(patternData));
        }
    }


    public EnemyAttackPattern GetAttackPattern(float distanceToTarget)
    {
        if (IsGlobalCooldownActive)
            return null;

        foreach (EnemyAttackPattern pattern in _attackPatterns)
        {
            if (!pattern.IsInAttackRange(distanceToTarget))
                continue;

            if (!pattern.IsCooldownReady(Time.time))
                continue;

            return pattern;
        }

        return null;
    }

    /// <summary>
    /// 파라미터 패턴의 쿨타임 + 전역 쿨타임 초기화 메서드
    /// </summary>
    /// <param name="pattern">초기화할 패턴</param>
    public void MarkPatternExecuted(EnemyAttackPattern pattern)
    {
        if (pattern == null)
            return;

        pattern.MarkExecuted(Time.time);
        _lastGlobalExecutedTime = Time.time;
    }
}

