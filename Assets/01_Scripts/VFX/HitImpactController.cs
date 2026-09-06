using System;
using System.Collections;
using UnityEngine;

public class HitImpactController : MonoBehaviour
{
    private Action<HitImpactController> _returnToPool;

    [Header("Impact Flash")]
    [SerializeField] private Transform _flashTransform;
    [SerializeField] private MeshRenderer _flashRenderer;

    [Header("Playback")]
    [SerializeField] private float _duration = 0.12f;
    [SerializeField] private float _startScale = 0.3f;
    [SerializeField] private float _endScale = 1.4f;

    [Header("Camera Offset")]
    [SerializeField, Min(0f)]
    private float _cameraOffset = 0.05f;

    private static readonly int HitTextureID = Shader.PropertyToID("_HitTexture");
    private static readonly int HitColorID = Shader.PropertyToID("_HitColor");
    private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _playCoroutine;
    private Camera _mainCamera;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    public void Initialize(Action<HitImpactController> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    public void Play(HitImpactVFXDataSO data, Vector3 impactPoint, Vector3 impactDirection)
    {
        if (data == null || _flashTransform == null || _flashRenderer == null)
        {
            _returnToPool?.Invoke(this);
            return;
        }

        if (_playCoroutine != null) StopCoroutine(_playCoroutine);

        _mainCamera = Camera.main;
        transform.position = impactPoint;

        if (_mainCamera != null)
        {
            Vector3 directionToCamera =
                (_mainCamera.transform.position - impactPoint).normalized;

            transform.position +=
                directionToCamera * _cameraOffset;
        }

        // Hero별 VFX 데이터 적용
        _flashRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetTexture(HitTextureID, data.hitImpactTexture);
        _propertyBlock.SetColor(HitColorID, data.hitImpactColor);
        _propertyBlock.SetFloat(IntensityID, data.hitImpactIntensity);
        _propertyBlock.SetFloat(AlphaID, 1f);
        _flashRenderer.SetPropertyBlock(_propertyBlock);

        _flashTransform.localScale = Vector3.one * (_startScale * data.hitImpactScale);

        _playCoroutine = StartCoroutine(ExecutePlayRoutine(data.hitImpactScale));
    }

    private IEnumerator ExecutePlayRoutine(float impactScale)
    {
        float elapsedTime = 0f;

        while (elapsedTime < _duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / _duration);

            UpdateCameraFacing();

            float currentScale = Mathf.Lerp(_startScale, _endScale, normalizedTime) * impactScale;
            _flashTransform.localScale = Vector3.one * currentScale;

            _propertyBlock.SetFloat(AlphaID, 1f - normalizedTime);
            _flashRenderer.SetPropertyBlock(_propertyBlock);

            yield return null;
        }

        Debug.Log($"{gameObject.name} HitImpact 재생 종료");

        _playCoroutine = null;
        _returnToPool?.Invoke(this);
    }

    private void UpdateCameraFacing()
    {
        if (_mainCamera == null) return;

        Vector3 direction = _flashTransform.position - _mainCamera.transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        _flashTransform.rotation = Quaternion.LookRotation(direction);
    }

    private void OnDisable()
    {
        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
            _playCoroutine = null;
        }
    }
}