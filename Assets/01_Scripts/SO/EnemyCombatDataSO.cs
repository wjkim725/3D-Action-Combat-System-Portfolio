using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy 패턴이 실행할 연계 모션과
/// 각 모션의 타격 데이터를 관리하는 SO
/// </summary>
[CreateAssetMenu(
    fileName = "EnemyCombatData",
    menuName = "Scriptable Objects/EnemyCombatData")]
public class EnemyCombatDataSO : ScriptableObject
{
    /// <summary>
    /// 하나의 애니메이션 모션에서 발생하는 개별 타격 데이터
    /// Animation Event 호출 순서와 리스트 순서를 매칭한다.
    /// </summary>
    [Serializable]
    public struct HitData
    {
        [Tooltip("인스펙터 식별용 이름")]
        public string hitName;

        [Header("Damage")]
        public float damageMultiplier;

        [Header("Hit Reaction")]
        public HitType hitType;

        [Header("Environment Hit Mark")]
        [Tooltip("지면에 HitMark를 남기지 않는 타격은 비워둡니다.")]
        public HitMarkDataSO environmentHitMarkData;
    }

    /// <summary>
    /// 하나의 패턴 안에서 실행되는 개별 연계 모션
    /// </summary>
    [Serializable]
    public struct MotionSequenceData
    {
        [Header("Motion Info")]
        public string motionName;

        [Header("Perfect Assist")]
        [Tooltip("해당 모션이 패링 지원 판정을 허용하는지")]
        public bool isParryable;

        [Header("Physics & Range")]
        public float hitRadius;
        public float hitForwardOffset;

        [Header("Multi-Hit Registry")]
        [Tooltip("Animation Event의 Hit Frame 호출 순서와 매칭")]
        public List<HitData> hits;
    }

    [Header("Pattern Motion Sequences")]
    [Tooltip("하나의 패턴에서 순서대로 실행되는 연계 모션")]
    public List<MotionSequenceData> motionSequences;
}