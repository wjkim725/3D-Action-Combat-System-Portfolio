using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hero 오브젝트가 아닌 Hero의 무기 오브젝트가 존재할경우 그 때 부착.
/// </summary>
public class HeroWeapon : MonoBehaviour
{
    public enum WeaponDisplayState { Hide, OutCombat, InCombat }

    private WeaponDisplayState _currentState = WeaponDisplayState.Hide;
    public WeaponDisplayState CurrentState => _currentState;
    public event System.Action<WeaponDisplayState> OnWeaponDisplayStateChanged;

    [SerializeField]
    private Renderer[] _idleWeaponRenderers;
    [SerializeField]
    private Renderer[] _combatWeaponRenderers;

    // 전체 무기들의 Renderer 들을 완전히 꺼야할 때 쓸 Renderer 리스트
    public List<Renderer> AllWeaponRenderers    
    {
        get
        {
            List<Renderer> total = new List<Renderer>();
            if (_idleWeaponRenderers != null) total.AddRange(_idleWeaponRenderers);
            if (_combatWeaponRenderers != null) total.AddRange(_combatWeaponRenderers);
            return total;
        }
    }

    /// <summary>
    /// 무기 표시를 적용한 뒤 실제 상태 변경을 구독자에게 알림.
    /// </summary>
    public void ChangeWeaponDisplayState(WeaponDisplayState newState)
    {
        bool hasChanged = _currentState != newState;
        _currentState = newState;
        ApplyVisibility();
        if (hasChanged) OnWeaponDisplayStateChanged?.Invoke(_currentState);
    }

    /// <summary>
    /// 전체 무기를 숨긴 뒤 현재 표시 상태에 해당하는 렌더러만 표시함.
    /// </summary>
    private void ApplyVisibility()
    {
        SetRenderersVisible(_idleWeaponRenderers, false);
        SetRenderersVisible(_combatWeaponRenderers, false);

        switch (_currentState)
        {
            case WeaponDisplayState.OutCombat:
                SetRenderersVisible(_idleWeaponRenderers, true);
                break;

            case WeaponDisplayState.InCombat:
                SetRenderersVisible(_combatWeaponRenderers, true);
                break;
        }
    }

    /// <summary>
    /// 지정된 렌더러 목록의 표시 여부를 설정함.
    /// </summary>
    private void SetRenderersVisible(Renderer[] renderers, bool isVisible)
    {
        if (renderers == null) return;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
                targetRenderer.enabled = isVisible;
        }
    }

}
