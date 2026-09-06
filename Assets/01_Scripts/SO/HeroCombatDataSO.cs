using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HeroCombatData", menuName = "Scriptable Objects/HeroCombatData")]
public class HeroCombatDataSO : ScriptableObject
{
    // --- [ 1단계 : 단일 타격(Hit)에 대한 세부 명세서 ] ---
    [Serializable]
    public struct HitData
    {
        [Tooltip("인스펙터 식별용 서브 네임 (ex: 1타, 막타)")]
        public string hitName;

        [Header("Damage & Groggy")]
        public float damageMultiplier;          // 데미지 계수 (%)
        public float dazeMultiplier;            // 그로기 계수
        public HitType hitType;                 // 경직 타입 (Light, Heavy, Parry 등)

        [Header("Hit Mark")]
        public HitMarkDataSO hitMarkData;       // 타격 성공 시 적에게 적용될 VFX Data

        [Header("Groggy Extend")]
        public bool canExtendGroggy;            // 그로기 연장 가능 여부
        public float groggyExtendTime;          // 그로기 연장 시간

        [Header("Resource Payback")]
        public float payBackEnergy;             // 타격 성공시 획득 에너지
        public float payBackDecibel;            // 타격 성공시 획득 데시벨

        [Header("ChainAttack Available")]
        public bool canTriggerChainAttack;      // 콤보 스킬(ChainAttack) 발동 가능 여부
    }

    // --- [ 2단계 : 강화 특수 스킬 에너지 요구치 ] ---
    // 구조체로 담아서 List 로 관리하는 이유는 콤보 기믹이 있는 Hero를 위해서
    // ex) 1타는 40, 그 이후로 콤보로 20씩 소모
    [Serializable]
    public struct ExSkillData
    {
        [Header("Motion Info")]
        public string motionName;       // ex) 강특 1타, 강특 2타, 강특 막타

        [Header("Energy Requirment")]
        public float energyRequirement;  // Energy 요구치
    }
    
    // --- [ 3단계 : 하나의 연격 모션(Combo)이 가질 스펙 덩어리 ] ---
    [Serializable]
    public struct MotionSequenceData
    {
        [Header("Motion Info")]
        public string motionName;               // 식별용 이름 (ex: 평타 1타, 궁극기 난무)

        [Header("Physics & Range")]
        public float hitRadius;                 // 타격 범위 반지름
        public float hitForwardOffset;          // 캐릭터 중심 기준 전방 오프셋

        [Header("Movement")]
        public float magneticSpeedMultiplier;   // 공격 시 돌진/이동 거리 배율

        [Header("Multi-Hit Registry")]
        [Tooltip("이 애니메이션 안에서 터질 다단히트 데이터 리스트 (에디터 이벤트 개수와 매칭)")]
        public List<HitData> hits;              // 다단히트 구조체 리스트
    }


    // =========================================================================
    // 데이터 필드 영역 : 모든 공격 형태를 통일된 규격으로 관리
    // =========================================================================

    [Header("Normal Attack (평타 연속기)")]
    [Tooltip("연속 콤보가 존재하므로 리스트에 0타, 1타, 2타 순서대로 등록합니다.")]
    public List<MotionSequenceData> normalAttackSequences;

    [Header("Charge Attack (차지 공격)")]
    [Tooltip("단발성일지라도 규격을 통일하여 List[0]에 등록해 사용합니다.")]
    public List<MotionSequenceData> chargeAttackSequences;

    [Header("Dash Attack (대시 공격)")]
    public List<MotionSequenceData> dashAttackSequences;

    [Header("Skill (E 스킬)")]
    public List<MotionSequenceData> skillAttackSequences;

    [Header("EX Skill (강화 E 스킬)")]
    [Tooltip("에너지가 있을 때 반복해서 발동되는 일반 강화 콤보 시퀀스")]
    public List<MotionSequenceData> exSkillAttackSequences;

    /// <summary>
    /// Maria 전용 데이터 : 강특 연타 -> 막타는 에너지 소모없는 고정 공격일 경우 필요한 데이터
    /// </summary>
    [Header("EX SKill Finisher (강화 E 스킬 막타)")]
    [Tooltip("에너지가 부족하거나 콤보가 끝날 때 발동되는 단발성 피니시 시퀀스")]
    public MotionSequenceData exSkillFinisherSequence;

    /// <summary>
    /// Maria 전용 데이터 : 강특 연타 -> 에너지 소모량이 바뀌는 경우 필요한 데이터
    /// </summary>
    [Header("EX Skill Special Fields (강화 E 스킬 특수 필드)")]
    [Tooltip("주의 : 강화 E 에너지 요구치에 대한 인지 필요(모션과 별개)")]
    public List<ExSkillData> exSkillData;
    
    [Header("Ultimate (Q 궁극기)")]
    public List<MotionSequenceData> ultAttackSequences;

    /// <summary>
    /// Parry 시퀀스는 HeroHitChecker 를 통해 사용되지않고
    /// CombatManager - ApplyParryAssistResult 메서드를 통해
    /// 계산되어 타겟에게만 수치 전달
    /// </summary>
    [Header("Parry (패링)")]
    public List<MotionSequenceData> parrySequences;

    /// <summary>
    /// ParryCounter 시퀀스는 기존의 HeroHitChecker 를 통해 물리검출 및 타격전달
    /// </summary>
    [Header("ParryCounter (패링 반격)")]
    public List<MotionSequenceData> parryCounterSequences;

    [Header("DodgeCounter (회피 반격)")]
    public List<MotionSequenceData> dodgeCounterSequences;

    [Header("ChainAttack (콤보 스킬")]
    public List<MotionSequenceData> chainAttackSequences;

    [Header("QuickAssist (빠른 지원 공격)")]
    public List<MotionSequenceData> quickAssistSequences;

    /// <summary>
    /// 스크립트에서 초깃값 할당 - 인스펙터에서 수정 권장
    /// </summary>
    [Header("Ultimate Special Fields (궁극기 전용 특수 필드)")]
    public bool ultTeleportAvailable = true;       // 궁극기 시전 시 텔레포트 여부
    public float ultTeleportPosFromTarget = 9f;    // 타겟과의 고정 유지 거리

}