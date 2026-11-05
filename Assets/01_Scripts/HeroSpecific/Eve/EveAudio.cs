using UnityEngine;

[DisallowMultipleComponent]
public class EveAudio : MonoBehaviour
{
    private Hero _hero;
    private HeroStatus _status;
    private HeroWeapon _weapon;
    private PartyStateManager _partyStateManager;

    // 핸들을 발급한 Manager에 반환하도록 함께 보관함.
    private AudioManager _handedManaLoopManager;
    private long _handedManaLoopHandle;
    private bool _handedManaHasStarted;
    private AudioManager _chargeLoopManager;
    private long _chargeLoopHandle;
    // 차징 시작마다 해제 소멸음을 한 번만 허용함.
    private bool _chargeCancelPending;

    [Header("AudioCue Datas")]
    [SerializeField] private AudioCueSO _handedManaLoopCue;
    [SerializeField] private AudioCueSO _handSwingCue;
    [SerializeField] private AudioCueSO _quickAssistFireBreathCue;
    [SerializeField] private AudioCueSO _attackComboCue;
    [SerializeField] private AudioCueSO _chargeStartCue;
    [SerializeField] private AudioCueSO _chargeLoopCue;
    [SerializeField] private AudioCueSO _chargeCancelCue;

    #region [초기화 영역]

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        _status = GetComponent<HeroStatus>();
        _weapon = GetComponent<HeroWeapon>();
    }

    private void OnEnable()
    {
        if (_hero != null)
            _hero.OnDeathStateChanged += HandleDeathStateChanged;

        if (_weapon != null)
            _weapon.OnWeaponDisplayStateChanged += HandleWeaponDisplayStateChanged;

        if (_handedManaHasStarted)
        {
            SubscribeCurrentHeroChanged();
            SynchronizeHandedManaLoop();
        }
    }

    private void Start()
    {
        _handedManaHasStarted = true;
        SubscribeCurrentHeroChanged();
        SynchronizeHandedManaLoop();
    }

    private void OnDisable()
    {
        if (_hero != null)
            _hero.OnDeathStateChanged -= HandleDeathStateChanged;

        if (_weapon != null)
            _weapon.OnWeaponDisplayStateChanged -= HandleWeaponDisplayStateChanged;

        if (_partyStateManager != null)
            _partyStateManager.OnCurrentHeroChanged -= HandleCurrentHeroChanged;
        _partyStateManager = null;

        StopHandedManaLoop();
        CancelChargeSFX();
    }

    private void LateUpdate()
    {
        if (_chargeCancelPending && (_hero == null ||
            _hero.CurrentState != HeroState.ChargeAttack ||
            !_hero.Animation.IsAttackCharging ||
            _status == null || _status.IsDead ||
            PartyStateManager.Instance == null || PartyStateManager.Instance.CurrentHero != _hero))
            CancelChargeSFX();

        if (_chargeLoopHandle != 0 && !CanMaintainChargeLoop())
            StopChargeLoop();
    }

    #endregion

    #region [상태 이벤트 연결]

    /// <summary>
    /// 초기화된 파티 Manager의 현재 Hero 변경 이벤트를 중복 없이 구독함.
    /// </summary>
    private void SubscribeCurrentHeroChanged()
    {
        PartyStateManager manager = PartyStateManager.Instance;
        if (_partyStateManager == manager)
            return;

        if (_partyStateManager != null)
            _partyStateManager.OnCurrentHeroChanged -= HandleCurrentHeroChanged;

        _partyStateManager = manager;
        if (_partyStateManager != null)
            _partyStateManager.OnCurrentHeroChanged += HandleCurrentHeroChanged;
    }

    /// <summary>
    /// 현재 조작 Hero가 변경되면 손 마력 반복음을 재평가함.
    /// </summary>
    private void HandleCurrentHeroChanged(Hero currentHero)
    {
        if (currentHero != _hero) CancelChargeSFX();
        SynchronizeHandedManaLoop();
    }

    /// <summary>
    /// 사망 또는 부활 후 실제 Hero 상태에 맞춰 반복음을 재평가함.
    /// </summary>
    private void HandleDeathStateChanged(bool isAlive)
    {
        if (!isAlive) CancelChargeSFX();
        SynchronizeHandedManaLoop();
    }

    /// <summary>
    /// 손 마력의 무기 표시 상태가 변경되면 반복음을 재평가함.
    /// </summary>
    private void HandleWeaponDisplayStateChanged(HeroWeapon.WeaponDisplayState state)
    {
        SynchronizeHandedManaLoop();
    }

    #endregion

    #region [SFX 이벤트 영역]

    /// <summary>
    /// 마법 시전 모션의 팔 휘두름 이벤트 시점에 효과음을 재생함.
    /// </summary>
    public void OnHandSwingSFX()
    {
        if (!isActiveAndEnabled || _handSwingCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _handSwingCue,
            transform.position);
    }

    /// <summary>
    /// 빠른 지원의 화염 분사 시작 이벤트에서 효과음을 한 번 재생함.
    /// </summary>
    public void OnQuickAssistFireBreathSFX()
    {
        if (!isActiveAndEnabled || _quickAssistFireBreathCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _quickAssistFireBreathCue,
            transform.position);
    }

    #endregion

    #region [투사체 발사 SFX]

    /// <summary>
    /// 평타와 대시·강공격의 투사체 발사 이벤트에서 효과음을 재생함.
    /// </summary>
    public void OnAttackComboSFX()
    {
        if (!isActiveAndEnabled || _attackComboCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _attackComboCue,
            transform.position);
    }

    #endregion

    #region [차징 SFX 제어]

    /// <summary>
    /// 차징 시작 클립 진입 시 짧은 마력 응집음을 재생함.
    /// </summary>
    public void OnChargeStartSFX(AnimationEvent animationEvent)
    {
        if (!CanMaintainChargeLoop() || !IsCurrentChargeEvent(animationEvent)) return;
        StopChargeLoop();
        _chargeCancelPending = true;
        if (_chargeStartCue != null)
            AudioManager.Instance?.PlayOneShot(_chargeStartCue, transform.position);
    }

    /// <summary>
    /// 완료 대기 클립 진입 시 위치 추적 루프 하나만 대여함.
    /// </summary>
    public void OnChargeHoldSFX(AnimationEvent animationEvent)
    {
        if (!CanMaintainChargeLoop() || !IsCurrentChargeEvent(animationEvent) || _chargeLoopCue == null) return;
        if (_chargeLoopManager != null && _chargeLoopManager.IsLoopActive(_chargeLoopHandle)) return;
        StopChargeLoop();
        AudioManager manager = AudioManager.Instance;
        if (manager == null) return;
        long handle = manager.StartLoop(_chargeLoopCue, transform);
        if (handle == 0) return;
        _chargeLoopManager = manager;
        _chargeLoopHandle = handle;
    }

    /// <summary>
    /// 발사 전환이 해제 감지보다 먼저 실행되어도 소멸음을 한 번만 처리함.
    /// </summary>
    public void OnChargeReleaseSFX(AnimationEvent animationEvent)
    {
        if (_hero == null || !IsCurrentChargeEvent(animationEvent)) return;
        CancelChargeSFX();
    }

    /// <summary>
    /// 버튼 해제 또는 차징 중단 시 소멸음을 한 번 재생하고 루프를 반환함.
    /// </summary>
    private void CancelChargeSFX()
    {
        bool shouldPlay = _chargeCancelPending;
        _chargeCancelPending = false;
        StopChargeLoop();
        if (shouldPlay && Application.isPlaying && _chargeCancelCue != null)
            AudioManager.Instance?.PlayOneShot(_chargeCancelCue, transform.position);
    }

    /// <summary>
    /// 온필드에서 생존한 Eve가 차징을 유지하는지 확인함.
    /// </summary>
    private bool CanMaintainChargeLoop()
    {
        return isActiveAndEnabled && _hero != null &&
            _hero.CurrentState == HeroState.ChargeAttack &&
            _hero.Animation.IsAttackCharging &&
            _status != null && !_status.IsDead &&
            PartyStateManager.Instance != null &&
            PartyStateManager.Instance.CurrentHero == _hero;
    }

    /// <summary>
    /// 전이 중 빠져나가는 차징 클립의 지연 이벤트를 제외함.
    /// </summary>
    private bool IsCurrentChargeEvent(AnimationEvent animationEvent)
    {
        if (animationEvent == null || !animationEvent.isFiredByAnimator) return false;
        Animator animator = _hero.Animation.Animator;
        int targetHash = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0).fullPathHash
            : animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        return animationEvent.animatorStateInfo.fullPathHash == targetHash;
    }

    /// <summary>
    /// 발사·취소·비활성화 시 대여한 차징 루프를 반환함.
    /// </summary>
    private void StopChargeLoop()
    {
        if (_chargeLoopManager != null)
            _chargeLoopManager.StopLoop(_chargeLoopHandle);
        _chargeLoopManager = null;
        _chargeLoopHandle = 0;
    }

    #endregion

    #region [손 마력 SFX 제어]

    /// <summary>
    /// 온필드에서 생존 중이며 손 마력이 표시될 때 반복음 하나를 유지함.
    /// </summary>
    private void SynchronizeHandedManaLoop()
    {
        if (!_handedManaHasStarted)
            return;

        bool canPlay =
            isActiveAndEnabled &&
            _hero != null && _hero.CurrentState != HeroState.Dead &&
            _status != null && !_status.IsDead &&
            _weapon != null &&
            _weapon.CurrentState != HeroWeapon.WeaponDisplayState.Hide &&
            _partyStateManager != null &&
            _partyStateManager.CurrentHero == _hero;

        if (canPlay)
            StartHandedManaLoop();
        else
            StopHandedManaLoop();
    }

    /// <summary>
    /// 손 마력 반복음용 Source를 중복 없이 대여하여 Eve 위치를 따라 재생함.
    /// </summary>
    private void StartHandedManaLoop()
    {
        if (!isActiveAndEnabled || _handedManaLoopCue == null)
            return;

        if (_handedManaLoopManager != null &&
            _handedManaLoopManager.IsLoopActive(_handedManaLoopHandle))
            return;

        StopHandedManaLoop();

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
            return;

        long handle = manager.StartLoop(_handedManaLoopCue, transform);
        if (handle == 0)
            return;

        _handedManaLoopManager = manager;
        _handedManaLoopHandle = handle;
    }

    /// <summary>
    /// 대여한 손 마력 반복음을 반환하고 핸들을 초기화함.
    /// </summary>
    private void StopHandedManaLoop()
    {
        if (_handedManaLoopManager != null)
            _handedManaLoopManager.StopLoop(_handedManaLoopHandle);

        _handedManaLoopManager = null;
        _handedManaLoopHandle = 0;
    }

    #endregion
}
