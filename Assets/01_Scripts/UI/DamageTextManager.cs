using System.Collections.Generic;
using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [Header("Pool Settings")]
    [SerializeField] private DamageText _damageTextPrefab;
    [SerializeField, Min(0)] private int _initialPoolSize = 30;

    // 현재 사용하지 않는 DamageText 인스턴스를 보관하는 풀
    private readonly Queue<DamageText> _poolQueue = new();

    // 현재 화면에 출력 중인 DamageText 인스턴스
    private readonly HashSet<DamageText> _activeTexts = new();

    // DamageText 출력 차단 여부 : ChainAttack(Selecting) & Wipe Out 연출
    private bool _isOutputSuppressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializePool();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 시작 시 지정된 수만큼 DamageText를 생성하여 풀에 보관
    /// </summary>
    private void InitializePool()
    {
        if (_damageTextPrefab == null)
        {
            Debug.LogError("DamageTextManager: DamageText 프리팹이 지정되지 않았습니다.");
            return;
        }

        for (int i = 0; i < _initialPoolSize; i++)
        {
            _poolQueue.Enqueue(CreateNewInstance());
        }
    }

    /// <summary>
    /// 새로운 DamageText 인스턴스를 생성
    /// 풀 등록 여부는 호출한 메서드에서 결정
    /// </summary>
    private DamageText CreateNewInstance()
    {
        DamageText newText = Instantiate(_damageTextPrefab, transform);
        // UnityEngine.Object.Instantiate(_damageTextPrefab, transform);
        newText.gameObject.SetActive(false);
        return newText;
    }

    /// <summary>
    /// 지정된 월드 위치에 데미지 텍스트를 출력
    /// 풀이 비어 있으면 새 인스턴스를 생성하여 자동 확장
    /// </summary>
    public void PopDamageText(Vector3 worldPosition, int damageAmount, bool isCritical)
    {
        if (_isOutputSuppressed) return;

        if (_damageTextPrefab == null)
        {
            Debug.LogError("DamageTextManager: DamageText 프리팹이 지정되지 않아 텍스트를 출력할 수 없습니다.");
            return;
        }

        DamageText textInstance = _poolQueue.Count > 0
            ? _poolQueue.Dequeue() : CreateNewInstance();

        // ActiveTexts 에 등록
        _activeTexts.Add(textInstance);

        textInstance.transform.position = worldPosition;

        // 월드 공간 UI가 현재 카메라를 바라보도록 회전
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            textInstance.transform.rotation = mainCamera.transform.rotation;
        }

        textInstance.gameObject.SetActive(true);
        textInstance.Setup(damageAmount, isCritical);
    }

    /// <summary>
    /// 연출이 끝난 DamageText를 PoolQueue로 반환
    /// + ActiveTexts에서 제거
    /// </summary>
    public void ReturnToPool(DamageText expiredText)
    {
        if (expiredText == null) return;
        if (_poolQueue.Contains(expiredText)) return;

        _activeTexts.Remove(expiredText);

        // DamageText 에서 비활성화 하지 않고 Manager에서 처리
        expiredText.gameObject.SetActive(false);
        expiredText.transform.SetParent(transform);

        _poolQueue.Enqueue(expiredText);
    }

    /// <summary>
    /// DamageText 신규 출력 차단 및 현재 출력 중인 텍스트 정리
    /// ChainAttack(Selecting) & Wipe Out 연출에서 호출
    /// </summary>
    public void SetOutputSuppressed(bool isSuppressed)
    {
        if (_isOutputSuppressed == isSuppressed) return;

        _isOutputSuppressed = isSuppressed;

        if (_isOutputSuppressed)
        {
            ClearActiveDamageTexts();
        }
    }

    /// <summary>
    /// 현재 출력 중인 DamageText 즉시 풀 반환
    /// </summary>
    private void ClearActiveDamageTexts()
    {
        if (_activeTexts.Count == 0) return;

        // 순회 중 HashSet 변경 방지를 위한 복사
        List<DamageText> activeTextSnapshot = new List<DamageText>(_activeTexts);

        foreach (DamageText activeText in activeTextSnapshot)
        {
            activeText?.ForceReturnToPool();
        }
    }
}