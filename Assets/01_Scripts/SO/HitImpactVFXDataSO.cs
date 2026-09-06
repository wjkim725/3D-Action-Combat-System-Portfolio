using UnityEngine;

[CreateAssetMenu(fileName = "HitImpactVFXDataSO", menuName = "Scriptable Objects/HitImpactVFXDataSO")]
public class HitImpactVFXDataSO  : ScriptableObject
{
    [Header("Hit Impact")]
    public Texture2D hitImpactTexture;
    public Color hitImpactColor = Color.white;
    public float hitImpactIntensity = 3f;
    public float hitImpactScale = 1f;
}
