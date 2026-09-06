using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WarningSignController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _effectPivot;
    [SerializeField] private RawImage _horizontalBeam;
    [SerializeField] private RawImage _verticalBeam;
    [SerializeField] private RawImage _coreFlash;
    [SerializeField] private RawImage _outerGlow;

    [Header("Position")]
    [SerializeField] private float _worldHeightOffset = 1.5f;

    [Header("Animation")]
    [SerializeField] private float _duration = 0.3f;

    [Header("Beam")]
    [SerializeField] private float _beamFadeInEnd = 0.02f;
    [SerializeField] private float _beamHoldEnd = 0.08f;
    [SerializeField] private float _beamFadeOutEnd = 0.18f;
    [SerializeField] private float _beamPeakIntensity = 2.5f;

    [Header("Core Flash")]
    [SerializeField] private float _coreFadeInEnd = 0.02f;
    [SerializeField] private float _coreHoldEnd = 0.06f;
    [SerializeField] private float _coreFadeOutEnd = 0.12f;
    [SerializeField] private float _corePeakIntensity = 4.5f;
    [SerializeField] private float _coreGlowPeakIntensity = 2.2f;
    [SerializeField] private float _coreStartScale = 0.75f;
    [SerializeField] private float _corePeakScale = 1.10f;
    [SerializeField] private float _coreEndScale = 1f;

    [Header("Outer Glow")]
    [SerializeField] private float _outerGlowFadeInEnd = 0.1f;
    [SerializeField] private float _outerGlowFadeOutStart = 0.2f;
    [SerializeField] private float _outerGlowPeakIntensity = 1.25f;
    [SerializeField] private float _outerGlowStartScale = 0.85f;
    [SerializeField] private float _outerGlowEndScale = 1.08f;

    [Header("Parry Color")]
    [SerializeField, ColorUsage(true, true)]
    private Color _parryBeamColor = new Color(1f, 0.72f, 0.02f, 1f);

    [SerializeField, ColorUsage(true, true)]
    private Color _parryCenterColor = new Color(1f, 0.92f, 0.45f, 1f);

    [SerializeField, ColorUsage(true, true)]
    private Color _parryGlowColor = new Color(1f, 0.62f, 0.02f, 1f);

    [Header("Dodge Color")]
    [SerializeField, ColorUsage(true, true)]
    private Color _dodgeBeamColor = new Color(0.95f, 0.04f, 0.01f, 1f);

    [SerializeField, ColorUsage(true, true)]
    private Color _dodgeCenterColor = new Color(1f, 0.32f, 0.20f, 1f);

    [SerializeField, ColorUsage(true, true)]
    private Color _dodgeGlowColor = new Color(0.75f, 0.015f, 0.005f, 1f);

    private RectTransform _canvasRect;
    private Canvas _canvas;
    private Camera _camera;
    private Material _beamMaterial;
    private Material _coreMaterial;
    private Material _outerGlowMaterial;
    private Transform _target;
    private Coroutine _playCoroutine;

    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    private static readonly int BeamColorID = Shader.PropertyToID("_BeamColor");
    private static readonly int CenterColorID = Shader.PropertyToID("_CenterColor");
    private static readonly int BeamIntensityID = Shader.PropertyToID("_BeamIntensity");
    private static readonly int CoreColorID = Shader.PropertyToID("_CoreColor");
    private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
    private static readonly int CoreIntensityID = Shader.PropertyToID("_CoreIntensity");
    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _canvasRect = _canvas != null ? _canvas.GetComponent<RectTransform>() : null;
        _camera = Camera.main;

        // RawImage 전용 Material Instance 생성
        if (_horizontalBeam != null && _horizontalBeam.material != null)
        {
            _beamMaterial = new Material(_horizontalBeam.material);
            _horizontalBeam.material = _beamMaterial;

            if (_verticalBeam != null)
                _verticalBeam.material = _beamMaterial;
        }

        if (_coreFlash != null && _coreFlash.material != null)
        {
            _coreMaterial = new Material(_coreFlash.material);
            _coreFlash.material = _coreMaterial;
        }

        if (_outerGlow != null && _outerGlow.material != null)
        {
            _outerGlowMaterial = new Material(_outerGlow.material);
            _outerGlow.material = _outerGlowMaterial;
        }

        SetVisible(false);
    }

    public void Play(Transform target, DefenseWindowType defenseType)
    {
        if (target == null ||
            _effectPivot == null ||
            _beamMaterial == null ||
            _coreMaterial == null ||
            _outerGlowMaterial == null)
        {
            return;
        }

        _target = target;

        if (_playCoroutine != null)
            StopCoroutine(_playCoroutine);

        ApplyWarningColor(defenseType);
        ResetVisualState();
        UpdateScreenPosition();
        SetVisible(true);

        _playCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;

            UpdateScreenPosition();
            UpdateBeam(elapsed);
            UpdateCoreFlash(elapsed);
            UpdateOuterGlow(elapsed);

            yield return null;
        }

        ResetVisualState();
        SetVisible(false);

        _target = null;
        _playCoroutine = null;
    }

    private void UpdateBeam(float elapsed)
    {
        float alpha = EvaluateFlash(
            elapsed,
            _beamFadeInEnd,
            _beamHoldEnd,
            _beamFadeOutEnd);

        _beamMaterial.SetFloat(AlphaID, alpha);
        _beamMaterial.SetFloat(
            BeamIntensityID,
            _beamPeakIntensity * alpha);
    }

    private void UpdateCoreFlash(float elapsed)
    {
        float alpha = EvaluateFlash(
            elapsed,
            _coreFadeInEnd,
            _coreHoldEnd,
            _coreFadeOutEnd);

        float scale;

        if (elapsed <= _coreFadeInEnd)
        {
            float t = Mathf.Clamp01(elapsed / _coreFadeInEnd);
            scale = Mathf.Lerp(
                _coreStartScale,
                _corePeakScale,
                Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            float t = Mathf.InverseLerp(
                _coreFadeInEnd,
                _coreFadeOutEnd,
                elapsed);

            scale = Mathf.Lerp(
                _corePeakScale,
                _coreEndScale,
                Mathf.SmoothStep(0f, 1f, t));
        }

        _coreMaterial.SetFloat(AlphaID, alpha);
        _coreMaterial.SetFloat(CoreIntensityID, _corePeakIntensity * alpha);
        _coreMaterial.SetFloat(GlowIntensityID, _coreGlowPeakIntensity * alpha);
        _coreFlash.rectTransform.localScale = Vector3.one * scale;
    }

    private void UpdateOuterGlow(float elapsed)
    {
        float alpha;

        if (elapsed <= _outerGlowFadeInEnd)
        {
            float t = Mathf.Clamp01(elapsed / _outerGlowFadeInEnd);
            alpha = Mathf.SmoothStep(0f, 1f, t);
        }
        else if (elapsed <= _outerGlowFadeOutStart)
        {
            alpha = 1f;
        }
        else
        {
            float t = Mathf.InverseLerp(
                _outerGlowFadeOutStart,
                _duration,
                elapsed);

            alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        float scaleT = Mathf.Clamp01(elapsed / _duration);
        float scale = Mathf.Lerp(
            _outerGlowStartScale,
            _outerGlowEndScale,
            Mathf.SmoothStep(0f, 1f, scaleT));

        _outerGlowMaterial.SetFloat(AlphaID, alpha);
        _outerGlowMaterial.SetFloat(
            GlowIntensityID,
            _outerGlowPeakIntensity * alpha);

        _outerGlow.rectTransform.localScale = Vector3.one * scale;
    }

    private float EvaluateFlash(
        float elapsed,
        float fadeInEnd,
        float holdEnd,
        float fadeOutEnd)
    {
        if (elapsed <= fadeInEnd)
        {
            float t = Mathf.Clamp01(elapsed / fadeInEnd);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        if (elapsed <= holdEnd)
            return 1f;

        if (elapsed >= fadeOutEnd)
            return 0f;

        float fadeT = Mathf.InverseLerp(
            holdEnd,
            fadeOutEnd,
            elapsed);

        return 1f - Mathf.SmoothStep(0f, 1f, fadeT);
    }

    private void ApplyWarningColor(DefenseWindowType defenseType)
    {
        bool isParry = defenseType == DefenseWindowType.DodgeAndParry;

        Color beamColor = isParry
            ? _parryBeamColor
            : _dodgeBeamColor;

        Color centerColor = isParry
            ? _parryCenterColor
            : _dodgeCenterColor;

        Color glowColor = isParry
            ? _parryGlowColor
            : _dodgeGlowColor;

        _beamMaterial.SetColor(BeamColorID, beamColor);
        _beamMaterial.SetColor(CenterColorID, centerColor);

        _coreMaterial.SetColor(CoreColorID, centerColor);
        _coreMaterial.SetColor(GlowColorID, glowColor);

        _outerGlowMaterial.SetColor(GlowColorID, glowColor);
    }

    private void ResetVisualState()
    {
        _beamMaterial?.SetFloat(AlphaID, 0f);
        _coreMaterial?.SetFloat(AlphaID, 0f);
        _outerGlowMaterial?.SetFloat(AlphaID, 0f);

        if (_coreFlash != null)
            _coreFlash.rectTransform.localScale =
                Vector3.one * _coreStartScale;

        if (_outerGlow != null)
            _outerGlow.rectTransform.localScale =
                Vector3.one * _outerGlowStartScale;
    }

    private void SetVisible(bool visible)
    {
        if (_horizontalBeam != null)
            _horizontalBeam.enabled = visible;

        if (_verticalBeam != null)
            _verticalBeam.enabled = visible;

        if (_coreFlash != null)
            _coreFlash.enabled = visible;

        if (_outerGlow != null)
            _outerGlow.enabled = visible;
    }

    private void UpdateScreenPosition()
    {
        if (_target == null ||
            _canvasRect == null ||
            _effectPivot == null)
        {
            return;
        }

        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            return;

        Vector3 worldPosition =
            _target.position +
            Vector3.up * _worldHeightOffset;

        Vector3 screenPosition =
            _camera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // Canvas 좌표 변환
        Camera canvasCamera =
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPosition,
            canvasCamera,
            out Vector2 localPoint))
        {
            _effectPivot.anchoredPosition = localPoint;
        }
    }

    private void OnDestroy()
    {
        if (_beamMaterial != null)
            Destroy(_beamMaterial);

        if (_coreMaterial != null)
            Destroy(_coreMaterial);

        if (_outerGlowMaterial != null)
            Destroy(_outerGlowMaterial);
    }
}