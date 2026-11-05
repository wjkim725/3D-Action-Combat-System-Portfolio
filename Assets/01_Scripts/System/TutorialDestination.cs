using UnityEngine;

public class TutorialDestination : MonoBehaviour
{
    [Header("Arrival Area")]
    [SerializeField, Min(0.1f)] private float _arrivalRadius = 1f;
    [SerializeField, Min(0f)] private float _heightTolerance = 1f;
    [SerializeField] private GameObject _markerVisual;

    #region [목적지 판정 및 표시]

    /// <summary>
    /// 바닥 기준 목적지 중심까지의 수평 거리를 반환함.
    /// </summary>
    public float GetPlanarDistance(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    /// <summary>
    /// 활성 Hero의 발 위치가 목적지 반경과 허용 높이 안에 있는지 검사함.
    /// </summary>
    public bool HasArrived(Hero hero)
    {
        if (!isActiveAndEnabled || hero == null || !hero.isActiveAndEnabled) return false;
        Vector3 position = hero.transform.position;
        return Mathf.Abs(position.y - transform.position.y) <= _heightTolerance &&
            GetPlanarDistance(position) <= _arrivalRadius;
    }

    /// <summary>
    /// 도착 판정 오브젝트를 유지하며 안내 마커의 표시만 전환함.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_markerVisual != null) _markerVisual.SetActive(visible);
    }

    #endregion
}
