using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 빠른 지원의 등장·유지·소비·만료 연출과 전용 머티리얼을 관리함.
/// </summary>
[DisallowMultipleComponent]
public class QuickAssistHUDView : MonoBehaviour
{
    [Header("Presentation References")]
    [SerializeField] private CanvasGroup _presentation;
    [SerializeField] private RectTransform _portraitRoot;
    [SerializeField] private RawImage _portrait;
    [SerializeField] private GameObject _idleIcon;
    [SerializeField] private RectTransform _bannerReveal;
    [SerializeField] private Image _ringEffect;
    [SerializeField] private Image _bannerEffect;
    [SerializeField] private Image _expansionRing;

    [Header("Motion Settings")]
    [SerializeField, Min(0.01f)] private float _openDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.18f;
    [SerializeField, Min(1f)] private float _openScale = 1.15f;
    [SerializeField, Min(1f)] private float _bannerWidth = 220f;

    [Header("Portrait Pulse")]
    [SerializeField, Range(1f, 1.2f)] private float _holdPulseScale = 1.08f;
    [SerializeField, Min(0.1f)] private float _holdPulseDuration = 0.7f;

    private enum PresentationPhase { Hidden, Opening, Holding, Closing }
    private PresentationPhase _phase;
    private Hero _assistHero;
    private float _phaseStartTime;
    private float _closeStartAlpha;
    private float _closeStartScale;
    private bool _consumed;
    private Material _ringSource;
    private Material _bannerSource;
    private Material _ringMaterial;
    private Material _bannerMaterial;
    private static readonly int EffectTimeProperty = Shader.PropertyToID("_EffectTime");
    private static readonly int FlashProperty = Shader.PropertyToID("_Flash");

    #region [초기화]
    private void Awake()
    {
        _ringSource = _ringEffect.material;
        _bannerSource = _bannerEffect.material;
        _ringMaterial = new Material(_ringSource) { name = _ringSource.name + " (Instance)" };
        _bannerMaterial = new Material(_bannerSource) { name = _bannerSource.name + " (Instance)" };
        _ringEffect.material = _ringMaterial;
        _bannerEffect.material = _bannerMaterial;
        ResetPresentation();
    }

    private void OnDisable()
    {
        ResetPresentation();
    }

    private void OnDestroy()
    {
        if (_ringEffect != null) _ringEffect.material = _ringSource;
        if (_bannerEffect != null) _bannerEffect.material = _bannerSource;
        if (_ringMaterial != null) Destroy(_ringMaterial);
        if (_bannerMaterial != null) Destroy(_bannerMaterial);
    }

    /// <summary>
    /// 숨김 및 재활성화 시 이전 초상화와 잔여 연출을 제거함.
    /// </summary>
    public void ResetPresentation()
    {
        _phase = PresentationPhase.Hidden;
        _assistHero = null;
        _consumed = false;
        _presentation.alpha = 0f;
        _presentation.gameObject.SetActive(false);
        _portrait.texture = null;
        _portraitRoot.localScale = Vector3.one;
        _idleIcon.SetActive(true);
    }
    #endregion

    #region [지원 상태 연결]
    /// <summary>
    /// 유효한 지원 대상이 새로 표시될 때만 등장 연출을 시작함.
    /// </summary>
    public void SetAssist(Hero hero)
    {
        if (hero == null)
        {
            if (_phase == PresentationPhase.Opening || _phase == PresentationPhase.Holding)
                ClosePresentation(QuickAssistCloseReason.ContextInvalidated);
            return;
        }
        if (_assistHero == hero && (_phase == PresentationPhase.Opening || _phase == PresentationPhase.Holding)) return;

        _assistHero = hero;
        _portrait.texture = hero.Portrait;
        _phase = PresentationPhase.Opening;
        _phaseStartTime = Time.unscaledTime;
        _consumed = false;
        _presentation.gameObject.SetActive(true);
        _presentation.alpha = 1f;
        _idleIcon.SetActive(false);
        RefreshMotion();
    }

    /// <summary>
    /// 교대 통지 이후 도착하는 소비 이벤트도 실행 플래시로 전환함.
    /// </summary>
    public void ClosePresentation(QuickAssistCloseReason reason)
    {
        if (reason == QuickAssistCloseReason.Replaced)
        {
            ResetPresentation();
            return;
        }
        if (_phase == PresentationPhase.Hidden) return;
        bool consumed = reason == QuickAssistCloseReason.Consumed;
        if (_phase == PresentationPhase.Closing && (!consumed || _consumed)) return;

        _closeStartAlpha = _presentation.alpha;
        _closeStartScale = _portraitRoot.localScale.x;
        _consumed = consumed;
        _phase = PresentationPhase.Closing;
        _phaseStartTime = Time.unscaledTime;
        RefreshMotion();
    }
    #endregion

    #region [모션 및 셰이더 갱신]
    private void LateUpdate()
    {
        RefreshMotion();
    }

    /// <summary>
    /// 비배율 시간으로 등장·초상화 반복 확대·소비 플래시·만료 페이드를 갱신함.
    /// </summary>
    private void RefreshMotion()
    {
        if (_phase == PresentationPhase.Hidden) return;
        _idleIcon.SetActive(false);
        float elapsed = Time.unscaledTime - _phaseStartTime;
        float flash = 0f;
        float expansion = 0f;
        if (_phase == PresentationPhase.Opening)
        {
            float progress = Mathf.Clamp01(elapsed / _openDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            _portraitRoot.localScale = Vector3.one * Mathf.Lerp(_openScale, 1f, eased);
            _bannerReveal.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _bannerWidth * eased);
            flash = (1f - progress) * (1f - progress);
            expansion = progress;
            if (progress >= 1f)
            {
                _phase = PresentationPhase.Holding;
                _phaseStartTime = Time.unscaledTime;
            }
        }
        else if (_phase == PresentationPhase.Holding)
        {
            // 원래 크기에서 시작하여 초상화와 테두리만 부드럽게 확대·축소함.
            float pulse = 0.5f - 0.5f * Mathf.Cos(elapsed / Mathf.Max(0.1f, _holdPulseDuration) * Mathf.PI * 2f);
            _portraitRoot.localScale = Vector3.one * Mathf.Lerp(1f, _holdPulseScale, pulse);
        }
        else if (_phase == PresentationPhase.Closing)
        {
            float progress = Mathf.Clamp01(elapsed / _closeDuration);
            _presentation.alpha = _closeStartAlpha * (1f - progress);
            _portraitRoot.localScale = Vector3.one * Mathf.Lerp(_closeStartScale, 1f, progress);
            flash = _consumed ? 1f - progress : 0f;
            if (progress >= 1f)
            {
                ResetPresentation();
                return;
            }
        }

        Color expansionColor = new Color(1f, 0.48f, 0.12f, 0f);
        if (_phase == PresentationPhase.Opening) expansionColor.a = (1f - expansion) * 0.75f;
        _expansionRing.color = expansionColor;
        _expansionRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, expansion);
        ApplyMaterial(_ringEffect, _ringMaterial, flash);
        ApplyMaterial(_bannerEffect, _bannerMaterial, flash);
    }

    /// <summary>
    /// 원본 인스턴스와 Mask 파생 머티리얼에 동일한 상태를 전달함.
    /// </summary>
    private void ApplyMaterial(Image image, Material instance, float flash)
    {
        if (instance == null) return;
        UpdateMaterial(instance, flash);
        Material rendering = image.materialForRendering;
        if (rendering != null && rendering != instance) UpdateMaterial(rendering, flash);
    }

    /// <summary>
    /// 전용 셰이더에 비배율 시간과 순간 플래시를 전달함.
    /// </summary>
    private void UpdateMaterial(Material material, float flash)
    {
        material.SetFloat(EffectTimeProperty, Time.unscaledTime);
        material.SetFloat(FlashProperty, flash);
    }
    #endregion
}
