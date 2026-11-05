using UnityEngine;

// 초기 겹침 조회 및 Trigger 감지 후 Controller에 전달
public class ProjectileCollisionSensor : MonoBehaviour
{
    private ProjectileController _controller;
    private Collider _sensorCollider;

    #region [초기화 및 겹침 조회]

    /// <summary>
    /// 충돌 결과를 받을 Controller와 이 오브젝트의 판정 Collider를 연결한다.
    /// </summary>
    public void Initialize(ProjectileController controller)
    {
        _controller = controller;
        _sensorCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// 판정 Collider의 월드 경계 상자에 들어온 후보를 반환한다. 실제 겹침은 별도로 확인한다.
    /// </summary>
    public Collider[] GetOverlapCandidates()
    {
        Bounds bounds = _sensorCollider.bounds;
        return Physics.OverlapBox(
            bounds.center, bounds.extents, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Collide);
    }

    /// <summary>
    /// 실제 Collider 형상으로 겹침 여부와 이 Sensor를 밀어낼 월드 방향·거리를 계산한다.
    /// </summary>
    public bool TryGetPenetration(Collider other, out Vector3 direction, out float distance)
    {
        return Physics.ComputePenetration(
            _sensorCollider, _sensorCollider.transform.position, _sensorCollider.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out direction, out distance);
    }

    #endregion

    #region [충돌 이벤트 전달]

    /// <summary>
    /// 비행 중 발생한 Trigger 접촉을 Controller에 전달한다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (_controller == null)
        {
            return;
        }

        _controller.HandleCollision(other);
    }

    #endregion
}
