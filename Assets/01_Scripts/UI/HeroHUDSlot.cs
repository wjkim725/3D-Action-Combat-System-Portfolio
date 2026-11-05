using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroHUDSlot : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private RawImage _portrait;
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private Slider _energySlider;
    [SerializeField] private Slider _gimmickBar;
    [SerializeField] private TextMeshProUGUI _decibelText;

    [Header("Party HUD Presentation")]
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private HeroMechanicResourceView _mechanicResourceView;
    [SerializeField] private Image _ultFill;
    [SerializeField] private GameObject _ultReadyIcon;

    [Header("Energy Threshold")]
    [SerializeField] private Image _energyFill;
    [SerializeField] private Image _energyThresholdTop;
    [SerializeField] private Image _energyThresholdBottom;
    [SerializeField] private Color _energyReadyColor = new Color(1f, 0.204f, 0.239f);
    [SerializeField] private Color _energyInsufficientColor = new Color(0.522f, 0.545f, 0.576f);

    [Header("Bar Effects")]
    [SerializeField] private UIBarEffect _hpEffect;
    [SerializeField] private UIBarEffect _energyEffect;
    [SerializeField] private UIBarEffect _ultEffect;
    [SerializeField] private Color _damageFlashColor = new Color(1f, 0.22f, 0.16f);
    [SerializeField] private Color _healFlashColor = new Color(0.75f, 1f, 0.8f);

    [Header("Decibel Presentation")]
    [SerializeField] private Color _decibelNormalColor = new Color(1f, 0.75f, 0.25f);
    [SerializeField] private Color _decibelReadyColor = new Color(1f, 0.92f, 0.55f);
    [SerializeField, Range(0f, 0.2f)] private float _ultIconPulseScale = 0.06f;
    private Vector3 _ultIconBaseScale = Vector3.one;
    private bool _hasVisualSnapshot;
    private float _previousHP;

    [Header("Target Status")]
    private Hero _targetHero;
    private HeroStatus _heroStatus;

    [Header("Portrait Material")]
    private Material _portraitMaterialInstance;

    // Shader 클래스 내장 캐싱함수를 통해 프로퍼티 캐싱
    private static readonly int SaturationProperty = Shader.PropertyToID("_Saturation");
    private static readonly int BrightnessProperty = Shader.PropertyToID("_Brightness");

    public Hero TargetHero => _targetHero;

    private void Awake()
    {
        InitializeSlider(_hpSlider);
        InitializeSlider(_energySlider);
        InitializeSlider(_gimmickBar);

        // 초상화의 material 인스턴스 생성 및 필드에 할당
        if (_portrait != null && _portrait.material != null)
        {
            _portraitMaterialInstance = new Material(_portrait.material);
            _portrait.material = _portraitMaterialInstance;
        }

        if (_ultReadyIcon != null) _ultIconBaseScale = _ultReadyIcon.transform.localScale;
    }

    private void OnDestroy()
    {
        if (_targetHero != null)
        {
            _targetHero.OnDeathStateChanged -= HandleDeathStateChanged;
        }

        // material 인스턴스 라이프 사이클 관리 과정 필요
        if (_portraitMaterialInstance != null)
        {
            Destroy(_portraitMaterialInstance);
        }
    }

    private void Update()
    {
        RefreshStatusUI();
    }

    /// <summary>
    /// PartyHUDController에서 표시할 Hero를 전달받아 슬롯에 연결
    /// </summary>
    public void BindHero(Hero hero)
    {
        if (_targetHero != null)
        {
            _targetHero.OnDeathStateChanged -= HandleDeathStateChanged;
        }


        _targetHero = hero;
        _heroStatus = hero != null ? hero.Status : null;
        ResetVisualEffects();

        gameObject.SetActive(hero != null);

        // Hero에 접근해서 초상화 접근 및 할당
        if (_portrait != null)
        {
            _portrait.texture = hero != null ? hero.Portrait : null;
        }

        if (_targetHero != null)
        {
            _targetHero.OnDeathStateChanged += HandleDeathStateChanged;

            bool isDead = _targetHero.CurrentState == HeroState.Dead;
            RefreshPortraitState(isDead);
        }

        RefreshStatusUI();
    }

    /// <summary>
    /// Hero 연결과 이전 표시 효과를 해제하고 슬롯을 숨김.
    /// </summary>
    public void ClearSlot()
    {
        if (_targetHero != null)
        {
            _targetHero.OnDeathStateChanged -= HandleDeathStateChanged;
        }

        _targetHero = null;
        _heroStatus = null;
        ResetVisualEffects();

        if (_portrait != null)
        {
            _portrait.texture = null;
        }

        gameObject.SetActive(false);
    }

    #region [상태 UI 갱신]
    /// <summary>
    /// 연결된 Hero의 상태와 전용 자원을 슬롯에 표시함.
    /// </summary>
    private void RefreshStatusUI()
    {
        if (_heroStatus == null) return;

        if (_hpSlider != null)
        {
            _hpSlider.value = GetRatio(
                _heroStatus.CurrentHP,
                _heroStatus.MaxHP
            );
        }

        if (_energySlider != null)
        {
            _energySlider.value = GetRatio(
                _heroStatus.CurrentEnergy,
                _heroStatus.MaxEnergy
            );
        }

        if (_decibelText != null)
        {
            _decibelText.SetText("{0}<size=50%> PTS</size>", Mathf.FloorToInt(_heroStatus.CurrentDecibel));
            _decibelText.color = _heroStatus.HasEnoughDecibelForUlt ? _decibelReadyColor : _decibelNormalColor;
        }

        if (_hpText != null)
        {
            _hpText.SetText("{0} / {1}", Mathf.CeilToInt(_heroStatus.CurrentHP), Mathf.CeilToInt(_heroStatus.MaxHP));
        }
        if (_ultFill != null)
        {
            _ultFill.fillAmount = _heroStatus.RequiredDecibel <= 0f
                ? 1f : GetRatio(_heroStatus.CurrentDecibel, _heroStatus.RequiredDecibel);
        }
        bool ultReady = _heroStatus.HasEnoughDecibelForUlt;
        if (_ultReadyIcon != null)
        {
            _ultReadyIcon.SetActive(ultReady);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.8f * Mathf.PI * 2f);
            _ultReadyIcon.transform.localScale = _ultIconBaseScale * (ultReady ? 1f + pulse * _ultIconPulseScale : 1f);
        }
        _ultEffect?.SetReady(ultReady, _hasVisualSnapshot);

        if (_hasVisualSnapshot && !Mathf.Approximately(_previousHP, _heroStatus.CurrentHP))
        {
            _hpEffect?.PlayFlash(_heroStatus.CurrentHP < _previousHP ? _damageFlashColor : _healFlashColor);
        }

        if (_mechanicResourceView != null)
        {
            IHeroMechanic mechanic = _targetHero.Mechanic;
            _mechanicResourceView.RefreshResource(
                mechanic != null ? mechanic.ResourceType : HeroMechanicResourceType.None,
                mechanic != null ? mechanic.CurrentResource : 0f,
                mechanic != null ? mechanic.MaxResource : 0f);
        }
        RefreshEnergyThreshold();
        _previousHP = _heroStatus.CurrentHP;
        _hasVisualSnapshot = true;
    }

    /// <summary>
    /// 강특 요구치의 위치와 색상을 표시하고 에너지의 채도를 전환함.
    /// </summary>
    private void RefreshEnergyThreshold()
    {
        if (_energyFill == null) return;

        float energyRequirement = 0f;
        bool isReady = false;
        bool hasRequirement = _targetHero.SkillSystem != null &&
            _targetHero.SkillSystem.TryGetNextExSkillPreview(out energyRequirement, out isReady);
        float ratio = hasRequirement
            ? GetRatio(energyRequirement, _heroStatus.MaxEnergy) : 0f;
        Color color = isReady ? _energyReadyColor : _energyInsufficientColor;

        RefreshThresholdMarker(_energyThresholdTop, hasRequirement, ratio, color);
        RefreshThresholdMarker(_energyThresholdBottom, hasRequirement, ratio, color);
        _energyEffect?.SetReady(isReady, _hasVisualSnapshot);
    }

    /// <summary>
    /// 전체 에너지 바를 기준으로 삼각형의 가로 앵커와 색을 갱신함.
    /// </summary>
    private void RefreshThresholdMarker(Image marker, bool visible, float ratio, Color color)
    {
        if (marker == null) return;

        marker.gameObject.SetActive(visible);
        if (!visible) return;

        RectTransform rect = marker.rectTransform;
        rect.anchorMin = new Vector2(ratio, rect.anchorMin.y);
        rect.anchorMax = new Vector2(ratio, rect.anchorMax.y);
        rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        marker.color = color;
    }
    #endregion

    #region [효과 초기화]
    /// <summary>
    /// 교대나 슬롯 해제 시 이전 HP 비교값과 잔여 플래시를 지움.
    /// </summary>
    private void ResetVisualEffects()
    {
        _hasVisualSnapshot = false;
        _hpEffect?.ResetEffect();
        _energyEffect?.ResetEffect();
        _ultEffect?.ResetEffect();
        if (_ultReadyIcon != null) _ultReadyIcon.transform.localScale = _ultIconBaseScale;
    }
    #endregion

    private void InitializeSlider(Slider slider)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    private float GetRatio(float currentValue, float maxValue)
    {
        if (maxValue <= 0f) return 0f;

        return Mathf.Clamp01(currentValue / maxValue);
    }

    // Hero 사망/부활 이벤트 핸들러
    private void HandleDeathStateChanged(bool isDead)
    {
        RefreshPortraitState(isDead);
    }

    private void RefreshPortraitState(bool isDead)
    {
        if (_portraitMaterialInstance == null) return;

        _portraitMaterialInstance.SetFloat(
            SaturationProperty,
            isDead ? 0f : 1f
            );

        _portraitMaterialInstance.SetFloat(
            BrightnessProperty,
            isDead ? 0.4f : 1f
            );
    }
}
