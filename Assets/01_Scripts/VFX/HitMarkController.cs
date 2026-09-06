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

    private void Awake()
    {
        if (_decalProjector == null)
            _decalProjector = GetComponent<DecalProjector>();

        if (_decalProjector == null || _decalProjector.material == null) return;

        _runtimeMaterial = new Material(_decalProjector.material);
        _decalProjector.material = _runtimeMaterial;
    }

    public void Play(
        HitMarkDataSO data,
        Vector3 impactPoint,
        Vector3 surfaceNormal,
        Vector3 slashDirection)
    {
        if (data == null || _decalProjector == null || _runtimeMaterial == null) return;
        if (surfaceNormal.sqrMagnitude < 0.0001f || slashDirection.sqrMagnitude < 0.0001f) return;

        surfaceNormal.Normalize();
        slashDirection.Normalize();

        transform.position = impactPoint + surfaceNormal * data.surfaceOffset;
        transform.rotation = Quaternion.LookRotation(-surfaceNormal, slashDirection);

        float projectionDepth = Mathf.Max(0.01f, data.projectionDepth);

        _decalProjector.size = new Vector3(data.size.x, data.size.y, projectionDepth);
        _decalProjector.pivot = new Vector3(0f, 0f, projectionDepth * 0.5f);
        _decalProjector.fadeFactor = 1f;

        _runtimeMaterial.SetTexture(HitTextureID, data.texture);
        _runtimeMaterial.SetColor(HitColorID, data.color);

        if (_lifetimeRoutine != null)
            StopCoroutine(_lifetimeRoutine);

        _lifetimeRoutine = StartCoroutine(ExecuteLifetime(data.duration, data.fadeOutDuration));
    }

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
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }
}