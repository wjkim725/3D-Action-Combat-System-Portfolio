using UnityEngine;

[CreateAssetMenu(fileName = "HitMarkDataSO", menuName = "Scriptable Objects/HitMarkDataSO")]
public class HitMarkDataSO : ScriptableObject
{
    public Texture2D texture;
    public Color color = Color.white;
    public Vector2 size = Vector2.one;
    public float duration = 0.4f;
    public float fadeOutDuration = 0.2f;
    public float surfaceOffset = 0.025f;
    public float projectionDepth = 0.4f;    // Mesh를 얼마나 기게 관통해서 Decal 을 투사할지 정하는 필드
}
