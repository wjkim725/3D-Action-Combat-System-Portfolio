using System;
using UnityEngine;

/// <summary>
/// 파티 편성 정보와 현재 파티 상태를 관리
/// 실제 교대 입력과 등장·퇴장 연출은 PartySwapManager가 담당
/// </summary>
public class PartyStateManager : MonoBehaviour
{
    public static PartyStateManager Instance { get; private set; }

    [Header("Party Members")]
    [SerializeField] private Hero[] _partyMembers; // 인스펙터에서 캐릭터를 파티 순서대로 할당
    [SerializeField] private int _currentHeroIndex = 0;

    #region [이벤트 영역]

    // 현재 조작 Hero 변경 이벤트 <- 이제 PartySwapManager 는 해당 이벤트를 소유하지 않음
    public event Action<Hero> OnCurrentHeroChanged;

    // 파티 전멸 상태 전달
    public event Action OnPartyDefeated;

    #endregion


    #region [프로퍼티 영역]

    public Hero[] PartyMembers => _partyMembers;
    public int CurrentHeroIndex => _currentHeroIndex;

    public Hero CurrentHero
    {
        get
        {
            if (_partyMembers == null || _partyMembers.Length == 0) return null;
            if (_currentHeroIndex < 0 || _currentHeroIndex >= _partyMembers.Length) return null;

            return _partyMembers[_currentHeroIndex];
        }
    }

    public Hero NextHero
    {
        get
        {
            if (_partyMembers == null || _partyMembers.Length == 0) return null;

            int nextIndex = (_currentHeroIndex + 1) % _partyMembers.Length;
            return _partyMembers[nextIndex];
        }
    }

    public Hero PrevHero
    {
        get
        {
            if (_partyMembers == null || _partyMembers.Length == 0) return null;

            int prevIndex = (_currentHeroIndex - 1 + _partyMembers.Length) % _partyMembers.Length;
            return _partyMembers[prevIndex];
        }
    }

    public bool IsPartyDefeated => GetAlivePartyCount() == 0;

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ValidateCurrentHeroIndex();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    #region [현재 Hero 상태 관리 영역]

    /// <summary>
    /// 지정한 Hero를 현재 조작 Hero로 확정
    /// 확정이 완료된 시점에 OnCurrentHeroChanged 이벤트를 발생
    /// </summary>
    public bool TrySetCurrentHero(Hero hero)
    {
        if (hero == null || _partyMembers == null) return false;

        int heroIndex = Array.IndexOf(_partyMembers, hero);
        if (heroIndex < 0)
        {
            Debug.LogError($"{hero.gameObject.name}이 파티 배열에 존재하지 않습니다.");
            return false;
        }

        return TrySetCurrentHeroIndex(heroIndex);
    }

    /// <summary>
    /// 지정한 파티 인덱스를 현재 조작 Hero 인덱스로 확정
    /// </summary>
    public bool TrySetCurrentHeroIndex(int heroIndex)
    {
        if (_partyMembers == null || _partyMembers.Length == 0) return false;
        if (heroIndex < 0 || heroIndex >= _partyMembers.Length) return false;
        if (_partyMembers[heroIndex] == null) return false;

        if (_currentHeroIndex == heroIndex)
            return true;

        _currentHeroIndex = heroIndex;
        OnCurrentHeroChanged?.Invoke(CurrentHero);
        return true;
    }

    /// <summary>
    /// 현재 Hero는 유지한 채 HUD 등 구독자에게 초기 상태 갱신을 요청
    /// </summary>
    public void NotifyCurrentHeroChanged()
    {
        if (CurrentHero == null) return;

        OnCurrentHeroChanged?.Invoke(CurrentHero);
    }

    #endregion


    #region [파티원 조회 영역]

    /// <summary>
    /// 현재 파티 상태를 순회하며 정방향 교대가 가능한 Hero를 반환
    /// </summary>
    public Hero GetValidNextHero()
    {
        return GetValidSwapHero(SwapDirection.Next);
    }

    /// <summary>
    /// 현재 파티 상태를 순회하며 역방향 교대가 가능한 Hero를 반환
    /// </summary>
    public Hero GetValidPrevHero()
    {
        return GetValidSwapHero(SwapDirection.Prev);
    }

    /// <summary>
    /// 현재 Hero를 제외하고 지정한 방향으로 교대 가능한 Hero를 탐색
    /// </summary>
    public Hero GetValidSwapHero(SwapDirection direction)
    {
        int directionStep = direction == SwapDirection.Next ? 1 : -1;

        if (_partyMembers == null || _partyMembers.Length <= 1) return null;

        for (int i = 1; i < _partyMembers.Length; i++)
        {
            int targetIndex =
                (_currentHeroIndex + directionStep * i + _partyMembers.Length) %
                _partyMembers.Length;

            Hero candidate = _partyMembers[targetIndex];

            if (candidate == null ||
                candidate.Status == null ||
                candidate.SwapSystem == null)
            {
                continue;
            }

            if (candidate.Status.IsDead) continue;
            if (!candidate.SwapSystem.CanBeSwappedIn) continue;

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// 현재 파티 상태를 순회하며 정방향으로 생존한 Hero를 반환 
    /// CurrentHero 사망 시 사망 교대 처리 & ChainAttack Candidate Hero 
    /// & QuickAssist 등장 Hero 탐색에 사용
    /// </summary>
    public Hero GetNextAliveHero()
    {
        return GetAliveHeroInDirection(1);
    }

    /// <summary>
    /// 현재 파티 상태를 순회하며 역방향으로 생존한 Hero를 반환 
    /// ChainAttack Candidate Hero 탐색에 사용
    /// </summary>
    public Hero GetPrevAliveHero()
    {
        return GetAliveHeroInDirection(-1);
    }

    private Hero GetAliveHeroInDirection(int directionStep)
    {
        if (_partyMembers == null || _partyMembers.Length <= 1) return null;

        for (int i = 1; i < _partyMembers.Length; i++)
        {
            int targetIndex =
                (_currentHeroIndex + directionStep * i + _partyMembers.Length) %
                _partyMembers.Length;

            Hero candidate = _partyMembers[targetIndex];

            if (candidate == null || candidate.Status == null) continue;
            if (candidate.Status.IsDead) continue;

            return candidate;
        }

        return null;
    }


    /// <summary>
    /// 현재 파티에서 생존한 Hero 수를 반환
    /// </summary>
    public int GetAlivePartyCount()
    {
        if (_partyMembers == null || _partyMembers.Length == 0) return 0;

        int aliveCount = 0;

        foreach (Hero hero in _partyMembers)
        {
            if (hero == null || hero.Status == null) continue;
            if (hero.Status.IsDead) continue;

            aliveCount++;
        }

        return aliveCount;
    }

    #endregion


    #region [파티 전멸 처리 영역]

    /// <summary>
    /// 모든 파티원이 사망한 경우 파티 전멸 이벤트를 발생
    /// 실제 실패 UI와 재시작 처리는 외부 시스템에서 담당
    /// </summary>
    public void NotifyPartyDefeated()
    {
        if (!IsPartyDefeated) return;

        Debug.Log("파티 전멸");
        OnPartyDefeated?.Invoke();
    }

    #endregion


    private void ValidateCurrentHeroIndex()
    {
        if (_partyMembers == null || _partyMembers.Length == 0)
        {
            _currentHeroIndex = 0;
            return;
        }

        _currentHeroIndex = Mathf.Clamp(
            _currentHeroIndex,
            0,
            _partyMembers.Length - 1
        );
    }
}