using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class EnemyVFX : MonoBehaviour
{
    [Header("Hit Impact VFX Data")]
    [SerializeField]
    private HitImpactVFXDataSO _hitImpactData;

    public HitImpactVFXDataSO HitImpactData =>
        _hitImpactData;

    [Header("Melee Impact Locator")]
    [SerializeField]
    private MeleeImpactLocator _meleeImpactLocator;

    [Header("Ground Impact")]
    [SerializeField]
    private Transform _groundImpactOrigin;

    [SerializeField]
    private LayerMask _environmentLayerMask;

    [SerializeField, Min(0f)]
    private float _groundRayStartOffset = 0.2f;

    [SerializeField, Min(0.01f)]
    private float _groundRayDistance = 2f;

    public bool TryGetMeleeImpactData(
        Collider targetCollider,
        out Vector3 point,
        out Vector3 direction)
    {
        point = default;
        direction = default;

        if (_meleeImpactLocator == null ||
            targetCollider == null)
        {
            return false;
        }

        if (!_meleeImpactLocator.TryGetImpactSurface(
            targetCollider,
            out point,
            out _))
        {
            return false;
        }

        direction =
            _meleeImpactLocator.GetImpactDirection(
                targetCollider
            );

        return true;
    }

    /// <summary>
    /// TODO : 해당 메서드를 통해 Enemy의 JumpAttack 패턴을 통해 바닥을 내려찍을때 바닥에 파괴되는 효과 추가
    /// </summary>
    public bool TryGetGroundImpactData(
        out Collider groundCollider,
        out Vector3 point,
        out Vector3 surfaceNormal,
        out Vector3 markDirection)
    {
        groundCollider = null;
        point = default;
        surfaceNormal = default;
        markDirection = default;

        if (_groundImpactOrigin == null)
        {
            return false;
        }

        Vector3 rayOrigin =
            _groundImpactOrigin.position +
            Vector3.up * _groundRayStartOffset;

        float rayDistance =
            _groundRayStartOffset +
            _groundRayDistance;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                _environmentLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        groundCollider = hit.collider;
        point = hit.point;
        surfaceNormal = hit.normal;

        markDirection =
            Vector3.ProjectOnPlane(
                transform.forward,
                surfaceNormal
            );

        if (markDirection.sqrMagnitude < 0.0001f)
        {
            markDirection =
                Vector3.ProjectOnPlane(
                    transform.right,
                    surfaceNormal
                );
        }

        if (markDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        markDirection.Normalize();

        return true;
    }
}