using System;
using System.Collections;
using UnityEngine;

public class TutorialController : MonoBehaviour
{
    public enum TutorialPhase
    {
        NotStarted,
        Movement,
        LockOn,
        BasicAttack,
        NormalSkill,
        EXSkill,
        Ult,
        SwapNext,
        SwapPrev,
        Dodge,
        PerfectDodge,
        ParryAssist,
        ChainAttack,
        Completed
    }

    [Header("Entry Presentation")]
    [SerializeField] private CombatEntryController _entryController;

    [Header("Managers")]
    [SerializeField] private PartyStateManager _partyStateManager;
    [SerializeField] private CombatManager _combatManager;
    [SerializeField] private LockOnManager _lockOnManager;

    [Header("Party Joining")]
    [SerializeField] private PartySwapManager _partySwapManager;
    [SerializeField] private Hero[] _joiningHeroes;
    private bool _isJoiningParty;

    [Header("Movement Step")]
    [SerializeField] private TutorialDestination _movementDestination;
    [SerializeField] private TutorialUI _tutorialUI;

    [Header("Completion")]
    [SerializeField] private TutorialCompleteUI _completionUI;

    [Header("Basic Attack Step")]
    [SerializeField] private Enemy _trainingEnemyPrefab;
    [SerializeField] private Transform _trainingEnemySpawnPoint;
    [SerializeField] private Transform _enemyRoot;

    [Header("Messages")]
    [SerializeField] private string _header = "조작 튜토리얼";
    [SerializeField] private string _movementTitle = "이동";
    [SerializeField, TextArea(2, 4)] private string _movementMessage =
        "W A S D 키를 눌러\n표시된 위치로 이동하세요.";
    [SerializeField] private string _lockOnTitle = "락온";
    [SerializeField, TextArea(2, 4)] private string _lockOnMessage =
        "휠 클릭으로 적을 락온하세요.\n해제 후 다시 락온하세요.";
    [SerializeField] private string _basicAttackTitle = "기본공격";
    [SerializeField, TextArea(2, 4)] private string _basicAttackMessage =
        "왼쪽 클릭을 연속으로 눌러\n기본공격 3타를 맞히세요.";
    [SerializeField] private string _normalSkillTitle = "일반스킬";
    [SerializeField, TextArea(2, 4)] private string _normalSkillMessage =
        "E키를 눌러 일반스킬을\n훈련용 적에게 맞히세요.";
    [SerializeField] private string _exSkillTitle = "강화 특수스킬";
    [SerializeField, TextArea(2, 4)] private string _exSkillMessage =
        "에너지가 충분하면 강특이 발동됩니다.\nE키로 훈련용 적에게 맞히세요.";
    [SerializeField] private string _ultTitle = "궁극기";
    [SerializeField, TextArea(2, 4)] private string _ultMessage =
        "Q키를 눌러 궁극기를\n훈련용 적에게 맞히세요.";
    [SerializeField] private string _swapNextTitle = "정방향 교대";
    [SerializeField, TextArea(2, 4)] private string _swapNextMessage =
        "Space키를 눌러\n정방향으로 3번 교대하세요.";
    [SerializeField] private string _swapPrevTitle = "역방향 교대";
    [SerializeField, TextArea(2, 4)] private string _swapPrevMessage =
        "C키를 눌러\n역방향으로 3번 교대하세요.";
    [SerializeField] private string _dodgeTitle = "회피";
    [SerializeField, TextArea(2, 4)] private string _dodgeMessage =
        "우클릭을 눌러\n회피를 3번 사용하세요.";
    [SerializeField] private string _perfectDodgeTitle = "극한회피";
    [SerializeField, TextArea(2, 4)] private string _perfectDodgeMessage =
        "적의 공격이 닿기 직전에\n우클릭으로 회피하세요.";
    [SerializeField] private string _parryAssistTitle = "패링지원";
    [SerializeField, TextArea(2, 4)] private string _parryAssistMessage =
        "노란 섬광에 맞춰 Space키로\n패링지원을 사용하세요.";
    [SerializeField] private string _chainAttackTitle = "콤보스킬";
    [SerializeField, TextArea(2, 4)] private string _chainAttackMessage =
        "적을 그로기로 만들고 강공격하세요.\n선택창에서 캐릭터를 선택하세요.";
    [SerializeField, TextArea(2, 4)] private string _chainAttackCompletedMessage =
        "콤보스킬 적중에 성공했습니다.";

    private Enemy _trainingEnemy;
    private int _lockOnSuccessCount;
    // 일반스킬 타격의 페이백까지 끝난 LateUpdate에서 강특 단계 진입을 확정함.
    private bool _isEXSkillStepPending;
    // 강특 타격의 페이백 이후 데시벨을 설정하도록 궁극기 단계 진입을 예약함.
    private bool _isUltStepPending;
    // 궁극기 적중 이후 합류 준비와 컷신·동작 종료를 기다리는 동안 유지함.
    private bool _isSwapNextStepPending;
    private int _swapNextSuccessCount;
    private int _swapPrevSuccessCount;
    private int _dodgeSuccessCount;
    private int _perfectDodgeSuccessCount;
    private int _parryAssistSuccessCount;
    // 패링 타격 처리 중 적을 제거하지 않도록 LateUpdate에서 콤보스킬 단계를 시작함.
    private bool _isChainAttackStepPending;
    private Hero _dodgeEventHero;

    public TutorialPhase CurrentPhase { get; private set; } = TutorialPhase.NotStarted;
    public event Action OnMovementStepStarted;
    public event Action OnMovementStepCompleted;
    public event Action OnLockOnStepCompleted;
    public event Action OnBasicAttackStepCompleted;
    public event Action OnNormalSkillStepCompleted;
    public event Action OnEXSkillStepCompleted;
    public event Action OnUltStepCompleted;
    public event Action OnSwapNextStepCompleted;
    public event Action OnSwapPrevStepCompleted;
    public event Action OnDodgeStepCompleted;
    public event Action OnPerfectDodgeStepCompleted;
    public event Action OnParryAssistStepCompleted;
    public event Action OnChainAttackStepCompleted;

    #region [초기화 및 갱신]

    private IEnumerator Start()
    {
        if (_movementDestination == null || _tutorialUI == null || _completionUI == null ||
            _trainingEnemyPrefab == null || _trainingEnemySpawnPoint == null || _enemyRoot == null)
        {
            Debug.LogError("TutorialController: 목적지, 안내·완료 UI와 훈련용 적 소환 참조를 확인해야 함.", this);
            enabled = false;
            yield break;
        }

        if (_partyStateManager == null) _partyStateManager = PartyStateManager.Instance;
        if (_partyStateManager == null)
        {
            Debug.LogError("TutorialController: 파티 매니저 참조를 확인해야 함.", this);
            enabled = false;
            yield break;
        }

        // 모든 Awake가 끝난 뒤 첫 안내 대기 프레임 전에 파티의 초기 에너지를 비움.
        _partyStateManager.SetPartyEnergyLockedAtZero(true);
        _movementDestination.SetVisible(false);
        _tutorialUI.Hide();
        _completionUI.gameObject.SetActive(false);
        if (_entryController == null ||
            !_entryController.TryBeginEntry(_partyStateManager.CurrentHero))
        {
            Debug.LogError("TutorialController: 입장 연출 시작에 실패함.", this);
            yield break;
        }
        while (_entryController.IsPlaying)
            yield return null;

        // 입장 종료와 파티 초기화 이후 첫 안내 시작 조건을 확인함.
        yield return null;
        while (CurrentPhase == TutorialPhase.NotStarted)
        {
            if (BeginTutorial()) yield break;
            yield return null;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (CurrentPhase == TutorialPhase.Completed)
        {
            TryShowCompletionUI();
            return;
        }

        if (_isSwapNextStepPending)
        {
            TryBeginSwapNextStep();
            return;
        }

        if (CurrentPhase == TutorialPhase.EXSkill) RefillEXSkillRetryEnergy();
        if (CurrentPhase == TutorialPhase.Ult) RefillUltRetryDecibel();

        if (CurrentPhase == TutorialPhase.LockOn || CurrentPhase == TutorialPhase.BasicAttack || CurrentPhase == TutorialPhase.NormalSkill ||
            CurrentPhase == TutorialPhase.EXSkill || CurrentPhase == TutorialPhase.Ult ||
            CurrentPhase == TutorialPhase.PerfectDodge || CurrentPhase == TutorialPhase.ParryAssist ||
            CurrentPhase == TutorialPhase.ChainAttack)
        {
            // 실패한 연습으로 사망한 적은 사망 모션과 Destroy를 마친 뒤 다시 소환함.
            // 기본공격 3타로 적을 처치해도 일반스킬 연습을 이어갈 수 있도록 재소환함.
            // 각 전투 연습 단계에서만 재소환하며 최종 완료 후에는 중단함.
            if (_trainingEnemy == null) SpawnTrainingEnemy();
            return;
        }

        if (CurrentPhase != TutorialPhase.Movement) return;
        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || hero.Status == null || hero.Status.IsDead) return;

        _tutorialUI.SetRemainingDistance(_movementDestination.GetPlanarDistance(hero.transform.position));
        if (_movementDestination.HasArrived(hero)) TryCompleteMovementStep(hero);
    }

    private void LateUpdate()
    {
        if ((!_isEXSkillStepPending && !_isUltStepPending && !_isChainAttackStepPending) || Time.timeScale <= 0f) return;
        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || hero.Status == null || hero.Status.IsDead) return;

        if (_isEXSkillStepPending)
        {
            _isEXSkillStepPending = false;
            _partyStateManager.SetPartyEnergyLockedAtZero(false);
            hero.Status.ModifyEnergy(60f - hero.Status.CurrentEnergy);
            CurrentPhase = TutorialPhase.EXSkill;
            RefreshStepUI();
            OnNormalSkillStepCompleted?.Invoke();
        }
        else if (_isUltStepPending)
        {
            _isUltStepPending = false;
            hero.Status.ModifyDecibel(3000f - hero.Status.CurrentDecibel);
            CurrentPhase = TutorialPhase.Ult;
            RefreshStepUI();
            OnEXSkillStepCompleted?.Invoke();
        }
        else if (_isChainAttackStepPending)
        {
            _isChainAttackStepPending = false;
            BeginChainAttackStep();
            OnParryAssistStepCompleted?.Invoke();
        }
    }

    private void OnDisable()
    {
        if (_lockOnManager != null) _lockOnManager.OnHardLockActivated -= HandleHardLockActivated;
        if (_partyStateManager != null) _partyStateManager.OnCurrentHeroChanged -= BindDodgeHero;
        BindDodgeHero(null);
        if (_partyStateManager != null) _partyStateManager.SetPartyEnergyLockedAtZero(false);
        if (_combatManager != null) _combatManager.OnHitApplied -= HandleHitApplied;
        if (_combatManager != null) _combatManager.OnPerfectDodgeSucceeded -= HandlePerfectDodgeSucceeded;
        if (_combatManager != null) _combatManager.OnParryAssistSucceeded -= HandleParryAssistSucceeded;
        if (_partySwapManager != null) _partySwapManager.OnSwapExecuted -= HandleSwapNextExecuted;
        if (_partySwapManager != null) _partySwapManager.OnSwapExecuted -= HandleSwapPrevExecuted;
        if (_movementDestination != null) _movementDestination.SetVisible(false);
        if (_tutorialUI != null) _tutorialUI.Hide();
        if (_completionUI != null) _completionUI.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (CurrentPhase == TutorialPhase.NotStarted) return;
        if (_partyStateManager != null)
            _partyStateManager.SetPartyEnergyLockedAtZero(
                CurrentPhase == TutorialPhase.Movement || CurrentPhase == TutorialPhase.LockOn ||
                CurrentPhase == TutorialPhase.BasicAttack ||
                CurrentPhase == TutorialPhase.NormalSkill);
        SubscribeTutorialEvents();
        if (_isSwapNextStepPending) JoinTutorialParty();
        RefreshStepUI();
    }

    /// <summary>
    /// 실제 타격 및 교대 성공 이벤트를 중복 없이 구독함.
    /// </summary>
    private void SubscribeTutorialEvents()
    {
        if (_lockOnManager == null) _lockOnManager = LockOnManager.Instance;
        if (_lockOnManager != null)
        {
            _lockOnManager.OnHardLockActivated -= HandleHardLockActivated;
            _lockOnManager.OnHardLockActivated += HandleHardLockActivated;
        }
        if (_partyStateManager != null)
        {
            _partyStateManager.OnCurrentHeroChanged -= BindDodgeHero;
            _partyStateManager.OnCurrentHeroChanged += BindDodgeHero;
            BindDodgeHero(_partyStateManager.CurrentHero);
        }
        if (_combatManager == null) return;
        _combatManager.OnHitApplied -= HandleHitApplied;
        _combatManager.OnHitApplied += HandleHitApplied;
        _combatManager.OnPerfectDodgeSucceeded -= HandlePerfectDodgeSucceeded;
        _combatManager.OnPerfectDodgeSucceeded += HandlePerfectDodgeSucceeded;
        _combatManager.OnParryAssistSucceeded -= HandleParryAssistSucceeded;
        _combatManager.OnParryAssistSucceeded += HandleParryAssistSucceeded;
        if (_partySwapManager == null) _partySwapManager = PartySwapManager.Instance;
        if (_partySwapManager != null)
        {
            _partySwapManager.OnSwapExecuted -= HandleSwapNextExecuted;
            _partySwapManager.OnSwapExecuted += HandleSwapNextExecuted;
            _partySwapManager.OnSwapExecuted -= HandleSwapPrevExecuted;
            _partySwapManager.OnSwapExecuted += HandleSwapPrevExecuted;
        }
    }

    #endregion

    #region [단계 진행]

    /// <summary>
    /// 콤보스킬 동작과 후속 QTE 및 연출이 종료된 뒤 완료 팝업을 표시함.
    /// 설정창이 열려 있으면 닫힐 때까지 기다려 일시정지 소유권 충돌을 방지함.
    /// </summary>
    private void TryShowCompletionUI()
    {
        if (_completionUI == null || _completionUI.gameObject.activeSelf) return;
        if (SettingsManager.Instance != null && SettingsManager.Instance.IsSettingsOpen) return;
        ChainAttackManager chain = ChainAttackManager.Instance;
        if (chain != null && chain.CurrentPhase != ChainAttackPhase.Inactive) return;
        CombatPresentationManager presentation = CombatPresentationManager.Instance;
        if (presentation != null && (presentation.IsPresenting || presentation.IsUltPresentationActive)) return;
        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || (hero.CurrentState != HeroState.Idle && hero.CurrentState != HeroState.Walk &&
            hero.CurrentState != HeroState.Run && hero.CurrentState != HeroState.Sprint)) return;
        _tutorialUI.Hide();
        _completionUI.Show();
    }

    /// <summary>
    /// 패링 타격 처리 이후 기존 훈련용 적을 정리하고 풀피의 콤보스킬 연습 대상을 소환함.
    /// </summary>
    private void BeginChainAttackStep()
    {
        CurrentPhase = TutorialPhase.ChainAttack;
        if (_trainingEnemy != null)
        {
            if (_lockOnManager != null && _lockOnManager.MagneticTarget == _trainingEnemy.transform)
                _lockOnManager.ClearLockOn();
            _trainingEnemy.gameObject.SetActive(false);
            Destroy(_trainingEnemy.gameObject);
            _trainingEnemy = null;
        }

        SpawnTrainingEnemy();
        RefreshStepUI();
    }

    /// <summary>
    /// 훈련용 적에 대한 하드 락온 활성화를 두 번 성공하면 기본공격 단계로 전환함.
    /// 마지막 락온은 유지하며 해제와 대상 변경은 집계하지 않음.
    /// </summary>
    private void HandleHardLockActivated(Transform target)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.LockOn) return;
        if (_trainingEnemy == null || target == null ||
            target.GetComponentInParent<Enemy>() != _trainingEnemy) return;

        _lockOnSuccessCount++;
        if (_lockOnSuccessCount < 2)
        {
            RefreshStepUI();
            return;
        }

        CurrentPhase = TutorialPhase.BasicAttack;
        RefreshStepUI();
        OnLockOnStepCompleted?.Invoke();
    }

    /// <summary>
    /// 이전 Hero의 회피 이벤트를 해제하고 현재 조작 Hero의 이벤트를 연결함.
    /// null 전달 시 기존 구독만 해제하며 진행 횟수는 유지함.
    /// </summary>
    private void BindDodgeHero(Hero hero)
    {
        if (_dodgeEventHero != null) _dodgeEventHero.OnDodgeStarted -= HandleDodgeStarted;
        _dodgeEventHero = hero;
        if (_dodgeEventHero != null) _dodgeEventHero.OnDodgeStarted += HandleDodgeStarted;
    }

    /// <summary>
    /// 현재 조작 Hero의 실제 회피 시작을 누적하고 세 번 성공하면 회피 단계를 완료함.
    /// </summary>
    private void HandleDodgeStarted(Hero hero)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.Dodge) return;
        if (hero == null || hero != _partyStateManager.CurrentHero) return;

        _dodgeSuccessCount++;
        if (_dodgeSuccessCount < 3)
        {
            RefreshStepUI();
            return;
        }

        BeginPerfectDodgeStep();
        OnDodgeStepCompleted?.Invoke();
    }

    /// <summary>
    /// 극한회피 단계를 시작하고 훈련용 적의 자율 공격을 허용함.
    /// 사망 모션 중인 적은 기존 재소환 경로에서 교체함.
    /// </summary>
    private void BeginPerfectDodgeStep()
    {
        _perfectDodgeSuccessCount = 0;
        CurrentPhase = TutorialPhase.PerfectDodge;
        if (_trainingEnemy == null) SpawnTrainingEnemy();
        if (_trainingEnemy != null && !_trainingEnemy.Status.IsDead)
            _trainingEnemy.SetAIMode(EnemyAIMode.Normal);
        RefreshStepUI();
    }

    /// <summary>
    /// 극한회피 성공하면 공격을 유지한 채 패링지원 단계로 전환함.
    /// </summary>
    private void HandlePerfectDodgeSucceeded(Hero hero, IAttacker attacker)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.PerfectDodge) return;
        if (hero == null || hero != _partyStateManager.CurrentHero) return;
        if (_trainingEnemy == null || !ReferenceEquals(attacker, _trainingEnemy)) return;

        _perfectDodgeSuccessCount++;
        if (_perfectDodgeSuccessCount < 1)
        {
            RefreshStepUI();
            return;
        }

        _parryAssistSuccessCount = 0;
        CurrentPhase = TutorialPhase.ParryAssist;
        RefreshStepUI();
        OnPerfectDodgeStepCompleted?.Invoke();
    }

    /// <summary>
    /// 현재 조작 Hero가 훈련용 적에게 Parry Impact 성공하면 패링지원 단계를 완료함.
    /// 일반 교대나 패링 진입만으로는 집계하지 않으며 완료 후 적의 공격을 중단함.
    /// </summary>
    private void HandleParryAssistSucceeded(Hero hero, IAttacker attacker)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.ParryAssist) return;
        if (_isChainAttackStepPending) return;
        if (hero == null || hero != _partyStateManager.CurrentHero) return;
        if (_trainingEnemy == null || !ReferenceEquals(attacker, _trainingEnemy)) return;

        _parryAssistSuccessCount++;
        if (_parryAssistSuccessCount < 1)
        {
            RefreshStepUI();
            return;
        }

        _trainingEnemy.SetAIMode(EnemyAIMode.Stationary);
        _isChainAttackStepPending = true;
    }

    /// <summary>
    /// 예약된 파티원 합류와 궁극기 종료를 확인한 뒤 정방향 교대 안내를 시작함.
    /// </summary>
    private void TryBeginSwapNextStep()
    {
        if (_isJoiningParty || _joiningHeroes == null || _joiningHeroes.Length == 0) return;
        foreach (Hero joiningHero in _joiningHeroes)
        {
            if (joiningHero == null || Array.IndexOf(_partyStateManager.PartyMembers, joiningHero) < 0) return;
        }

        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || hero.Status == null || hero.Status.IsDead || hero.SwapSystem == null) return;
        if (!hero.SwapSystem.CanRequestSwap) return;
        if (CombatPresentationManager.Instance != null &&
            CombatPresentationManager.Instance.IsUltPresentationActive) return;

        _isSwapNextStepPending = false;
        _swapNextSuccessCount = 0;
        CurrentPhase = TutorialPhase.SwapNext;
        RefreshStepUI();
    }

    /// <summary>
    /// 정방향 일반 교대 성공을 누적하고 세 번 성공하면 역방향 교대 단계로 전환함.
    /// </summary>
    private void HandleSwapNextExecuted(Hero outHero, Hero inHero,
        SwapDirection direction, HeroSwapSystem.SwapType swapType)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.SwapNext) return;
        if (direction != SwapDirection.Next || swapType != HeroSwapSystem.SwapType.Normal) return;

        _swapNextSuccessCount++;
        if (_swapNextSuccessCount < 3)
        {
            RefreshStepUI();
            return;
        }

        _swapPrevSuccessCount = 0;
        CurrentPhase = TutorialPhase.SwapPrev;
        RefreshStepUI();
        OnSwapNextStepCompleted?.Invoke();
    }

    /// <summary>
    /// 역방향 일반 교대 성공을 누적하고 세 번 성공하면 역방향 교대 단계를 완료함.
    /// 정방향과 지원 교대는 횟수에 포함하지 않음.
    /// </summary>
    private void HandleSwapPrevExecuted(Hero outHero, Hero inHero,
        SwapDirection direction, HeroSwapSystem.SwapType swapType)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.SwapPrev) return;
        if (direction != SwapDirection.Prev || swapType != HeroSwapSystem.SwapType.Normal) return;

        _swapPrevSuccessCount++;
        if (_swapPrevSuccessCount < 3)
        {
            RefreshStepUI();
            return;
        }

        _dodgeSuccessCount = 0;
        CurrentPhase = TutorialPhase.Dodge;
        RefreshStepUI();
        OnSwapPrevStepCompleted?.Invoke();
    }

    /// <summary>
    /// 궁극기 연습 중 연출과 동작을 마치면 재시도에 부족한 데시벨을 3000으로 보충함.
    /// 현재 Maria의 애니메이션 타격은 Ult 상태 안에서 끝나므로 기본 상태 복귀를 확인함.
    /// </summary>
    private void RefillUltRetryDecibel()
    {
        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || hero.Status == null || hero.Status.IsDead) return;
        if (CombatPresentationManager.Instance != null &&
            CombatPresentationManager.Instance.IsUltPresentationActive) return;

        bool isBaseState = hero.CurrentState == HeroState.Idle || hero.CurrentState == HeroState.Walk ||
            hero.CurrentState == HeroState.Run || hero.CurrentState == HeroState.Sprint;
        if (!isBaseState || hero.Status.HasEnoughDecibelForUlt) return;

        hero.Status.ModifyDecibel(3000f - hero.Status.CurrentDecibel);
    }

    /// <summary>
    /// 강특 연습 중 기본 상태로 복귀하면 재시도에 부족한 에너지를 60으로 보충함.
    /// 스킬 및 후속타 진행 중에는 소비와 연계 판정에 개입하지 않음.
    /// </summary>
    private void RefillEXSkillRetryEnergy()
    {
        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || hero.Status == null || hero.Status.IsDead || hero.SkillSystem == null) return;

        bool isBaseState = hero.CurrentState == HeroState.Idle || hero.CurrentState == HeroState.Walk ||
            hero.CurrentState == HeroState.Run || hero.CurrentState == HeroState.Sprint;
        if (!isBaseState) return;
        if (!hero.SkillSystem.TryGetNextExSkillPreview(out _, out bool isReady) || isReady) return;

        hero.Status.ModifyEnergy(60f - hero.Status.CurrentEnergy);
    }

    /// <summary>
    /// 교대 안내 단계에서 호출하여 예약된 Hero를 Inspector 순서대로 합류시킴.
    /// 궁극기 목표 달성 시 호출하며 중복 진행을 차단함.
    /// </summary>
    public void JoinTutorialParty()
    {
        if (!isActiveAndEnabled || _isJoiningParty || _joiningHeroes == null) return;
        if (_partySwapManager == null) _partySwapManager = PartySwapManager.Instance;
        if (_partySwapManager == null) return;
        StartCoroutine(JoinTutorialPartyMembers());
    }

    /// <summary>
    /// 파티 합류 초기화가 끝난 뒤 다음 Hero의 합류를 진행함.
    /// </summary>
    private IEnumerator JoinTutorialPartyMembers()
    {
        _isJoiningParty = true;
        try
        {
            foreach (Hero hero in _joiningHeroes)
                yield return _partySwapManager.AddPartyMember(hero);
        }
        finally
        {
            _isJoiningParty = false;
        }
    }

    /// <summary>
    /// 조작 Hero와 필수 매니저가 준비되면 이동 튜토리얼을 한 번 시작함.
    /// </summary>
    public bool BeginTutorial()
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.NotStarted) return false;
        if (_movementDestination == null || _tutorialUI == null ||
            _trainingEnemyPrefab == null || _trainingEnemySpawnPoint == null || _enemyRoot == null) return false;
        if (_partyStateManager == null) _partyStateManager = PartyStateManager.Instance;
        if (_combatManager == null) _combatManager = CombatManager.Instance;
        if (_partyStateManager == null || _combatManager == null) return false;

        Hero hero = _partyStateManager.CurrentHero;
        if (hero == null || hero.Status == null || hero.Status.IsDead || !hero.IsInputEnabled) return false;

        _partyStateManager.SetPartyEnergyLockedAtZero(true);
        SubscribeTutorialEvents();
        CurrentPhase = TutorialPhase.Movement;
        RefreshStepUI();
        _tutorialUI.SetRemainingDistance(_movementDestination.GetPlanarDistance(hero.transform.position));
        OnMovementStepStarted?.Invoke();
        return true;
    }

    /// <summary>
    /// 현재 조작 Hero의 목적지 도착을 확인하고 락온 단계로 전환함.
    /// 중복 완료 요청을 차단하여 훈련용 적을 한 번만 생성함.
    /// </summary>
    public bool TryCompleteMovementStep(Hero arrivingHero)
    {
        if (!isActiveAndEnabled || CurrentPhase != TutorialPhase.Movement) return false;
        if (_partyStateManager == null || arrivingHero == null) return false;
        if (_partyStateManager.CurrentHero != arrivingHero) return false;
        if (arrivingHero.Status == null || arrivingHero.Status.IsDead) return false;
        if (!_movementDestination.HasArrived(arrivingHero)) return false;

        _lockOnSuccessCount = 0;
        CurrentPhase = TutorialPhase.LockOn;
        SpawnTrainingEnemy();
        RefreshStepUI();
        OnMovementStepCompleted?.Invoke();
        return true;
    }

    /// <summary>
    /// 훈련용 적을 지정 위치에 생성하고 활성화 전에 Stationary 모드를 적용함.
    /// 기존 훈련용 적이 남아 있으면 중복 소환하지 않음.
    /// </summary>
    private void SpawnTrainingEnemy()
    {
        if (_trainingEnemy != null) return;
        _trainingEnemy = Instantiate(_trainingEnemyPrefab,
            _trainingEnemySpawnPoint.position, _trainingEnemySpawnPoint.rotation, _enemyRoot);
        _trainingEnemy.SetAIMode(CurrentPhase == TutorialPhase.PerfectDodge ||
            CurrentPhase == TutorialPhase.ParryAssist
            ? EnemyAIMode.Normal : EnemyAIMode.Stationary);
        _trainingEnemy.gameObject.SetActive(true);
        if (CurrentPhase == TutorialPhase.ChainAttack)
        {
            // 신규 생성의 최대 HP는 EnemyStatus.Awake에서 설정되며, 그로기는 90%로 시작함.
            _trainingEnemy.Groggy.ApplyDazeAmount(
                _trainingEnemy.Status.MaxGroggyGauge * 0.9f - _trainingEnemy.Status.CurrentGroggyGauge);
        }
        // 최초 소환 및 재소환된 훈련용 적을 기존 소프트 록온 탐색에 반영함.
        LockOnManager.Instance?.RefreshSoftLockTarget();
    }

    /// <summary>
    /// 파티 Hero가 훈련용 적에게 적용한 피해를 현재 단계의 목표와 대조함.
    /// 기본공격은 생성 시점의 모션 번호, 일반스킬은 저장된 공격 종류로 판정함.
    /// </summary>
    private void HandleHitApplied(CombatEventContext context)
    {
        if (!isActiveAndEnabled) return;
        if (_trainingEnemy == null || !ReferenceEquals(context.Target, _trainingEnemy)) return;
        if (context.Attacker is not Hero hero ||
            Array.IndexOf(_partyStateManager.PartyMembers, hero) < 0) return;

        switch (CurrentPhase)
        {
            case TutorialPhase.BasicAttack:
                if (context.AttackType != HeroCombatAttackType.NormalAttack || context.ComboIndex != 2) return;
                CurrentPhase = TutorialPhase.NormalSkill;
                RefreshStepUI();
                OnBasicAttackStepCompleted?.Invoke();
                break;
            case TutorialPhase.NormalSkill:
                if (context.AttackType != HeroCombatAttackType.Skill) return;
                _isEXSkillStepPending = true;
                break;
            case TutorialPhase.EXSkill:
                if (context.AttackType != HeroCombatAttackType.EXSkill) return;
                _isUltStepPending = true;
                break;
            case TutorialPhase.Ult:
                if (context.AttackType != HeroCombatAttackType.Ult || _isSwapNextStepPending) return;
                _isSwapNextStepPending = true;
                JoinTutorialParty();
                _tutorialUI.SetProgress("궁극기 완료 · 교대 준비 중");
                OnUltStepCompleted?.Invoke();
                break;
            case TutorialPhase.ChainAttack:
                if (context.AttackType != HeroCombatAttackType.ChainAttack) return;
                CurrentPhase = TutorialPhase.Completed;
                RefreshStepUI();
                OnChainAttackStepCompleted?.Invoke();
                break;
        }
    }

    /// <summary>
    /// 현재 단계에 맞춰 마커, 안내 문구와 목표 표시를 갱신함.
    /// </summary>
    private void RefreshStepUI()
    {
        _movementDestination.SetVisible(CurrentPhase == TutorialPhase.Movement);
        switch (CurrentPhase)
        {
            case TutorialPhase.Movement:
                _tutorialUI.ShowMessage(_header, _movementTitle, _movementMessage, "01");
                break;
            case TutorialPhase.LockOn:
                _tutorialUI.ShowMessage(_header, _lockOnTitle, _lockOnMessage, "02");
                _tutorialUI.SetProgress($"목표: 락온 활성화 ({_lockOnSuccessCount}/2)");
                break;
            case TutorialPhase.BasicAttack:
                _tutorialUI.ShowMessage(_header, _basicAttackTitle, _basicAttackMessage, "03");
                _tutorialUI.SetProgress("목표: 기본공격 3타 적중");
                break;
            case TutorialPhase.NormalSkill:
                _tutorialUI.ShowMessage(_header, _normalSkillTitle, _normalSkillMessage, "04");
                _tutorialUI.SetProgress("목표: 일반스킬 적중");
                break;
            case TutorialPhase.EXSkill:
                _tutorialUI.ShowMessage(_header, _exSkillTitle, _exSkillMessage, "05");
                _tutorialUI.SetProgress("목표: 강화 특수스킬 적중");
                break;
            case TutorialPhase.Ult:
                _tutorialUI.ShowMessage(_header, _ultTitle, _ultMessage, "06");
                _tutorialUI.SetProgress("목표: 궁극기 적중");
                break;
            case TutorialPhase.SwapNext:
                _tutorialUI.ShowMessage(_header, _swapNextTitle, _swapNextMessage, "07");
                _tutorialUI.SetProgress($"목표: 정방향 교대 ({_swapNextSuccessCount}/3)");
                break;
            case TutorialPhase.SwapPrev:
                _tutorialUI.ShowMessage(_header, _swapPrevTitle, _swapPrevMessage, "08");
                _tutorialUI.SetProgress($"목표: 역방향 교대 ({_swapPrevSuccessCount}/3)");
                break;
            case TutorialPhase.Dodge:
                _tutorialUI.ShowMessage(_header, _dodgeTitle, _dodgeMessage, "09");
                _tutorialUI.SetProgress($"목표: 회피 ({_dodgeSuccessCount}/3)");
                break;
            case TutorialPhase.PerfectDodge:
                _tutorialUI.ShowMessage(_header, _perfectDodgeTitle, _perfectDodgeMessage, "10");
                _tutorialUI.SetProgress($"목표: 극한회피 ({_perfectDodgeSuccessCount}/1)");
                break;
            case TutorialPhase.ParryAssist:
                _tutorialUI.ShowMessage(_header, _parryAssistTitle, _parryAssistMessage, "11");
                _tutorialUI.SetProgress($"목표: 패링지원 ({_parryAssistSuccessCount}/1)");
                break;
            case TutorialPhase.ChainAttack:
                _tutorialUI.ShowMessage(_header, _chainAttackTitle, _chainAttackMessage, "12");
                _tutorialUI.SetProgress("목표: 콤보스킬 적중 (0/1)");
                break;
            case TutorialPhase.Completed:
                _tutorialUI.ShowMessage(_header, _chainAttackTitle, _chainAttackCompletedMessage, "완료");
                _tutorialUI.SetProgress("콤보스킬 완료");
                break;
        }
    }

    #endregion
}
