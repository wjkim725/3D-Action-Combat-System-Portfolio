using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Hero))]
public class AreaHitDetector : MonoBehaviour
{
    [Header("Placement Ground")]
    [SerializeField] private LayerMask _groundLayerMask;
    [Tooltip("설치 후보 위치보다 위에서 지면 탐색을 시작할 높이")]
    [SerializeField, Min(0f)] private float _groundProbeStartHeight = 2f;
    [Tooltip("설치 후보 위치보다 아래로 탐색할 깊이")]
    [SerializeField, Min(0f)] private float _groundProbeDepth = 10f;

    private Hero _hero;

    #region [초기화]

    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }

    #endregion

    #region [설치형 공격 생성]

    /// <summary>
    /// 모션의 사거리와 지면 탐색으로 위치를 확정하고 전체 타격 일정을 가진 설치기를 생성함.
    /// </summary>
    public bool Place(
        HeroCombatDataSO.MotionSequenceData motionData,
        IReadOnlyList<CombatEventContext> hitContexts,
        IReadOnlyList<float> hitTimes)
    {
        if (motionData.placedAreaPrefab == null ||
            !IsFinite(motionData.placedAreaRange) || motionData.placedAreaRange < 0f ||
            !IsFinite(motionData.hitForwardOffset))
        {
            Debug.LogError($"[{name}] 설치형 프리팹 또는 설치 범위 설정이 올바르지 않습니다.", this);
            return false;
        }

        PlacedAreaPoolManager poolManager = PlacedAreaPoolManager.Instance;
        if (poolManager == null)
        {
            Debug.LogError($"[{name}] 씬에 PlacedAreaPoolManager가 없습니다.", this);
            return false;
        }

        Vector3 position = transform.position;
        if (motionData.hitForwardOffset != 0f)
        {
            position += transform.forward * motionData.hitForwardOffset;
            LockOnManager lockOnManager = LockOnManager.Instance;
            Transform target = lockOnManager != null ? lockOnManager.MagneticTarget : null;
            Enemy enemy = target != null ? target.GetComponentInParent<Enemy>() : null;

            // 후보만 제공받고 생성 순간의 공격자 기준 거리로 설치 가능 여부를 판단함.
            if (enemy != null && lockOnManager.IsValidTarget(enemy) &&
                Vector3.Distance(transform.position, target.position) <= motionData.placedAreaRange)
            {
                position = target.position;
            }
        }

        if (!TryResolveGroundPosition(position, out Vector3 groundPosition))
        {
            Debug.LogWarning($"[{name}] 설치 위치 아래에서 유효한 지면을 찾지 못했습니다.", this);
            return false;
        }

        position = groundPosition;
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        return poolManager.Place(
            motionData.placedAreaPrefab, position, Quaternion.LookRotation(direction),
            hitContexts, hitTimes, _hero.EnemyLayerMask);
    }

    /// <summary>
    /// 후보 위치의 수평 좌표를 유지하고 트리거를 제외한 지면 높이로 보정함.
    /// </summary>
    private bool TryResolveGroundPosition(Vector3 candidatePosition, out Vector3 groundPosition)
    {
        groundPosition = candidatePosition;
        float probeDistance = _groundProbeStartHeight + _groundProbeDepth;
        if (_groundLayerMask.value == 0 ||
            !IsFinite(_groundProbeStartHeight) || _groundProbeStartHeight < 0f ||
            !IsFinite(_groundProbeDepth) || _groundProbeDepth <= 0f ||
            !IsFinite(probeDistance))
        {
            Debug.LogError($"[{name}] 설치 지면 레이어 또는 탐색 거리 설정이 올바르지 않습니다.", this);
            return false;
        }

        Vector3 origin = candidatePosition + Vector3.up * _groundProbeStartHeight;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeDistance,
            _groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        groundPosition.y = hit.point.y;
        return true;
    }

    /// <summary>
    /// 설치 위치 계산에 사용할 값이 유한한지 확인함.
    /// </summary>
    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    #endregion

}
