using UnityEngine;
using UnityEngine.Serialization;

public class MeleeImpactLocator : MonoBehaviour
{
    [Header("Impact Segment")]
    [FormerlySerializedAs("_weaponRoot")]
    [SerializeField] private Transform _impactRoot;

    [FormerlySerializedAs("_weaponTip")]
    [SerializeField] private Transform _impactTip;

    // 타격 시점의 공격 부위 위치 저장용 필드
    private Vector3 _previousImpactTipPosition;
    private bool _hasPreviousTipPosition;

    private void OnEnable()
    {
        CacheCurrentTipPosition();
    }

    private void LateUpdate()
    {
        CacheCurrentTipPosition();
    }

    private void CacheCurrentTipPosition()
    {
        if (_impactTip == null)
        {
            return;
        }

        _previousImpactTipPosition = _impactTip.position;

        _hasPreviousTipPosition = true;
    }

    /// <summary>
    /// 피격 지점과 표면 Normal 계산 메서드
    /// 공격 부위가 Collider 내부에 있더라도 외부에서 표면 방향으로
    /// Raycast하여 실제 진입 표면 탐색
    /// </summary>
    public bool TryGetImpactSurface(
        Collider targetCollider,
        out Vector3 impactPoint,
        out Vector3 surfaceNormal)
    {
        impactPoint = default;
        surfaceNormal = default;

        if (targetCollider == null ||
            _impactRoot == null ||
            _impactTip == null)
        {
            return false;
        }

        Vector3 impactSegmentPoint =
            GetClosestPointOnImpactSegment(targetCollider.bounds.center);

        Vector3 directionToCenter = targetCollider.bounds.center - impactSegmentPoint;

        if (directionToCenter.sqrMagnitude < 0.0001f)
        {
            directionToCenter = targetCollider.bounds.center - transform.position;

            if (directionToCenter.sqrMagnitude < 0.0001f)
            {
                directionToCenter = transform.forward;
            }
        }

        directionToCenter.Normalize();

        // Collider Bounds를 완전히 벗어나는 위치에서 Ray 시작
        float boundsRadius = targetCollider.bounds.extents.magnitude;

        float rayMargin = 0.1f;

        Vector3 rayOrigin =
            targetCollider.bounds.center -
            directionToCenter *
            (boundsRadius + rayMargin);

        float rayDistance =
            (boundsRadius + rayMargin) * 2f;

        Ray ray = new Ray(
            rayOrigin,
            directionToCenter
        );

        if (!targetCollider.Raycast(
                ray,
                out RaycastHit hit,
                rayDistance))
        {
            return false;
        }

        impactPoint = hit.point;
        surfaceNormal = hit.normal;

        return true;
    }

    /// <summary>
    /// 공격 부위에서 피격 대상을 향하는 타격 방향 계산
    /// </summary>
    public Vector3 GetImpactDirection(
        Collider targetCollider)
    {
        if (targetCollider == null ||
            _impactRoot == null ||
            _impactTip == null)
        {
            return transform.forward;
        }

        Vector3 impactSegmentPoint =
            GetClosestPointOnImpactSegment(
                targetCollider.bounds.center
            );

        Vector3 surfacePoint =
            targetCollider.ClosestPoint(
                impactSegmentPoint
            );

        Vector3 impactDirection =
            surfacePoint - impactSegmentPoint;

        if (impactDirection.sqrMagnitude < 0.0001f)
        {
            impactDirection =
                targetCollider.bounds.center -
                impactSegmentPoint;
        }

        if (impactDirection.sqrMagnitude < 0.0001f)
        {
            return transform.forward;
        }

        return impactDirection.normalized;
    }

    /// <summary>
    /// Root와 Tip으로 구성된 공격 구간에서
    /// 피격 대상과 가장 가까운 지점 계산
    /// </summary>
    private Vector3 GetClosestPointOnImpactSegment(
        Vector3 targetPosition)
    {
        Vector3 rootPosition =
            _impactRoot.position;

        Vector3 tipPosition =
            _impactTip.position;

        Vector3 impactSegment =
            tipPosition - rootPosition;

        float segmentLengthSqr =
            impactSegment.sqrMagnitude;

        if (segmentLengthSqr < 0.0001f)
        {
            return rootPosition;
        }

        float normalizedPosition =
            Vector3.Dot(
                targetPosition - rootPosition,
                impactSegment
            ) / segmentLengthSqr;

        normalizedPosition =
            Mathf.Clamp01(normalizedPosition);

        return rootPosition +
               impactSegment * normalizedPosition;
    }

    /// <summary>
    /// 이전 프레임과 현재 프레임의 Tip 위치를 사용하여
    /// 타격 부위의 진행 방향 계산
    /// </summary>
    public Vector3 GetImpactMotionDirection(
        Vector3 surfaceNormal)
    {
        if (!_hasPreviousTipPosition ||
            _impactRoot == null ||
            _impactTip == null)
        {
            return transform.right;
        }

        Vector3 motionDirection =
            _impactTip.position -
            _previousImpactTipPosition;

        // 피격 표면 위의 방향으로 변환
        motionDirection =
            Vector3.ProjectOnPlane(
                motionDirection,
                surfaceNormal
            );

        if (motionDirection.sqrMagnitude < 0.0001f)
        {
            Vector3 segmentDirection =
                _impactTip.position -
                _impactRoot.position;

            motionDirection =
                Vector3.ProjectOnPlane(
                    segmentDirection,
                    surfaceNormal
                );
        }

        if (motionDirection.sqrMagnitude < 0.0001f)
        {
            return transform.right;
        }

        return motionDirection.normalized;
    }
}