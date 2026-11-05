using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 커맨드 한 칸의 준비 강조, 회피 쿨타임과 빠른 지원 초상화를 표시함.
/// </summary>
[DisallowMultipleComponent]
public class CombatCommandSlot : MonoBehaviour
{
    [Header("Command Graphics")]
    [SerializeField] private Image _icon;
    [SerializeField] private Image _highlightRing;
    [SerializeField] private Image _glow;

    [Header("Cooldown Graphics")]
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private Image _cooldownRing;
    [SerializeField] private TMP_Text _cooldownText;

    [Header("Assist Graphics")]
    [SerializeField] private GameObject _portraitMask;
    [SerializeField] private RawImage _portrait;
    [SerializeField] private TMP_Text _assistText;

    [Header("State Colors")]
    [SerializeField] private Color _normalColor = new Color(0.843f, 0.863f, 0.894f);
    [SerializeField] private Color _readyColor = new Color(0.682f, 0.486f, 1f);
    [SerializeField] private Color _unavailableColor = new Color(0.30f, 0.34f, 0.38f);

    [Header("Ready Glow")]
    [SerializeField, Range(0f, 1f)] private float _glowMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _glowMaxAlpha = 0.85f;
    [SerializeField, Min(0f)] private float _pulseSpeed = 0.8f;

    private bool _isReady;
    private bool _isAvailable = true;

    private void LateUpdate()
    {
        if (!_isReady || !_isAvailable || _glow == null) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pulseSpeed * Mathf.PI * 2f);
        Color glowColor = _readyColor;
        glowColor.a = Mathf.Lerp(_glowMinAlpha, _glowMaxAlpha, pulse);
        _glow.color = glowColor;
    }

    #region [커맨드 상태 표시]
    /// <summary>
    /// 자원 충족 여부에 따라 아이콘 색과 연속 강조 링을 갱신함.
    /// </summary>
    public void SetReady(bool isReady)
    {
        _isReady = isReady;
        RefreshColors();
    }

    /// <summary>
    /// 교대 대상 부재처럼 커맨드를 사용할 수 없는 상태를 표시함.
    /// </summary>
    public void SetAvailable(bool isAvailable)
    {
        _isAvailable = isAvailable;
        RefreshColors();
    }

    /// <summary>
    /// 기존 회피 타이머에서 받은 남은 시간과 비율을 표시함.
    /// </summary>
    public void SetCooldown(float remaining, float duration)
    {
        bool isCoolingDown = remaining > 0f && duration > 0f;
        if (_cooldownOverlay != null) _cooldownOverlay.gameObject.SetActive(isCoolingDown);
        if (_cooldownRing != null)
        {
            _cooldownRing.gameObject.SetActive(isCoolingDown);
            _cooldownRing.fillAmount = isCoolingDown ? Mathf.Clamp01(remaining / duration) : 0f;
        }
        if (_cooldownText != null)
        {
            _cooldownText.gameObject.SetActive(isCoolingDown);
            // 남은 시간이 양수인데 0.0으로 보이지 않도록 0.1초 단위로 올림함.
            if (isCoolingDown) _cooldownText.SetText("{0:1}", Mathf.Ceil(remaining * 10f) / 10f);
        }
    }

    /// <summary>
    /// 빠른 지원 대상을 표시하고 대상이 없으면 교대 아이콘으로 복귀함.
    /// </summary>
    public void SetAssistPortrait(Texture portrait, bool isAssistActive)
    {
        bool hasPortrait = isAssistActive && portrait != null;
        if (_portrait != null) _portrait.texture = hasPortrait ? portrait : null;
        if (_portraitMask != null) _portraitMask.SetActive(hasPortrait);
        if (_icon != null) _icon.gameObject.SetActive(!hasPortrait);
        if (_assistText != null) _assistText.gameObject.SetActive(isAssistActive);
    }

    /// <summary>
    /// Hero 교대와 HUD 해제 시 이전 캐릭터의 표시 상태를 지움.
    /// </summary>
    public void ResetPresentation()
    {
        _isReady = false;
        _isAvailable = true;
        SetCooldown(0f, 0f);
        SetAssistPortrait(null, false);
        RefreshColors();
    }

    /// <summary>
    /// 기본색과 준비 강조색을 아이콘 및 공용 효과 이미지에 적용함.
    /// </summary>
    private void RefreshColors()
    {
        bool showReady = _isReady && _isAvailable;
        Color iconColor = _isAvailable ? _normalColor : _unavailableColor;
        if (showReady) iconColor = _readyColor;
        if (_icon != null) _icon.color = iconColor;
        if (_highlightRing != null)
        {
            _highlightRing.color = _readyColor;
            _highlightRing.gameObject.SetActive(showReady);
        }
        if (_glow != null) _glow.gameObject.SetActive(showReady);
    }
    #endregion
}
