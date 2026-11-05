using UnityEngine;

[DisallowMultipleComponent]
public class MariaAudio : MonoBehaviour
{
    private MariaMechanic _mechanic;
    private HeroStatus _status;
    private PartyStateManager _partyStateManager;

    // 핸들을 발급한 Manager에 반환하도록 함께 보관함.
    private AudioManager _blazingLoopManager;
    private long _blazingLoopHandle;
    private bool _blazingHasStarted;


    [Header("AudioCue Datas")]
    [SerializeField] private AudioCueSO _blazingLoopCue;
    [SerializeField] private AudioCueSO _blazingOnCue;
    [SerializeField] private AudioCueSO _blazingOffCue;


    [SerializeField] private AudioCueSO _slidingCue;        // Chain
    [SerializeField] private AudioCueSO _dashForwardCue;    // EXSkill Finale & Ult

    [SerializeField] private AudioCueSO _lightSlashCue;

    [SerializeField] private AudioCueSO _heavySlashCue_A;
    [SerializeField] private AudioCueSO _heavySlashCue_B;

    [SerializeField] private AudioCueSO _blazingLightSlashCue;

    [SerializeField] private AudioCueSO _blazingHeavySlashCue_A;
    [SerializeField] private AudioCueSO _blazingHeavySlashCue_B;

    [SerializeField] private AudioCueSO _ultSlashCue;
    [SerializeField] private AudioCueSO _ultImpactCue;


    private void Awake()
    {
        _mechanic = GetComponent<MariaMechanic>();
        _status = GetComponent<HeroStatus>();
    }

    private void OnEnable()
    {
        if (_mechanic != null)
            _mechanic.OnBlazingActiveChanged += HandleBlazingActiveChanged;

        if (_blazingHasStarted)
        {
            SubscribeCurrentHeroChanged();
            SynchronizeBlazingLoop();
        }
    }

    private void Start()
    {
        _blazingHasStarted = true;
        SubscribeCurrentHeroChanged();
        SynchronizeBlazingLoop();
    }

    private void OnDisable()
    {
        if (_mechanic != null)
            _mechanic.OnBlazingActiveChanged -= HandleBlazingActiveChanged;

        if (_partyStateManager != null)
            _partyStateManager.OnCurrentHeroChanged -= HandleCurrentHeroChanged;
        _partyStateManager = null;

        StopBlazingLoop();
    }

    #region [교대 이벤트 연결]

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
    /// 현재 조작 Hero가 변경되면 전환음 없이 반복음만 재평가함.
    /// </summary>
    private void HandleCurrentHeroChanged(Hero currentHero)
    {
        SynchronizeBlazingLoop();
    }

    #endregion

    #region [Blazing SFX 제어]

    /// <summary>
    /// Blazing 상태 변경에 따라 반복음과 전환음을 처리함.
    /// </summary>
    private void HandleBlazingActiveChanged(bool isActive)
    {
        if (!isActiveAndEnabled || !_blazingHasStarted)
            return;

        if (isActive)
        {
            PlayBlazingOnSFX();
            SynchronizeBlazingLoop();
        }
        else
        {
            // 사망 여부와 관계없이 반복음부터 정리함.
            StopBlazingLoop();
            PlayBlazingOffSFX();
        }
    }

    /// <summary>
    /// 온필드이면서 Blazing이 활성화된 경우에만 반복음을 유지함.
    /// </summary>
    private void SynchronizeBlazingLoop()
    {
        bool canPlay =
            isActiveAndEnabled &&
            _mechanic != null &&
            _mechanic.IsBlazingActive &&
            PartyStateManager.Instance != null &&
            PartyStateManager.Instance.CurrentHero == _mechanic.Hero;

        if (canPlay)
            StartBlazingLoop();
        else
            StopBlazingLoop();
    }

    /// <summary>
    /// Blazing 반복음용 Source를 중복 없이 대여함.
    /// </summary>
    private void StartBlazingLoop()
    {
        if (!isActiveAndEnabled || _blazingLoopCue == null)
            return;

        if (_blazingLoopManager != null &&
            _blazingLoopManager.IsLoopActive(_blazingLoopHandle))
            return;

        StopBlazingLoop();

        AudioManager manager = AudioManager.Instance;
        if (manager == null)
            return;

        long handle = manager.StartLoop(_blazingLoopCue, transform);
        if (handle == 0)
            return;

        _blazingLoopManager = manager;
        _blazingLoopHandle = handle;
    }

    /// <summary>
    /// 대여한 Blazing 반복음을 반환하고 핸들을 초기화함.
    /// </summary>
    private void StopBlazingLoop()
    {
        if (_blazingLoopManager != null)
            _blazingLoopManager.StopLoop(_blazingLoopHandle);

        _blazingLoopManager = null;
        _blazingLoopHandle = 0;
    }

    /// <summary>
    /// 생존 상태에서 Blazing 시작음을 재생함.
    /// </summary>
    private void PlayBlazingOnSFX()
    {
        if (!isActiveAndEnabled ||
            _status == null || _status.IsDead ||
            _blazingOnCue == null)
        {
            return;
        }

        AudioManager.Instance?.PlayOneShot(
            _blazingOnCue,
            transform.position);
    }

    /// <summary>
    /// 생존 상태에서 Blazing 종료음을 재생함.
    /// </summary>
    private void PlayBlazingOffSFX()
    {
        if (!isActiveAndEnabled ||
            _status == null || _status.IsDead ||    // 사망할때 Blazing Off SFX 재생은 차단
            _blazingOffCue == null)
        {
            return;
        }

        AudioManager.Instance?.PlayOneShot(
            _blazingOffCue,
            transform.position);
    }

    #endregion


    #region [SFX 이벤트 영역]

    /// <summary>
    /// 애니메이션 이벤트 시점에 슬라이딩 효과음을 재생함.
    /// </summary>
    public void OnSlidingSFX()
    {
        if (!isActiveAndEnabled || _slidingCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _slidingCue,
            transform.position);
    }

    public void OnDashForwardSFX()
    {
        if (!isActiveAndEnabled || _dashForwardCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _dashForwardCue,
            transform.position);
    }
    
    public void OnUltSlashSFX()
    {
        if (!isActiveAndEnabled || _ultSlashCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _ultSlashCue,
            transform.position);
    }

    public void OnUltImpactSFX()
    {
        if (!isActiveAndEnabled || _ultImpactCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _ultImpactCue,
            transform.position);
    }

    public void OnLightSlash_SFX() 
    {
        if (_mechanic == null) return;

        if (!_mechanic.IsBlazingActive)
        {
            PlayLightSlashSFX();
        }
        else
        {
            PlayBlazingLightSlash_SFX();
        }
    }

    /// <summary>
    /// 비교적 날렵한 (DashAttack 등) Heavy Slash SFX 재생
    /// </summary>
    public void OnHeavySlashA_SFX()
    {
        if (_mechanic == null) return;

        if (!_mechanic.IsBlazingActive)
        {
            PlayHeavySlashA_SFX();
        }
        else
        {
            PlayBlazingHeavySlashA_SFX();
        }
    }

    /// <summary>
    /// 비교적 묵직한 (AttackCombo 막타 등) Heavy Slash SFX 재생
    /// </summary>
    public void OnHeavySlashB_SFX()
    {
        if (_mechanic == null) return;

        if (!_mechanic.IsBlazingActive)
        {
            PlayHeavySlashB_SFX();
        }
        else
        {
            PlayBlazingHeavySlashB_SFX();
        }
    }

    

    #endregion


    #region [SFX 재생 영역]

    private void PlayLightSlashSFX()
    {
        if (!isActiveAndEnabled || _lightSlashCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _lightSlashCue,
            transform.position);
    }

    private void PlayHeavySlashA_SFX()
    {
        if (!isActiveAndEnabled || _heavySlashCue_A == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _heavySlashCue_A,
            transform.position);
    }

    private void PlayHeavySlashB_SFX()
    {
        if (!isActiveAndEnabled || _heavySlashCue_B == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _heavySlashCue_B,
            transform.position);
    }

    private void PlayBlazingLightSlash_SFX()
    {
        if (!isActiveAndEnabled || _blazingLightSlashCue == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _blazingLightSlashCue,
            transform.position);
    }

    private void PlayBlazingHeavySlashA_SFX()
    {
        if (!isActiveAndEnabled || _blazingHeavySlashCue_A == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _blazingHeavySlashCue_A,
            transform.position);
    }

    private void PlayBlazingHeavySlashB_SFX()
    {
        if (!isActiveAndEnabled || _blazingHeavySlashCue_B == null)
            return;

        AudioManager.Instance?.PlayOneShot(
            _blazingHeavySlashCue_B,
            transform.position);
    }

    

    #endregion
}
