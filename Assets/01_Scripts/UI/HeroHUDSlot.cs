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

    public void ClearSlot()
    {
        if (_targetHero != null)
        {
            _targetHero.OnDeathStateChanged -= HandleDeathStateChanged;
        }

        _targetHero = null;
        _heroStatus = null;

        if (_portrait != null)
        {
            _portrait.texture = null;
        }

        gameObject.SetActive(false);
    }

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
            _decibelText.text = $"{(int)_heroStatus.CurrentDecibel}";
        }

        // TODO: Gimmick 데이터가 Hero에 추가되면 _gimmickBar 갱신

        // TODO: Hero별 초상화 데이터가 추가되면 _portrait.texture 갱신
        // -> 초상화는 BindHero 메서드 선에서 할당


    }

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
