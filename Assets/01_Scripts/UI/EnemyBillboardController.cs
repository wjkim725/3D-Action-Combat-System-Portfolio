using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyBillboardController : MonoBehaviour
{
    [Header("Enemy Billboard Root")]
    [SerializeField] private GameObject _enemyBillboardRoot;

    [Header("Target Enemy")]
    [SerializeField] private Enemy _enemy;
    [SerializeField] private EnemyGroggy _groggy;
    [SerializeField] private EnemyStatus _status;

    [Header("UI Components")]
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private Slider _groggySlider;
    [SerializeField] private TextMeshProUGUI _groggyBonusDMG;

    [Header("Billboard Presentation")]
    [SerializeField] private TextMeshProUGUI _groggyValue;
    [SerializeField] private Image _groggyFill;
    [SerializeField] private Image _hpDamageTrail;
    [SerializeField] private Sprite _normalGroggySprite;
    [SerializeField] private Sprite _activeGroggySprite;
    [SerializeField] private Sprite _spentGroggySprite;
    [SerializeField] private Color _normalGroggyColor = new Color(1f, 0.85f, 0.27f);
    [SerializeField] private Color _activeGroggyColor = new Color(0.13f, 0.84f, 0.94f);
    [SerializeField] private Color _spentGroggyColor = new Color(0.76f, 0.8f, 0.85f);
    [SerializeField] private Color _damageMultiplierColor = new Color(1f, 0.85f, 0.27f);

    [Header("HP Damage Trail")]
    [SerializeField, Min(0f)] private float _damageTrailDelay = 0.25f;
    [SerializeField, Min(0.01f)] private float _damageTrailSpeed = 0.65f;

    private bool _hasHPValue;
    private float _previousHPRatio;
    private float _damageTrailResumeTime;

    private Transform _mainCameraTransform;

    private ChainAttackManager _chainAttackManager;

    #region [생명주기 영역]

    private void OnEnable()
    {
        // 재활성화 직후의 체력은 피격으로 표현하지 않음.
        _hasHPValue = false;
    }

    private void Start()
    {
        // 메인 카메라의 위치 주소 캐싱
        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }

        if (_enemy == null)
        {
            _enemy = GetComponentInParent<Enemy>();
        }

        // Enemy의 인스턴스로 존재하는 EnemyGroggy에 접근
        if (_enemy != null)
        {
            if (_groggy == null) _groggy = _enemy.Groggy;
            if (_status == null) _status = _enemy.Status;
        }
        else
        {
            // Enemy를 못 찾았을 때를 대비한 최소한의 방어 코드
            if (_groggy == null) _groggy = GetComponentInParent<EnemyGroggy>();
            if (_status == null) _status = GetComponentInParent<EnemyStatus>();
        }

        // ChainAttack - Selecting 연출 시 EnemyBillboard 비활성화를 위한 이벤트 구독
        _chainAttackManager = ChainAttackManager.Instance;

        if (_chainAttackManager != null)
        {
            _chainAttackManager.OnSelectionOpened += HandleChainSelectionOpened;
            _chainAttackManager.OnSelectionClosed += HandleChainSelectionClosed;

            SetEnemyBillboardActive(!_chainAttackManager.IsSelectionOpen);
        }
    }

    private void OnDestroy()
    {
        if (_chainAttackManager != null)
        {
            _chainAttackManager.OnSelectionOpened -= HandleChainSelectionOpened;
            _chainAttackManager.OnSelectionClosed -= HandleChainSelectionClosed;
        }
    }

    private void Update()
    {
        UpdateEnemyUI();
    }

    private void LateUpdate()
    {
        if (_mainCameraTransform == null && Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }

        if (_mainCameraTransform != null)
        {
            transform.rotation = _mainCameraTransform.rotation;
        }
    }

    #endregion

    #region [표시 갱신 영역]

    /// <summary>
    /// 기존 Enemy 상태에서 체력, 그로기 수치와 상태별 외형을 갱신함.
    /// </summary>
    private void UpdateEnemyUI()
    {
        if (_status == null) return;

        float hpRatio = _status.TotalMaxHP > 0f
            ? Mathf.Clamp01(_status.CurrentHP / _status.TotalMaxHP)
            : 0f;
        if (_hpSlider != null) _hpSlider.SetValueWithoutNotify(hpRatio);
        UpdateDamageTrail(hpRatio);

        bool isGroggy = _status.IsGroggy;
        float groggyRatio = _status.MaxGroggyGauge > 0f
            ? _status.CurrentGroggyGauge / _status.MaxGroggyGauge
            : 0f;
        if (isGroggy)
        {
            groggyRatio = _groggy != null ? _groggy.GroggyTimeRatio : 0f;
        }

        groggyRatio = Mathf.Clamp01(groggyRatio);
        if (_groggySlider != null) _groggySlider.SetValueWithoutNotify(groggyRatio);

        Sprite groggySprite = _normalGroggySprite;
        Color groggyColor = _normalGroggyColor;
        if (isGroggy)
        {
            if (_groggy != null && _groggy.CanConsumeChainAttack)
            {
                groggySprite = _activeGroggySprite;
                groggyColor = _activeGroggyColor;
            }
            else
            {
                groggySprite = _spentGroggySprite;
                groggyColor = _spentGroggyColor;
            }
        }

        if (_groggyFill != null && groggySprite != null && _groggyFill.sprite != groggySprite)
        {
            _groggyFill.sprite = groggySprite;
        }
        if (_groggyValue != null)
        {
            _groggyValue.SetText("{0}%", Mathf.CeilToInt(groggyRatio * 100f));
            _groggyValue.color = groggyColor;
        }
        if (_groggyBonusDMG != null)
        {
            if (_groggyBonusDMG.gameObject.activeSelf != isGroggy)
            {
                _groggyBonusDMG.gameObject.SetActive(isGroggy);
            }
            if (isGroggy)
            {
                _groggyBonusDMG.SetText("DMG {0}%", _status.GroggyDamageMultiplier);
                _groggyBonusDMG.color = _damageMultiplierColor;
            }
        }
    }

    /// <summary>
    /// 피격 전 체력의 잔상을 잠시 유지한 후 현재 체력으로 감소시킴.
    /// </summary>
    private void UpdateDamageTrail(float hpRatio)
    {
        if (_hpDamageTrail == null) return;

        if (!_hasHPValue || hpRatio > _previousHPRatio)
        {
            _hpDamageTrail.fillAmount = hpRatio;
            _hasHPValue = true;
            _damageTrailResumeTime = 0f;
        }
        else if (hpRatio < _previousHPRatio)
        {
            _hpDamageTrail.fillAmount = Mathf.Max(_hpDamageTrail.fillAmount, _previousHPRatio);
            _damageTrailResumeTime = Time.time + _damageTrailDelay;
        }

        if (Time.time >= _damageTrailResumeTime)
        {
            _hpDamageTrail.fillAmount = Mathf.MoveTowards(
                _hpDamageTrail.fillAmount, hpRatio, _damageTrailSpeed * Time.deltaTime);
        }
        _previousHPRatio = hpRatio;
    }

    #endregion

    #region [체인 선택 표시 영역]

    /// <summary>
    /// 체인 선택 중 적 UI를 숨김.
    /// </summary>
    private void HandleChainSelectionOpened()
    {
        SetEnemyBillboardActive(false);
    }

    /// <summary>
    /// 체인 선택 종료 시 적 UI를 복원함.
    /// </summary>
    private void HandleChainSelectionClosed()
    {
        SetEnemyBillboardActive(true);
    }

    /// <summary>
    /// 컨트롤러를 유지한 채 표시 루트의 활성 상태만 변경함.
    /// </summary>
    private void SetEnemyBillboardActive(bool isActive)
    {
        if (_enemyBillboardRoot != null)
        {
            _enemyBillboardRoot.SetActive(isActive);
        }
    }

    #endregion
}
