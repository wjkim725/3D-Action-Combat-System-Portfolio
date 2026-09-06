using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 해당 Hero에 종속된 로컬 VFX 제어 컴포넌트
/// </summary>
public class HeroVFX : MonoBehaviour
{
    [Header("Hit Impact VFX Data")]
    [SerializeField] private HitImpactVFXDataSO _hitImpactData;
    public HitImpactVFXDataSO HitImpactData => _hitImpactData;

    [Header("Weapon Trail")]
    [SerializeField] private SwordTrailController _swordTrail;

    [Header("Melee Impact Locator")]
    [FormerlySerializedAs("_meleeWeaponImpactLocator")]
    [SerializeField]
    private MeleeImpactLocator _meleeImpactLocator;

    [Header("Parry Impact Anchor")]
    [SerializeField] private Transform _parryImpactAnchor;

    public void PlaySwordTrail()
    {
        if (_swordTrail == null) return;

        _swordTrail.Play();
    }

    public void StopSwordTrail()
    {
        if (_swordTrail == null) return;

        _swordTrail.Stop();
    }

    public void StopSwordTrailImmediate()
    {
        if (_swordTrail == null) return;

        _swordTrail.StopImmediate();
    }

    public bool TryGetMeleeImpactData(
        Collider targetCollider,
        out Vector3 point,
        out Vector3 direction,
        out Vector3 surfaceNormal,
        out Vector3 slashDirection)
    {
        point = default;
        direction = default;
        surfaceNormal = default;
        slashDirection = default;

        if (_meleeImpactLocator == null || targetCollider == null)
        {
            return false;
        }

        if (!_meleeImpactLocator.TryGetImpactSurface(
            targetCollider,
            out point,
            out surfaceNormal))
        {
            return false;
        }


        direction = _meleeImpactLocator.GetImpactDirection(targetCollider);

        slashDirection = _meleeImpactLocator.GetImpactMotionDirection(surfaceNormal);

        Debug.DrawRay(
            point,
            surfaceNormal * 1.0f,
            Color.green,
            0.5f
        );

        return true;
    }

    public bool TryGetParryImpactPoint(out Vector3 point)
    {
        point = default;

        if (_parryImpactAnchor == null)
        {
            return false;
        }

        point = _parryImpactAnchor.position;
        return true;
    }
}