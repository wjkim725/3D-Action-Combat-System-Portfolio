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

    private Transform _mainCameraTransform;

    private ChainAttackManager _chainAttackManager;

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
        // 빌보드 로직: 캔버스가 항상 카메라를 똑바로 바라보게 만듭니다.
        if (_mainCameraTransform != null)
        {
            transform.rotation = _mainCameraTransform.rotation;
        }

        // 데이터 실시간 반영
        UpdateEnemyUI();
    }

    private void UpdateEnemyUI()
    {
        // 적 본체와 스탯 컴포넌트가 확실히 존재할 때만 로직 실행
        if (_enemy != null && _enemy.Status != null)
        {
            // 인스펙터 누락 시 에러 방지
            if (_groggyBonusDMG != null)
            {
                if (_enemy.Status is IEnemyStatus enemyStatus)
                {
                    if(_status.IsGroggy)
                    {
                        _groggyBonusDMG.text = $"{enemyStatus.GroggyDamageMultiplier}%";
                        _groggyBonusDMG.color = Color.red;
                    }
                    else
                    {
                        _groggyBonusDMG.text = "100%";
                        _groggyBonusDMG.color = Color.white;
                    }
                }
                else
                {
                    _groggyBonusDMG.text = "100%";
                }
            }

            // 적 체력 실시간 반영
            float maxHp = _enemy.Status.TotalMaxHP;
            _hpSlider.value = maxHp > 0f ? (_enemy.Status.CurrentHP / maxHp) : 0f;
        }

        // 적 그로기 게이지 및 연장 타이머 실시간 반영
        if (_groggy != null)
        {
            if (_status.IsGroggy)
            {
                _groggySlider.value = _groggy.GroggyTimeRatio;
            }
            else
            {
                float maxGroggy = _status.MaxGroggyGauge;
                _groggySlider.value = maxGroggy > 0f ? (_status.CurrentGroggyGauge / maxGroggy) : 0f;
            }
        }
    }

    private void HandleChainSelectionOpened()
    {
        SetEnemyBillboardActive(false);
    }

    private void HandleChainSelectionClosed()
    {
        SetEnemyBillboardActive(true);
    }

    private void SetEnemyBillboardActive(bool isActive)
    {
        if (_enemyBillboardRoot != null)
        {
            _enemyBillboardRoot.SetActive(isActive);
        }
    }
}