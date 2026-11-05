using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class HitMarkController : MonoBehaviour
{
    [Header("Hit Mark")]
    [SerializeField] private DecalProjector _decalProjector;

    private static readonly int HitTextureID = Shader.PropertyToID("_HitTexture");
    private static readonly int HitColorID = Shader.PropertyToID("_HitColor");

    private Material _runtimeMaterial;
    private Coroutine _lifetimeRoutine;
    private Action<HitMarkController> _returnToPool;
    private Transform _followTarget;
    private Vector3 _localPosition;
    private Quaternion _localRotation;
    private Vector3 _localScale;
    private bool _isPlaying;

    #region [초기화와 풀 반환]

    private void Awake()
    {
        if (_decalProjector == null)
            _decalProjector = GetComponent<DecalProjector>();

        if (_decalProjector == null || _decalProjector.material == null) return;

        _runtimeMaterial = new Material(_decalProjector.material);
        _decalProjector.material = _runtimeMaterial;
    }

    /// <summary>
    /// 인스턴스가 수명을 마치면 호출할 풀 반환 경로를 연결함.
    /// </summary>
    public void Initialize(Action<HitMarkController> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    /// <summary>
    /// 수명과 추적 상태를 해제하고 전용 머티리얼을 보존한 채 비활성화함.
    /// </summary>
    public void ResetForPool()
    {
        _isPlaying = false;
        _followTarget = null;
        if (_lifetimeRoutine != null) StopCoroutine(_lifetimeRoutine);
        _lifetimeRoutine = null;
        if (_decalProjector != null) _decalProjector.fadeFactor = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 재생 중인 인스턴스를 한 번만 풀에 반환함.
    /// </summary>
    private void FinishPlayback()
    {
        if (!_isPlaying) return;
        _isPlaying = false;
        if (_returnToPool != null) _returnToPool(this);
        else ResetForPool();
    }

    private void OnDisable()
    {
        FinishPlayback();
    }

    #endregion

    #region [데칼 재생과 대상 추적]

    /// <summary>
    /// 타격 데이터를 적용하고 적의 상대 위치를 추적하며 데칼을 재생함.
    /// </summary>
    public bool Play(
        HitMarkDataSO data,
        Transform target,
        Vector3 impactPoint,
        Vector3 surfaceNormal,
        Vector3 slashDirection,
        Vector3 localScale)
    {
        if (data == null || target == null || !target.gameObject.activeInHierarchy ||
            _decalProjector == null || _runtimeMaterial == null) return false;
        if (surfaceNormal.sqrMagnitude < 0.0001f || slashDirection.sqrMagnitude < 0.0001f) return false;

        surfaceNormal.Normalize();
        slashDirection.Normalize();

        // 적의 자식으로 두지 않아 대상 파괴 시에도 풀 객체를 보존함.
        _followTarget = target;
        _localPosition = target.InverseTransformPoint(impactPoint + surfaceNormal * data.surfaceOffset);
        _localRotation = Quaternion.Inverse(target.rotation) * Quaternion.LookRotation(-surfaceNormal, slashDirection);
        _localScale = localScale;
        UpdateFollowTransform();

        float projectionDepth = Mathf.Max(0.01f, data.projectionDepth);

        _decalProjector.size = new Vector3(data.size.x, data.size.y, projectionDepth);
        _decalProjector.pivot = new Vector3(0f, 0f, projectionDepth * 0.5f);
        _decalProjector.fadeFactor = 1f;

        _runtimeMaterial.SetTexture(HitTextureID, data.texture);
        _runtimeMaterial.SetColor(HitColorID, data.color);

        if (_lifetimeRoutine != null)
            StopCoroutine(_lifetimeRoutine);

        _isPlaying = true;
        gameObject.SetActive(true);
        _lifetimeRoutine = StartCoroutine(ExecuteLifetime(data.duration, data.fadeOutDuration));
        return true;
    }

    private void LateUpdate()
    {
        if (!_isPlaying) return;
        if (_followTarget == null || !_followTarget.gameObject.activeInHierarchy)
        {
            FinishPlayback();
            return;
        }
        UpdateFollowTransform();
    }

    /// <summary>
    /// 풀 루트를 유지하며 기존 적 자식 배치의 위치와 회전 및 크기를 따라감.
    /// </summary>
    private void UpdateFollowTransform()
    {
        transform.SetPositionAndRotation(_followTarget.TransformPoint(_localPosition),
            _followTarget.rotation * _localRotation);
        Vector3 worldScale = Vector3.Scale(_followTarget.lossyScale, _localScale);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            Mathf.Abs(parentScale.x) > 0.0001f ? worldScale.x / parentScale.x : 0f,
            Mathf.Abs(parentScale.y) > 0.0001f ? worldScale.y / parentScale.y : 0f,
            Mathf.Abs(parentScale.z) > 0.0001f ? worldScale.z / parentScale.z : 0f);
    }

    /// <summary>
    /// 유지 시간과 페이드아웃이 끝나면 데칼을 풀에 반환함.
    /// </summary>
    private IEnumerator ExecuteLifetime(float duration, float fadeOutDuration)
    {
        float clampedDuration = Mathf.Max(0.01f, duration);
        float clampedFadeOut = Mathf.Clamp(fadeOutDuration, 0f, clampedDuration);
        float visibleDuration = clampedDuration - clampedFadeOut;

        if (visibleDuration > 0f)
            yield return new WaitForSeconds(visibleDuration);

        if (clampedFadeOut > 0f)
        {
            float elapsed = 0f;

            while (elapsed < clampedFadeOut)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / clampedFadeOut);
                _decalProjector.fadeFactor = 1f - t;
                yield return null;
            }
        }

        _decalProjector.fadeFactor = 0f;
        _lifetimeRoutine = null;
        FinishPlayback();
    }

    #endregion

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }
}