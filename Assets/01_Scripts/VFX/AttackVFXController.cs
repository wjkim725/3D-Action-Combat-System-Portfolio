using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 판정 없이 공격 파티클을 재생하고 종료 시 반환을 요청함.
/// </summary>
public class AttackVFXController : MonoBehaviour
{
    [Header("VFX Lifetime")]
    [SerializeField, Min(0.1f)] private float _maxLifetime = 8f;
    private ParticleSystem[] _particles;
    private readonly List<ParticleSystem> _entryParticles = new();
    private Action<AttackVFXController> _returnToPool;
    private Coroutine _playCoroutine;

    private void Awake()
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        HashSet<ParticleSystem> subEmitters = new();
        foreach (ParticleSystem particle in _particles)
        {
            var sub = particle.subEmitters;
            for (int i = 0; i < sub.subEmittersCount; i++)
                subEmitters.Add(sub.GetSubEmitterSystem(i));
        }
        foreach (ParticleSystem particle in _particles)
            if (!subEmitters.Contains(particle)) _entryParticles.Add(particle);
    }

    /// <summary>
    /// 재생 완료 시 사용할 풀 반환 경로를 등록함.
    /// </summary>
    public void Initialize(Action<AttackVFXController> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    /// <summary>
    /// 지정 위치·회전에서 재생하며 Sub Emitter는 원본의 발생 이벤트에 맡김.
    /// </summary>
    public void Play(Vector3 position, Quaternion rotation)
    {
        ResetPlayback();
        transform.SetPositionAndRotation(position, rotation);
        gameObject.SetActive(true);
        foreach (ParticleSystem particle in _entryParticles)
            if (particle != null && particle.gameObject.activeInHierarchy) particle.Play(false);
        _playCoroutine = StartCoroutine(WaitForCompletion());
    }

    /// <summary>
    /// 지연 방출과 잔여 입자가 끝날 때까지 기다리고 최대 수명에서 반환함.
    /// </summary>
    private IEnumerator WaitForCompletion()
    {
        float elapsed = 0f;
        yield return null;
        while (elapsed < _maxLifetime && IsAlive())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _playCoroutine = null;
        ResetPlayback();
        if (_returnToPool != null) _returnToPool(this);
        else gameObject.SetActive(false);
    }

    /// <summary>
    /// 지연 재생을 포함해 아직 종료되지 않은 파티클이 있는지 확인함.
    /// </summary>
    private bool IsAlive()
    {
        foreach (ParticleSystem particle in _particles)
            if (particle != null && particle.gameObject.activeInHierarchy && particle.IsAlive(false)) return true;
        return false;
    }

    /// <summary>
    /// 재사용과 중간 비활성화를 위해 재생 감시와 모든 입자를 정리함.
    /// </summary>
    private void ResetPlayback()
    {
        if (_playCoroutine != null) StopCoroutine(_playCoroutine);
        _playCoroutine = null;
        if (_particles == null) return;
        foreach (ParticleSystem particle in _particles)
            if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        ResetPlayback();
    }
}
