using UnityEngine;

/// <summary>
/// Enemy AI가 공격 패턴을 선택할 때 필요한 조건과
/// 실행할 CombatData를 연결하는 SO
/// </summary>
[CreateAssetMenu(
    fileName = "EnemyPatternData",
    menuName = "Scriptable Objects/EnemyPatternData")]
public class EnemyPatternDataSO : ScriptableObject
{
    [Header("Pattern Info")]
    public string patternName;

    [Tooltip("해당 모션을 실행할 Animator Trigger")]
    public string animationTriggerName;

    [Header("패턴 우선순위")]
    // EnemyPattern - InitializePatterns 리스트 조립단계에서 활용
    [Tooltip("여러 패턴이 동시에 사용 가능할 때의 선택 우선순위. 낮을수록 우선")]
    public int priority;

    [Header("Selection Range")]
    [Min(0f)]
    public float minAttackRange;

    [Min(0f)]
    public float maxAttackRange;

    [Header("Cooldown")]
    [Min(0f)]
    public float cooldown;

    [Header("Combat Data")]
    public EnemyCombatDataSO combatData;
}