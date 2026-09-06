using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 현재는 Hero 본체가 아닌 HeroSwapSystem 에서 참조하고 있음. 해당 구조 고민필요.
public class HeroDitherController : MonoBehaviour
{
    [SerializeField] private float _fadeDuration = 0.2f;

    private Hero _hero;
    private HeroWeapon _weapon;
    private readonly List<Renderer> _targetRenderers = new();

    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _fadeCoroutine;
    private float _currentFade = 1f;

    private static readonly int FadeID = Shader.PropertyToID("_Fade");

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        _weapon = GetComponent<HeroWeapon>();
        _propertyBlock = new MaterialPropertyBlock();

        CacheRenderers();
    }

    // Dither 적용 대상 Renderer 캐싱
    private void CacheRenderers()
    {
        _targetRenderers.Clear();

        if (_hero.BodyRenderers != null)
        {
            foreach (var renderer in _hero.BodyRenderers)
                AddRenderer(renderer);
        }

        if (_weapon != null)
        {
            foreach (var renderer in _weapon.AllWeaponRenderers)
                AddRenderer(renderer);
        }
    }

    private void AddRenderer(Renderer renderer)
    {
        if (renderer == null || _targetRenderers.Contains(renderer)) return;
        _targetRenderers.Add(renderer);
    }

    public void FadeIn(Action onComplete = null)
    {
        StartFade(1f, onComplete);
    }

    public void FadeOut(Action onComplete = null)
    {
        StartFade(0f, onComplete);
    }

    public void SetFadeImmediate(float fade)
    {
        StopFade();
        ApplyFade(Mathf.Clamp01(fade));
    }

    private void StartFade(float targetFade, Action onComplete)
    {
        StopFade();
        _fadeCoroutine = StartCoroutine(FadeRoutine(targetFade, onComplete));
    }

    private IEnumerator FadeRoutine(float targetFade, Action onComplete)
    {
        float startFade = _currentFade;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);

            ApplyFade(Mathf.Lerp(startFade, targetFade, t));
            yield return null;
        }

        ApplyFade(targetFade);
        _fadeCoroutine = null;
        onComplete?.Invoke();
    }

    private void ApplyFade(float fade)
    {
        _currentFade = fade;

        foreach (var renderer in _targetRenderers)
        {
            if (renderer == null) continue;

            _propertyBlock.Clear();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(FadeID, _currentFade);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void StopFade()
    {
        if (_fadeCoroutine == null) return;

        StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = null;
    }
}
