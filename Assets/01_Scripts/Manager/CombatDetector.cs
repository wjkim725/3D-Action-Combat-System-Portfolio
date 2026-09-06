using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatDetector : MonoBehaviour
{
    public static CombatDetector Instance { get; private set; }

    [Header("Detection Settings")]
    [SerializeField] private float _detectRange = 10f;
    [SerializeField] private float _combatExitDelay = 5f;
    [SerializeField] private LayerMask _enemyLayerMask;
    [SerializeField] private float _checkInterval = 0.2f;

    private float _lastCombatTimer = 0f;
    private bool _isPartyInCombat = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 매니저에서 단 하나의 루틴만 구동 (파티원 수와 상관없이 연산량 고정)
        StartCoroutine(CombatCheckRoutine());
    }


    private IEnumerator CombatCheckRoutine()
    {
        while (true)
        {
            if (PartyStateManager.Instance.CurrentHero != null)
            {
                // 1. 온필드 캐릭터 기준으로 물리 체크
                bool enemyDetected = Physics.CheckSphere
                    (PartyStateManager.Instance.CurrentHero.transform.position, _detectRange, _enemyLayerMask);

                // 2. 온필드 캐릭터의 상태 체크
                bool anyCombatAction = PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.Attack ||
                                     PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.Skill ||
                                     PartyStateManager.Instance.CurrentHero.CurrentState == HeroState.Ult;

                if (enemyDetected || anyCombatAction)
                {
                    _lastCombatTimer = Time.time;
                    SetPartyCombatState(true);
                }
                else
                {
                    if (Time.time - _lastCombatTimer > _combatExitDelay)
                    {
                        SetPartyCombatState(false);
                    }
                }
            }

            yield return new WaitForSeconds(_checkInterval);
        }
    }

    private void SetPartyCombatState(bool isCombat)
    {
        if (_isPartyInCombat == isCombat) return;
        _isPartyInCombat = isCombat;

        foreach (var hero in PartyStateManager.Instance.PartyMembers)
        {
            hero.IsCombat = isCombat;
        }
    }
}