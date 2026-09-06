using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hero 오브젝트가 아닌 Hero의 무기 오브젝트가 존재할경우 그 때 부착.
/// </summary>
public class HeroWeapon : MonoBehaviour
{
    public enum WeaponDisplayState { Hide, OutCombat, InCombat }

    private WeaponDisplayState _currentState = WeaponDisplayState.Hide;

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
    /// WeaponDisplayState 상태 전환 메서드
    /// </summary>
    public void ChangeWeaponDisplayState(WeaponDisplayState newState)
    {
        _currentState = newState;
        ApplyVisibility();
    }

    /// <summary>
    /// 현재 WeaponDisplayState 상태에 맞춰 렌더러 On/Off
    /// </summary>
    private void ApplyVisibility()
    {
        // Combat State - CombatWeaponRenderers 활성화
        if (_combatWeaponRenderers != null)
        {
            bool isHandVisible = (_currentState == WeaponDisplayState.InCombat);
            foreach (var r in _combatWeaponRenderers) if (r != null) r.enabled = isHandVisible;
        }

        // Idle State - IdelWeaponRenderers 활성화
        if (_idleWeaponRenderers != null)
        {
            bool isBackVisible = (_currentState == WeaponDisplayState.OutCombat);
            foreach (var r in _idleWeaponRenderers) if (r != null) r.enabled = isBackVisible;
        }

        // Hide State - AllWeaponRenderes 비활성화
        if (_currentState == WeaponDisplayState.Hide)
        {
            foreach (var r in AllWeaponRenderers) if(r != null) r.enabled = false;
        }
    }

}
