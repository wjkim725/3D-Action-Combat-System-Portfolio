using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 씬의 CombatSandBox 옵션과 테스트용 몬스터 소환을 관리함.
/// </summary>
public class CombatSandboxController : MonoBehaviour
{
    [Header("Party")]
    [SerializeField] private PartyStateManager _partyStateManager;

    [Header("Session")]
    [SerializeField] private GameSessionController _gameSessionController;

    [Header("UI")]
    [SerializeField] private Toggle _invincibilityToggle;
    [SerializeField] private Toggle _infiniteEnergyToggle;
    [SerializeField] private Toggle _infiniteDecibelToggle;
    [SerializeField] private Button _spawnMutantButton;
    [SerializeField] private Button _spawnBerserkerButton;
    [SerializeField] private Button _removeAllEnemiesButton;

    [System.Serializable]
    private class EnemyAIModeButton
    {
        public Button button;
        public RawImage background;
        public Text label;
        public GameObject checkmark;

        /// <summary>
        /// 모드 버튼의 표시 참조가 모두 연결되었는지 반환함.
        /// </summary>
        public bool HasReferences()
        {
            return button != null && background != null && label != null && checkmark != null;
        }
    }

    [Header("Enemy AI")]
    [SerializeField] private EnemyAIModeButton _normalModeButton;
    [SerializeField] private EnemyAIModeButton _passiveModeButton;
    [SerializeField] private EnemyAIModeButton _stationaryModeButton;
    [SerializeField] private Texture _selectedModeTexture;
    [SerializeField] private Texture _unselectedModeTexture;

    private EnemyAIMode _enemyAIMode = EnemyAIMode.Normal;

    [Header("Spawn")]
    [SerializeField, Min(1)] private int _maxEnemyCount = 6;
    [SerializeField] private Enemy _mutantPrefab;
    [SerializeField] private Enemy _berserkerPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private Transform _spawnedEnemiesRoot;
    [SerializeField] private LayerMask _spawnBlockingLayers = ~0;

    private Hero[] _partyMembers;
    private bool _isInitialized;
    private int _nextSpawnPointIndex;
    private int _lastRemovalFrame = -1;

    #region [초기화 및 이벤트 연결]

    private void Start()
    {
        if (_partyStateManager == null || _partyStateManager.PartyMembers == null ||
            _partyStateManager.PartyMembers.Length == 0 || _gameSessionController == null ||
            _invincibilityToggle == null || _infiniteEnergyToggle == null ||
            _infiniteDecibelToggle == null || _spawnMutantButton == null ||
            _spawnBerserkerButton == null || _removeAllEnemiesButton == null || _mutantPrefab == null ||
            _berserkerPrefab == null || _spawnedEnemiesRoot == null ||
            _spawnPoints == null || _spawnPoints.Length == 0 ||
            _normalModeButton == null || !_normalModeButton.HasReferences() ||
            _passiveModeButton == null || !_passiveModeButton.HasReferences() ||
            _stationaryModeButton == null || !_stationaryModeButton.HasReferences() ||
            _selectedModeTexture == null || _unselectedModeTexture == null)
        {
            Debug.LogError("CombatSandboxController: 파티, UI 및 소환 참조를 확인해야 함.", this);
            enabled = false;
            return;
        }

        _partyMembers = (Hero[])_partyStateManager.PartyMembers.Clone();
        _invincibilityToggle.onValueChanged.AddListener(SetInvincibility);
        _infiniteEnergyToggle.onValueChanged.AddListener(SetInfiniteEnergy);
        _infiniteDecibelToggle.onValueChanged.AddListener(SetInfiniteDecibel);
        _spawnMutantButton.onClick.AddListener(SpawnMutant);
        _spawnBerserkerButton.onClick.AddListener(SpawnBerserker);
        _removeAllEnemiesButton.onClick.AddListener(RemoveAllEnemies);
        _normalModeButton.button.onClick.AddListener(HandleNormalModeClicked);
        _passiveModeButton.button.onClick.AddListener(HandlePassiveModeClicked);
        _stationaryModeButton.button.onClick.AddListener(HandleStationaryModeClicked);
        _isInitialized = true;
        ResetOptions();
    }

    private void OnDisable()
    {
        if (_isInitialized) ResetOptions();
    }

    private void OnDestroy()
    {
        if (!_isInitialized) return;
        if (_invincibilityToggle != null) _invincibilityToggle.onValueChanged.RemoveListener(SetInvincibility);
        if (_infiniteEnergyToggle != null) _infiniteEnergyToggle.onValueChanged.RemoveListener(SetInfiniteEnergy);
        if (_infiniteDecibelToggle != null) _infiniteDecibelToggle.onValueChanged.RemoveListener(SetInfiniteDecibel);
        if (_spawnMutantButton != null) _spawnMutantButton.onClick.RemoveListener(SpawnMutant);
        if (_spawnBerserkerButton != null) _spawnBerserkerButton.onClick.RemoveListener(SpawnBerserker);
        if (_removeAllEnemiesButton != null) _removeAllEnemiesButton.onClick.RemoveListener(RemoveAllEnemies);
        if (_normalModeButton.button != null) _normalModeButton.button.onClick.RemoveListener(HandleNormalModeClicked);
        if (_passiveModeButton.button != null) _passiveModeButton.button.onClick.RemoveListener(HandlePassiveModeClicked);
        if (_stationaryModeButton.button != null) _stationaryModeButton.button.onClick.RemoveListener(HandleStationaryModeClicked);
    }

    /// <summary>
    /// 컨트롤러가 해제될 때 파티의 샌드박스 옵션과 UI 표시를 초기화함.
    /// </summary>
    private void ResetOptions()
    {
        foreach (Hero hero in _partyMembers)
        {
            if (hero == null) continue;
            hero.ActionRegistry.SetSandboxInvincible(false);
            hero.Status.SetSandboxInfiniteEnergy(false);
            hero.Status.SetSandboxInfiniteDecibel(false);
        }
        if (_invincibilityToggle != null) _invincibilityToggle.isOn = false;
        if (_infiniteEnergyToggle != null) _infiniteEnergyToggle.isOn = false;
        if (_infiniteDecibelToggle != null) _infiniteDecibelToggle.isOn = false;
        SetEnemyAIMode(EnemyAIMode.Normal);
    }

    #endregion

    #region [파티 옵션]

    /// <summary>
    /// 샌드박스 입력을 처리할 수 있는 세션인지 반환함.
    /// </summary>
    private bool CanExecute()
    {
        return _isInitialized && isActiveAndEnabled &&
            _gameSessionController != null && !_gameSessionController.IsTransitioning;
    }

    /// <summary>
    /// 파티 전체의 CombatSandBox 무적 여부를 설정함.
    /// </summary>
    private void SetInvincibility(bool isEnabled)
    {
        if (!CanExecute()) return;
        foreach (Hero hero in _partyMembers)
            if (hero != null) hero.ActionRegistry.SetSandboxInvincible(isEnabled);
    }

    /// <summary>
    /// 파티 전체의 CombatSandBox 에너지 무한 여부를 설정함.
    /// </summary>
    private void SetInfiniteEnergy(bool isEnabled)
    {
        if (!CanExecute()) return;
        foreach (Hero hero in _partyMembers)
            if (hero != null) hero.Status.SetSandboxInfiniteEnergy(isEnabled);
    }

    /// <summary>
    /// 파티 전체의 CombatSandBox 데시벨 무한 여부를 설정함.
    /// </summary>
    private void SetInfiniteDecibel(bool isEnabled)
    {
        if (!CanExecute()) return;
        foreach (Hero hero in _partyMembers)
            if (hero != null) hero.Status.SetSandboxInfiniteDecibel(isEnabled);
    }

    #endregion

    #region [몬스터 AI 옵션]

    /// <summary>
    /// 일반 전투 모드 버튼 입력을 처리함.
    /// </summary>
    private void HandleNormalModeClicked()
    {
        if (CanExecute()) SetEnemyAIMode(EnemyAIMode.Normal);
    }

    /// <summary>
    /// 이동 전용 모드 버튼 입력을 처리함.
    /// </summary>
    private void HandlePassiveModeClicked()
    {
        if (CanExecute()) SetEnemyAIMode(EnemyAIMode.Passive);
    }

    /// <summary>
    /// 제자리 대기 모드 버튼 입력을 처리함.
    /// </summary>
    private void HandleStationaryModeClicked()
    {
        if (CanExecute()) SetEnemyAIMode(EnemyAIMode.Stationary);
    }

    /// <summary>
    /// 현재 및 이후 소환 몬스터의 모드를 지정하고 선택 표시를 동기화함.
    /// 컨트롤러 비활성화 시 초기화 경로에서도 호출함.
    /// </summary>
    private void SetEnemyAIMode(EnemyAIMode mode)
    {
        _enemyAIMode = mode;
        if (_spawnedEnemiesRoot != null)
        {
            foreach (Enemy enemy in _spawnedEnemiesRoot.GetComponentsInChildren<Enemy>(true))
            {
                if (enemy.Status != null && enemy.Status.IsDead) continue;
                enemy.SetAIMode(mode);
            }
        }

        RefreshModeButton(_normalModeButton, mode == EnemyAIMode.Normal);
        RefreshModeButton(_passiveModeButton, mode == EnemyAIMode.Passive);
        RefreshModeButton(_stationaryModeButton, mode == EnemyAIMode.Stationary);
    }

    /// <summary>
    /// 선택 여부에 맞춰 버튼 배경, 글자색과 체크 표시를 갱신함.
    /// </summary>
    private void RefreshModeButton(EnemyAIModeButton modeButton, bool isSelected)
    {
        if (modeButton.background != null)
            modeButton.background.texture = isSelected ? _selectedModeTexture : _unselectedModeTexture;
        if (modeButton.label != null)
            modeButton.label.color = isSelected ? Color.black : Color.white;
        if (modeButton.checkmark != null) modeButton.checkmark.SetActive(isSelected);
    }

    #endregion

    #region [몬스터 소환]

    /// <summary>
    /// 뮤턴트 소환 버튼의 요청을 처리함.
    /// </summary>
    private void SpawnMutant()
    {
        TrySpawnEnemy(_mutantPrefab);
    }

    /// <summary>
    /// 버서커 소환 버튼의 요청을 처리함.
    /// </summary>
    private void SpawnBerserker()
    {
        TrySpawnEnemy(_berserkerPrefab);
    }

    /// <summary>
    /// 비어 있는 지정 지점에 현재 Hero를 바라보는 몬스터 한 마리를 생성함.
    /// </summary>
    private void TrySpawnEnemy(Enemy prefab)
    {
        if (!CanExecute() || prefab == null) return;
        if (_lastRemovalFrame == Time.frameCount) return;
        if (HasReachedEnemyLimit()) return;
        Hero currentHero = _partyStateManager.CurrentHero;
        if (currentHero == null || currentHero.Status.IsDead) return;
        CharacterController capsule = prefab.GetComponent<CharacterController>();
        if (capsule == null) return;

        // 일시정지 중 연속 생성해도 직전에 생성된 충돌체를 검사하도록 동기화함.
        Physics.SyncTransforms();
        for (int offset = 0; offset < _spawnPoints.Length; offset++)
        {
            int index = (_nextSpawnPointIndex + offset) % _spawnPoints.Length;
            Transform point = _spawnPoints[index];
            if (point == null) continue;
            Vector3 direction = currentHero.transform.position - point.position;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction) : point.rotation;
            Vector3 scale = prefab.transform.localScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
            Vector3 center = point.position + rotation * Vector3.Scale(capsule.center, scale);
            if (IsOccupiedBySpawnedEnemy(center, radius, height)) continue;
            Vector3 halfSegment = Vector3.up * (height * 0.5f - radius);
            if (Physics.CheckCapsule(center - halfSegment, center + halfSegment,
                radius, _spawnBlockingLayers, QueryTriggerInteraction.Ignore)) continue;

            Enemy spawned = Instantiate(prefab, point.position, rotation, _spawnedEnemiesRoot);
            spawned.SetAIMode(_enemyAIMode);
            spawned.gameObject.SetActive(true);
            LockOnManager lockOnManager = LockOnManager.Instance;
            if (lockOnManager != null && lockOnManager.MagneticTarget == null)
            {
                // 새 충돌체를 탐색에 반영하고 기존 소프트 록온 규칙으로 초기 타겟을 선택함.
                Physics.SyncTransforms();
                lockOnManager.RefreshSoftLockTarget();
            }
            _nextSpawnPointIndex = (index + 1) % _spawnPoints.Length;
            return;
        }
        Debug.LogWarning("CombatSandboxController: 비어 있는 소환 지점이 없습니다.", this);
    }

    /// <summary>
    /// 생성 직후 충돌체가 아직 활성화되지 않은 몬스터도 위치와 크기로 중복 소환을 차단함.
    /// </summary>
    private bool IsOccupiedBySpawnedEnemy(Vector3 center, float radius, float height)
    {
        foreach (Transform spawned in _spawnedEnemiesRoot)
        {
            CharacterController capsule = spawned.GetComponent<CharacterController>();
            if (capsule == null || !spawned.gameObject.activeInHierarchy) continue;
            Vector3 scale = spawned.lossyScale;
            Vector3 otherCenter = spawned.TransformPoint(capsule.center);
            float otherRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float otherHeight = Mathf.Max(capsule.height * Mathf.Abs(scale.y), otherRadius * 2f);
            if (Mathf.Abs(center.y - otherCenter.y) >= (height + otherHeight) * 0.5f) continue;
            Vector3 delta = center - otherCenter;
            delta.y = 0f;
            float clearance = radius + otherRadius;
            if (delta.sqrMagnitude < clearance * clearance) return true;
        }
        return false;
    }

    /// <summary>
    /// 소환 루트 아래의 기존 배치 몹과 소환 몹을 합산하여 최대 수 도달 여부를 반환함.
    /// </summary>
    private bool HasReachedEnemyLimit()
    {
        Enemy[] enemies = _spawnedEnemiesRoot.GetComponentsInChildren<Enemy>(true);
        return enemies.Length >= _maxEnemyCount;
    }

    /// <summary>
    /// 소환 루트는 유지하고 비활성 오브젝트를 포함한 모든 자식 계층을 제거함.
    /// </summary>
    private void RemoveAllEnemies()
    {
        if (!CanExecute() || _lastRemovalFrame == Time.frameCount) return;
        _lastRemovalFrame = Time.frameCount;
        LockOnManager lockOnManager = LockOnManager.Instance;
        if (lockOnManager != null && lockOnManager.MagneticTarget != null &&
            lockOnManager.MagneticTarget != _spawnedEnemiesRoot &&
            lockOnManager.MagneticTarget.IsChildOf(_spawnedEnemiesRoot))
        {
            // 삭제될 대상을 바라보는 하드 록온의 입력·카메라 상태를 제거 전에 정리함.
            lockOnManager.ClearLockOn();
        }
        for (int i = _spawnedEnemiesRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_spawnedEnemiesRoot.GetChild(i).gameObject);
        }
        _nextSpawnPointIndex = 0;
    }

    #endregion
}
