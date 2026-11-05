using UnityEngine;

[CreateAssetMenu(fileName = "HitMarkDataSO", menuName = "Scriptable Objects/HitMarkDataSO")]
public class HitMarkDataSO : ScriptableObject
{
    [Header("Hit Mark Type")]
    public HitMarkType hitMarkType = HitMarkType.Decal;

    [Header("Lifetime")]
    public float duration = 0.4f;

    [Header("Decal")]
    public Texture2D texture;
    public Color color = Color.white;
    public Vector2 size = Vector2.one;
    public float fadeOutDuration = 0.2f;
    public float surfaceOffset = 0.025f;
    public float projectionDepth = 0.4f; // 메시 안쪽으로 데칼을 투사하는 깊이

    [Header("Arrow")]
    [Tooltip("화살촉이 원점이고 +Z가 진행 방향인 외형 전용 프리팹")]
    public GameObject arrowPrefab;

    [Min(0f)]
    [Tooltip("피격점에서 화살 진행 방향으로 밀어 넣는 깊이. 월드 단위")]
    public float penetrationDepth = 0.08f;
}
