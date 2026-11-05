using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확정한 위치에서 설치 연출과 지연 타격을 실행하고 잔여 연출이 끝나면 반환함.
/// </summary>
[DisallowMultipleComponent]
public class PlacedAreaController : MonoBehaviour
{
    [Header("Optional VFX")]
    [SerializeField] private Transform _placementVFXRoot;
    [SerializeField] private Transform _impactVFXRoot;

    [Tooltip("첫 타격을 기다리지 않고 설치 즉시 발동 VFX와 SFX를 재생함")]
    [SerializeField] private bool _playImpactVFXOnPlacement = false;

    [Header("Optional Audio")]
    [SerializeField] private AudioCueSO _placementCue;
    [SerializeField] private AudioCueSO _activationCue;

    [Header("VFX Tail")]
    [Tooltip("마지막 타격 후 잔여 파티클을 기다리는 최대 시간")]
    [SerializeField, Min(0f)] private float _maxVFXTailTime = 5f;

    private ParticleSystem[] _placementParticles;
    private ParticleSystem[] _impactParticles;
    private readonly List<CombatEventContext> _hitContexts = new();
    private readonly List<float> _hitTimes = new();
    private int _nextHitIndex;
    private Vector3 _hitPosition;
    private Vector3 _fallbackDirection;
    private LayerMask _targetLayerMask;
    private float _elapsedTime;
    private float _elapsedTailTime;
    private Action<PlacedAreaController> _returnToPool;

    #region [초기화]

    private void Awake()
    {
        CacheParticles();
    }

    /// <summary>
    /// 비활성 자식을 포함한 선택적 연출 그룹의 파티클을 확보함.
    /// </summary>
    private void CacheParticles()
    {
        _placementParticles = _placementVFXRoot != null
            ? _placementVFXRoot.GetComponentsInChildren<ParticleSystem>(true)
            : Array.Empty<ParticleSystem>();
        _impactParticles = _impactVFXRoot != null
            ? _impactVFXRoot.GetComponentsInChildren<ParticleSystem>(true)
            : Array.Empty<ParticleSystem>();
    }

    /// <summary>
    /// 독립적인 공격 정보를 보관하고 설치 연출을 시작함. 반환값은 적중이 아닌 설치 성공을 뜻함.
    /// </summary>
    public bool Initialize(
        IReadOnlyList<CombatEventContext> hitContexts, IReadOnlyList<float> hitTimes,
        LayerMask targetLayerMask)
    {
        ResetForPool();
        if (!IsValidHitSequence(hitContexts, hitTimes))
        {
            Debug.LogError($"[{name}] 설치형 공격 정보 또는 시간 설정이 올바르지 않습니다.", this);
            return false;
        }

        // 대상별 판정이 Context를 변경하므로 설치 인스턴스와 타격마다 별도 정보를 소유함.
        for (int i = 0; i < hitContexts.Count; i++)
        {
            CombatEventContext context = hitContexts[i];
            _hitContexts.Add(new CombatEventContext(
                context.Attacker, context.HitDetectionType, context.HitRadius,
                context.DamageMultiplier, context.DazeMultiplier, context.HitType,
                context.CanExtendGroggy, context.GroggyExtendTime,
                context.PayBackEnergy, context.PayBackDecibel, context.CanTriggerChainAttack,
                context.HitMarkData, context.ReactionDirectionMode, context.AttackType,
                hitImpactSFX: context.HitImpactSFX, comboIndex: context.ComboIndex));
            _hitTimes.Add(hitTimes[i]);
        }
        _targetLayerMask = targetLayerMask;
        _hitPosition = transform.position;
        _fallbackDirection = transform.forward;

        gameObject.SetActive(true);
        PlayParticles(_placementVFXRoot, _placementParticles);
        PlayPlacementSFX();
        // 선행 연출이 필요한 프리팹은 피해 판정 전에 발동 VFX와 SFX를 시작함.
        if (_playImpactVFXOnPlacement)
        {
            PlayActivationEffects();
        }

        ExecutePendingHits();

        return true;
    }

    /// <summary>
    /// 시간과 반경에 사용할 유한한 0 이상의 값인지 확인함.
    /// </summary>
    private bool IsFiniteNonNegative(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 전체 타격과 실행 시간이 유효하고 같은 공격자의 순차 일정인지 확인함.
    /// </summary>
    private bool IsValidHitSequence(
        IReadOnlyList<CombatEventContext> hitContexts, IReadOnlyList<float> hitTimes)
    {
        if (hitContexts == null || hitTimes == null || hitContexts.Count == 0 ||
            hitContexts.Count != hitTimes.Count || !IsFiniteNonNegative(_maxVFXTailTime)) return false;

        float previousTime = 0f;
        for (int i = 0; i < hitContexts.Count; i++)
        {
            CombatEventContext context = hitContexts[i];
            if (context == null || context.Attacker == null ||
                context.Attacker.AttackerTransform == null ||
                context.HitDetectionType != HitDetectionType.PlacedArea ||
                !IsFiniteNonNegative(context.HitRadius) ||
                !IsFiniteNonNegative(hitTimes[i]) || hitTimes[i] < previousTime) return false;
            if (!ReferenceEquals(context.Attacker, hitContexts[0].Attacker)) return false;
            previousTime = hitTimes[i];
        }
        return true;
    }

    #endregion

    #region [공격 진행]

    private void Update()
    {
        if (_hitContexts.Count == 0) return;

        if (_nextHitIndex < _hitContexts.Count)
        {
            _elapsedTime += Time.deltaTime;
            ExecutePendingHits();
            return;
        }

        _elapsedTailTime += Time.deltaTime;
        if (_elapsedTailTime >= _maxVFXTailTime ||
            (!IsAnyParticleAlive(_placementParticles) && !IsAnyParticleAlive(_impactParticles)))
        {
            FinishPlacedArea();
        }
    }

    /// <summary>
    /// 실행 시간이 지난 타격을 순서대로 처리하여 프레임 지연에도 타격을 누락하지 않음.
    /// </summary>
    private void ExecutePendingHits()
    {
        while (_nextHitIndex < _hitContexts.Count && _elapsedTime >= _hitTimes[_nextHitIndex])
        {
            int hitIndex = _nextHitIndex++;
            ExecuteHit(hitIndex);
            if (!gameObject.activeInHierarchy || _hitContexts.Count == 0) return;
        }
    }

    /// <summary>
    /// 첫 타격에서 설치 연출을 정리하고 설정에 따라 발동 VFX와 SFX를 시작하며,
    /// 각 타격의 Impulse와 범위 판정을 실행함.
    /// </summary>
    private void ExecuteHit(int hitIndex)
    {
        CombatEventContext context = _hitContexts[hitIndex];
        if (hitIndex == 0)
        {
            StopParticles(_placementParticles, ParticleSystemStopBehavior.StopEmitting);
            // 설치 시 이미 시작한 발동 VFX와 SFX는 첫 타격에서 다시 재생하지 않음.
            if (!_playImpactVFXOnPlacement)
            {
                PlayActivationEffects();
            }
        }

        // Hero의 현재 모션이나 온필드 여부와 관계없이 각 타격 발동 시 요청함.
        CombatPresentationManager.Instance?.PlayHitImpulse(context.HitType);

        // Hero 상태나 활성 여부를 재조회하지 않으며, 공격자 파괴 시에는 연출만 완료함.
        UnityEngine.Object attackerObject = context.Attacker as UnityEngine.Object;
        if (attackerObject == null) return;

        bool isHitSuccess = AreaHitUtility.Execute(
            context, _hitPosition, context.HitRadius, _targetLayerMask, _fallbackDirection);
        if (isHitSuccess)
        {
            CombatManager.Instance.PayBackCombatResource(context);
        }
    }

    #endregion

    #region [발동 및 파티클 연출]

    /// <summary>
    /// 설정된 발동 시점에 파티클과 효과음을 함께 재생함.
    /// </summary>
    private void PlayActivationEffects()
    {
        PlayParticles(_impactVFXRoot, _impactParticles);
        PlayActivationSFX();
    }

    /// <summary>
    /// 지정 그룹을 활성화하고 각 파티클을 중복 재귀 호출 없이 재생함.
    /// </summary>
    private void PlayParticles(Transform root, ParticleSystem[] particles)
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        foreach (ParticleSystem particle in particles)
        {
            if (particle != null && particle.gameObject.activeInHierarchy) particle.Play(false);
        }
    }

    /// <summary>
    /// 지정 그룹의 파티클 방출을 중단하거나 풀 재사용을 위해 완전히 비움.
    /// </summary>
    private void StopParticles(ParticleSystem[] particles, ParticleSystemStopBehavior behavior)
    {
        if (particles == null) return;
        foreach (ParticleSystem particle in particles)
        {
            if (particle != null) particle.Stop(false, behavior);
        }
    }

    /// <summary>
    /// 활성 그룹에 살아 있는 파티클이 남아 있는지 확인함.
    /// </summary>
    private bool IsAnyParticleAlive(ParticleSystem[] particles)
    {
        foreach (ParticleSystem particle in particles)
        {
            if (particle != null && particle.gameObject.activeInHierarchy && particle.IsAlive(false)) return true;
        }
        return false;
    }

    #endregion

    #region [SFX 재생]

    /// <summary>
    /// 유효한 설치가 완료된 위치에서 설치 효과음을 한 번 재생함.
    /// </summary>
    private void PlayPlacementSFX()
    {
        if (!isActiveAndEnabled || _placementCue == null) return;

        AudioManager.Instance?.PlayOneShot(_placementCue, transform.position);
    }

    /// <summary>
    /// 적중 여부와 관계없이 발동 위치에서 효과음을 한 번 재생함.
    /// </summary>
    private void PlayActivationSFX()
    {
        if (!isActiveAndEnabled || _activationCue == null) return;

        AudioManager.Instance?.PlayOneShot(_activationCue, transform.position);
    }

    #endregion

    #region [풀 반환]

    /// <summary>
    /// 생성한 풀의 반환 경로를 연결함.
    /// </summary>
    public void SetPoolReturnHandler(Action<PlacedAreaController> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    /// <summary>
    /// 이전 공격과 파티클 정보를 비우고 비활성 대기 상태로 복원함.
    /// </summary>
    public void ResetForPool()
    {
        if (_placementParticles == null || _impactParticles == null) CacheParticles();
        ClearExecution();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 타이머와 공격 참조를 해제하고 연출 그룹을 초기화함.
    /// </summary>
    private void ClearExecution()
    {
        _hitContexts.Clear();
        _hitTimes.Clear();
        _nextHitIndex = 0;
        _elapsedTime = 0f;
        _elapsedTailTime = 0f;
        _hitPosition = Vector3.zero;
        _fallbackDirection = Vector3.zero;
        _targetLayerMask = 0;
        StopParticles(_placementParticles, ParticleSystemStopBehavior.StopEmittingAndClear);
        StopParticles(_impactParticles, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_placementVFXRoot != null) _placementVFXRoot.gameObject.SetActive(false);
        if (_impactVFXRoot != null) _impactVFXRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 실행을 종료하고 연결된 풀에 반환하며 독립 생성인 경우 비활성화함.
    /// </summary>
    private void FinishPlacedArea()
    {
        if (_returnToPool != null) _returnToPool(this);
        else ResetForPool();
    }

    private void OnDisable()
    {
        ClearExecution();
    }

    #endregion
}
