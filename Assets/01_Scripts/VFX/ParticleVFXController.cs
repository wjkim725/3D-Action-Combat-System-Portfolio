using System;
using System.Collections;
using UnityEngine;

public class ParticleVFXController : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] _particleSystems;

    private Action<ParticleVFXController> _returnToPool;
    private Coroutine _playCoroutine;

    private void Awake()
    {
        _particleSystems =
        GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Initialize(
        Action<ParticleVFXController> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    public void Play(Vector3 position, Vector3 direction)
    {
        if (_particleSystems == null ||
            _particleSystems.Length == 0)
        {
            _returnToPool?.Invoke(this);
            return;
        }

        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
        }

        Quaternion rotation =
            direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized)
                : Quaternion.identity;

        transform.SetPositionAndRotation(position, rotation);

        StopAndClearParticles();

        foreach (ParticleSystem particleSystem in _particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Play(false);
        }

        _playCoroutine = StartCoroutine(WaitForCompletion());
    }

    private IEnumerator WaitForCompletion()
    {
        yield return null;

        while (IsAnyParticleAlive())
        {
            yield return null;
        }

        _playCoroutine = null;
        _returnToPool?.Invoke(this);
    }

    private bool IsAnyParticleAlive()
    {
        foreach (ParticleSystem particleSystem in _particleSystems)
        {
            if (particleSystem != null &&
                particleSystem.IsAlive(false))
            {
                return true;
            }
        }

        return false;
    }

    private void StopAndClearParticles()
    {
        foreach (ParticleSystem particleSystem in _particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    private void OnDisable()
    {
        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
            _playCoroutine = null;
        }

        StopAndClearParticles();
    }
}