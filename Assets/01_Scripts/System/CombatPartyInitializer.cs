using System;
using UnityEngine;

/// <summary>
/// 확정 편성의 Hero 프리팹을 생성하고 전투 시스템 초기화 전에 파티를 등록함.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class CombatPartyInitializer : MonoBehaviour
{
    [Header("Scene Party")]
    [SerializeField] private PartyStateManager _partyStateManager;
    [SerializeField] private Transform _partyRoot;
    [SerializeField] private Transform _spawnPoint;

    [Header("Entry Presentation")]
    [SerializeField] private CombatEntryController _entryController;

    public bool HasAppliedFormation { get; private set; }

    #region [전투 초기 편성]

    private void Awake()
    {
        if (!PartyFormationController.TryGetConfirmedParty(out Hero[] prefabs)) return;
        if (!CanCreateParty(prefabs))
        {
            Debug.LogError("CombatPartyInitializer: Party State Manager, 비어 있는 Party Root, Spawn Point와 Hero 프리팹을 확인해야 함.", this);
            return;
        }

        // 비활성 부모 아래 생성하여 파티 등록 전 Hero의 Awake/OnEnable 실행을 막음.
        _partyRoot.gameObject.SetActive(false);
        Hero[] members = new Hero[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            members[i] = Instantiate(prefabs[i], _spawnPoint.position, _spawnPoint.rotation, _partyRoot);
            members[i].name = prefabs[i].name;
            members[i].gameObject.SetActive(true);
        }

        if (!_partyStateManager.TryInitializeParty(members))
        {
            foreach (Hero member in members) Destroy(member.gameObject);
            _partyRoot.gameObject.SetActive(true);
            Debug.LogError("CombatPartyInitializer: 생성된 Hero의 초기 파티 등록에 실패함.", this);
            return;
        }

        _partyRoot.gameObject.SetActive(true);
        HasAppliedFormation = true;
    }

    private void Start()
    {
        if (!HasAppliedFormation) return;
        if (_entryController == null ||
            !_entryController.TryBeginEntry(_partyStateManager.CurrentHero))
        {
            Debug.LogError("CombatPartyInitializer: 입장 연출 시작에 실패함.", this);
        }
    }

    /// <summary>
    /// 생성 대상과 씬 참조를 검사하여 기존 자식의 중복 생성과 잘못된 프리팹 사용을 방지함.
    /// </summary>
    private bool CanCreateParty(Hero[] prefabs)
    {
        if (_partyStateManager == null || _partyRoot == null || _spawnPoint == null ||
            _partyRoot.childCount != 0 || _partyRoot.gameObject.scene != gameObject.scene ||
            transform.IsChildOf(_partyRoot)) return false;

        for (int i = 0; i < prefabs.Length; i++)
            if (prefabs[i] == null || prefabs[i].gameObject.scene.IsValid() ||
                Array.IndexOf(prefabs, prefabs[i]) != i) return false;
        return true;
    }

    #endregion
}
