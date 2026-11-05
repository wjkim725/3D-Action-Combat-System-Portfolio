using UnityEngine;

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
