using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 바의 개별 머티리얼을 관리하고 준비 상태의 맥동과 일시적인 플래시를 표시함.
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class UIBarEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [SerializeField] private bool _grayscaleWhenInactive;
    [SerializeField, Min(0.01f)] private float _flashDuration = 0.28f;

    private Image _image;
    private Material _sourceMaterial;
    private Material _materialInstance;
    private bool _ready;
    private float _flash;
    private float _flashStartTime;
    private Color _flashColor = Color.white;

    private static readonly int ReadyProperty = Shader.PropertyToID("_Ready");
    private static readonly int SaturationProperty = Shader.PropertyToID("_Saturation");
    private static readonly int FlashProperty = Shader.PropertyToID("_Flash");
    private static readonly int FlashColorProperty = Shader.PropertyToID("_FlashColor");
    private static readonly int EffectColorProperty = Shader.PropertyToID("_EffectColor");
    private static readonly int EffectTimeProperty = Shader.PropertyToID("_EffectTime");
    private static readonly int BarRectProperty = Shader.PropertyToID("_BarRect");

    private void Awake()
    {
        _image = GetComponent<Image>();
        _sourceMaterial = _image.material;
        if (_sourceMaterial == null || !_sourceMaterial.HasProperty(ReadyProperty)) return;

        _materialInstance = new Material(_sourceMaterial);
        _materialInstance.name = _sourceMaterial.name + " (Instance)";
        _image.material = _materialInstance;
        ApplyEffect();
    }

    private void LateUpdate()
    {
        if (_flash > 0f)
        {
            // 발생 프레임 이전의 긴 deltaTime으로 새 플래시가 즉시 소멸하지 않게 함.
            float elapsed = Time.unscaledTime - _flashStartTime;
            _flash = Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, _flashDuration));
        }
        ApplyEffect();
    }

    private void OnDisable()
    {
        _flash = 0f;
    }

    private void OnDestroy()
    {
        if (_materialInstance == null) return;
        if (_image != null) _image.material = _sourceMaterial;
        Destroy(_materialInstance);
    }

    #region [효과 상태]
    /// <summary>
    /// 준비 상태를 전환하고 새로 충족된 순간에만 강조 플래시를 재생함.
    /// </summary>
    public void SetReady(bool ready, bool playTransition)
    {
        if (ready && !_ready && playTransition && _materialInstance != null)
        {
            PlayFlash(_materialInstance.GetColor(EffectColorProperty));
        }
        _ready = ready;
    }

    /// <summary>
    /// 지정한 색상의 짧은 플래시를 시작함.
    /// </summary>
    public void PlayFlash(Color color)
    {
        _flashColor = color;
        _flash = 1f;
        _flashStartTime = Time.unscaledTime;
    }

    /// <summary>
    /// Hero 교대 시 이전 캐릭터의 준비 상태와 잔여 플래시를 초기화함.
    /// </summary>
    public void ResetEffect()
    {
        _ready = false;
        _flash = 0f;
    }
    #endregion

    #region [머티리얼 갱신]
    /// <summary>
    /// 개별 머티리얼과 Mask가 파생한 실제 렌더 머티리얼에 같은 효과 상태를 전달함.
    /// </summary>
    private void ApplyEffect()
    {
        if (_materialInstance == null) return;

        UpdateMaterial(_materialInstance);
        Material renderingMaterial = _image.materialForRendering;
        if (renderingMaterial != null && renderingMaterial != _materialInstance)
        {
            UpdateMaterial(renderingMaterial);
        }
    }

    /// <summary>
    /// 비배율 시간과 Rect 좌표를 전달하여 히트스톱 및 Sprite Atlas에서도 효과를 유지함.
    /// </summary>
    private void UpdateMaterial(Material material)
    {
        Rect rect = _image.rectTransform.rect;
        material.SetFloat(ReadyProperty, _ready ? 1f : 0f);
        material.SetFloat(SaturationProperty, _grayscaleWhenInactive && !_ready ? 0f : 1f);
        material.SetFloat(FlashProperty, _flash);
        material.SetColor(FlashColorProperty, _flashColor);
        material.SetFloat(EffectTimeProperty, Time.unscaledTime);
        material.SetVector(BarRectProperty, new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
    }
    #endregion
}
