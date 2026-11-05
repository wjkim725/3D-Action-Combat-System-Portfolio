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

    [Header("Utility Selection")]
    [Min(0f)]
    [Tooltip("패턴의 기본 선호도. 높을수록 선택 점수가 높아짐.")]
    public float basePreference = 1f;

    [Tooltip("X: 최소~최대 사거리를 0~1로 환산한 거리. Y: 거리 적합도 0~1.")]
    public AnimationCurve distanceUtilityCurve =
        AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Range(0f, 1f)]
    [Tooltip("직전에 종료된 패턴과 같을 때 곱하는 값. 1은 감점 없음, 0은 점수 0.")]
    public float repeatPenaltyMultiplier = 0.4f;

    [Header("Selection Range")]
    [Min(0f)]
    public float minAttackRange;

    [Min(0f)]
    public float maxAttackRange;

    [Header("Cooldown")]
    [Min(0f)]
    public float cooldown;

    [Header("Super Armor")]
    [Tooltip("해당 공격 패턴의 Attack 상태 동안 적용할 슈퍼아머 등급")]
    public HitReactionResistanceLevel superArmorLevel;

    [Header("Combat Data")]
    public EnemyCombatDataSO combatData;
}
