using UnityEngine;

public class HeroSkillSystem : MonoBehaviour
{
    private Hero _hero;

    #region [프로퍼티 영역]

    public bool IsExSkillMode { get; private set; }     // 현재 Skill 의 EX 여부 플래그

    public int CurrentExSkillComboIndex
    {
        get
        {
            if (_hero.ActionRegistry == null)
            {
                Debug.LogError("HeroSkillSystem : Hero.ActionRegistry Null");
                return 0;
            }

            // ComboIndex는 범용적으로 하나만 사용. 
            // 대신 CurrentState:Skill 일때만 CurrentComboIndex 가져옴
            if (_hero.CurrentState != HeroState.Skill) return 0;

            return _hero.ActionRegistry.CurrentComboIndex;
        }
    }
    
    public float CurrentEnergyRequirement
    {
        get
        {
            if (_hero.CombatData == null || _hero.CombatData.exSkillData == null || _hero.CombatData.exSkillData.Count == 0)
            {
                Debug.LogError("HeroSkillSystem : CombatData 또는 ExSkillData가 비어있습니다.");
                return 0f;
            }

            // 만약 플레이어가 리스트 범위를 벗어나는 연타를 했다면 예외 처리 (마지막 타격 요구치 고정 혹은 0타로 롤백)
            int targetIndex = CurrentExSkillComboIndex;
            if (targetIndex >= _hero.CombatData.exSkillData.Count)
            {
                // 1) 마지막 연타 데이터 반복 (ex: 40 -> 20 -> 20 -> 20...)
                targetIndex = _hero.CombatData.exSkillData.Count - 1;
            }

            return _hero.CombatData.exSkillData[targetIndex].energyRequirement;
        }
    }

    #endregion


    private void Awake()
    {
        _hero = GetComponent<Hero>();
    }


    #region [내부 연산 및 헬퍼 메서드]

    public bool CanExecuteExSkill()
    {
        return _hero.Status.CurrentEnergy >= CurrentEnergyRequirement;
    }

    public void ExecuteExSkillSequence()
    {
        _hero.Status.ModifyEnergy(-CurrentEnergyRequirement);
        //_hero.Status.ModifyEnergy(5);
        _hero.Animation.TriggerExSkill();
        //_hero.Animation.CrossFadeToExSkill(0.1f);
        Debug.Log($"[EX Skill Combo Index] {_hero.ActionRegistry.CurrentComboIndex} / 에너지 소모: {CurrentEnergyRequirement}");
    }

    public void ExecuteSkillSequence()
    {
        _hero.Animation.TriggerSkill();
        //_hero.Animation.CrossFadeToSkill(0.1f);
        Debug.Log($"[Skill Combo Index] {_hero.ActionRegistry.CurrentComboIndex}");
    }

    public void SkillLogic()
    {
        if (CanExecuteExSkill())
        {
            IsExSkillMode = true;
            ExecuteExSkillSequence();
        }
        else
        {
            IsExSkillMode = false;
            ExecuteSkillSequence();
        }
    }

    /// <summary>
    /// IsExSkillMode 를 외부(애니메이션 이벤트) 등에서 변경할 때 호출
    /// 켰으면 끄는 과정 까먹으면 안됨!!!!!
    /// </summary>
    public void SetIsExSkillMode(bool isExSkillMode)
    {
        IsExSkillMode = isExSkillMode;
    }

    /// <summary>
    /// [2타 이후 연타용] 애니메이션 캔슬 윈도우나 특정 연계 프레임 이벤트(OnCheckCombo)에서 호출될 메서드
    /// </summary>
    public void OnSkillComboExecuted()
    {
        if (_hero.CurrentState != HeroState.Skill) return;

        // 만약 첫타가 일반 스킬로 나갔다면, 연타도 일반 스킬이므로 에너지 소모 없이 리턴
        if (!IsExSkillMode)
        {
            ExecuteSkillSequence();
            return;
        }

        // 강화 스킬 모드일 때: 다음 연타에 필요한 에너지가 있는지 체크
        if (CanExecuteExSkill())
        {
            ExecuteExSkillSequence();
        }
        else
        {
            // 강화 스킬 연타 중 에너지가 모자라면 ExSkill 해제 및 ActionRegistry 초기화
            SetIsExSkillMode(false);
            _hero.ActionRegistry.ResetCombo();

            Debug.Log(
        $"[EX 연계 중단] " +
        $"State:{_hero.CurrentState} | " +
        $"Energy:{_hero.Status.CurrentEnergy} | " +
        $"Combo:{_hero.ActionRegistry.CurrentComboIndex} | " +
        $"QuickSwap:{_hero.SwapSystem.IsQuickSwapWindowOpen} | " +
        $"Chain:{ChainAttackManager.Instance?.CurrentPhase}"
    );

            Debug.Log("강화 연타 도중 에너지 고갈 -> 일반 모드");
        }
    }

    #endregion
}
