using System.Collections.Generic;
using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [Header("UI Settings")]
    [SerializeField] private RectTransform _damageTextLayer;

    [Header("Spawn Position Settings")]
    [SerializeField] private float _verticalSpawnOffset = 1.5f;
    [SerializeField, Min(0f)] private float _horizontalRandomOffset = 0.2f;
    [SerializeField, Min(0f)] private float _verticalRandomOffset = 0.1f;

    [Header("Pool Settings")]
    [SerializeField] private DamageText _damageTextPrefab;
    [SerializeField, Min(0)] private int _initialPoolSize = 30;

    // 현재 사용하지 않는 DamageText 인스턴스를 보관하는 풀
    private readonly Queue<DamageText> _poolQueue = new();

    // 현재 화면에 출력 중인 DamageText 인스턴스
    private readonly HashSet<DamageText> _activeTexts = new();

    // LateUpdate 순회 중 ActiveTexts 변경 방지를 위한 재사용 목록
    private readonly List<DamageText> _activeTextSnapshot = new();

    // DamageText 출력 차단 여부 : ChainAttack(Selecting) & Wipe Out 연출
    private bool _isOutputSuppressed;

    private Canvas _damageTextCanvas;
    private Camera _mainCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CacheUIReferences();
        InitializePool();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (_activeTexts.Count == 0) return;

        _activeTextSnapshot.Clear();
        _activeTextSnapshot.AddRange(_activeTexts);

        foreach (DamageText activeText in
                 _activeTextSnapshot)
        {
            if (activeText == null) continue;

            if (!TryGetCanvasPosition(
                activeText.WorldPosition,
                out Vector2 canvasPosition))
            {
                activeText.ForceReturnToPool();
                continue;
            }

            activeText.SetCanvasBasePosition(
                canvasPosition
            );
        }
    }

    /// <summary>
    /// DamageText 출력에 필요한 Canvas와 Camera 참조 초기화
    /// </summary>
    private void CacheUIReferences()
    {
        if (_damageTextLayer != null)
        {
            _damageTextCanvas =
                _damageTextLayer.GetComponentInParent<Canvas>();
        }

        _mainCamera = Camera.main;
    }

    /// <summary>
    /// 시작 시 지정된 수만큼 DamageText를 생성하여 풀에 보관
    /// </summary>
    private void InitializePool()
    {
        if (_damageTextPrefab == null)
        {
            Debug.LogError(
                "DamageTextManager: DamageText 프리팹이 지정되지 않았습니다."
            );

            return;
        }

        if (_damageTextLayer == null)
        {
            Debug.LogError(
                "DamageTextManager: DamageTextLayer가 지정되지 않았습니다."
            );

            return;
        }

        if (_damageTextCanvas == null)
        {
            Debug.LogError(
                "DamageTextManager: DamageTextLayer의 상위 Canvas를 찾을 수 없습니다."
            );

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
        DamageText newText = Instantiate(
            _damageTextPrefab,
            _damageTextLayer
        );

        newText.gameObject.SetActive(false);
        return newText;
    }

    /// <summary>
    /// 지정된 기준 위치를 바탕으로 데미지 텍스트를 출력
    /// 풀이 비어 있으면 새 인스턴스를 생성하여 자동 확장
    /// </summary>
    public void PopDamageText(
        Vector3 anchorPosition,
        int damageAmount,
        bool isCritical)
    {
        if (_isOutputSuppressed) return;

        if (_damageTextPrefab == null)
        {
            Debug.LogError(
                "DamageTextManager: DamageText 프리팹이 지정되지 않아 텍스트를 출력할 수 없습니다."
            );

            return;
        }

        Vector3 spawnPosition =
            GetDamageTextSpawnPosition(anchorPosition);

        if (!TryGetCanvasPosition(
            spawnPosition,
            out Vector2 canvasPosition))
        {
            return;
        }

        DamageText textInstance = _poolQueue.Count > 0
            ? _poolQueue.Dequeue()
            : CreateNewInstance();

        // ActiveTexts 에 등록
        _activeTexts.Add(textInstance);

        RectTransform textRectTransform =
            textInstance.RectTransform;

        textRectTransform.SetParent(
            _damageTextLayer,
            false
        );

        textRectTransform.anchoredPosition =
            canvasPosition;

        textRectTransform.localRotation =
            Quaternion.identity;

        textRectTransform.SetAsLastSibling();

        textInstance.gameObject.SetActive(true);
        textInstance.Setup(damageAmount, isCritical, spawnPosition);
    }

    /// <summary>
    /// 월드 좌표를 DamageTextLayer 기준 Canvas 좌표로 변환
    /// </summary>
    private bool TryGetCanvasPosition(
        Vector3 worldPosition,
        out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;

        if (_damageTextLayer == null ||
            _damageTextCanvas == null)
        {
            return false;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                return false;
            }
        }

        Vector3 viewportPosition =
            _mainCamera.WorldToViewportPoint(worldPosition);

        // 카메라 뒤에 있거나 화면 영역을 벗어난 위치는 출력하지 않음
        if (viewportPosition.z <= 0f ||
            viewportPosition.x < 0f ||
            viewportPosition.x > 1f ||
            viewportPosition.y < 0f ||
            viewportPosition.y > 1f)
        {
            return false;
        }

        Vector3 screenPosition =
            _mainCamera.WorldToScreenPoint(worldPosition);

        Camera canvasCamera =
            _damageTextCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : _damageTextCanvas.worldCamera;

        return RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                _damageTextLayer,
                screenPosition,
                canvasCamera,
                out canvasPosition
            );
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

        RectTransform textRectTransform =
            expiredText.RectTransform;

        textRectTransform.SetParent(
            _damageTextLayer,
            false
        );

        textRectTransform.anchoredPosition =
            Vector2.zero;

        textRectTransform.localRotation =
            Quaternion.identity;

        textRectTransform.localScale =
            Vector3.one;

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
        _activeTextSnapshot.Clear();
        _activeTextSnapshot.AddRange(_activeTexts);

        foreach (DamageText activeText in _activeTextSnapshot)
        {
            activeText?.ForceReturnToPool();
        }

        _activeTextSnapshot.Clear();
    }

    /// <summary>
    /// DamageText 생성 위치 계산
    /// </summary>
    private Vector3 GetDamageTextSpawnPosition(
        Vector3 anchorPosition)
    {
        // Vertical Position Offset 부여
        Vector3 spawnPosition =
            anchorPosition +
            Vector3.up * _verticalSpawnOffset;

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            return spawnPosition;
        }

        // 연속 타격 시 DamageText 좌우 분산
        spawnPosition +=
            _mainCamera.transform.right *
            Random.Range(
                -_horizontalRandomOffset,
                _horizontalRandomOffset
            );

        // 연속 타격 시 DamageText 상하 분산
        spawnPosition +=
            Vector3.up *
            Random.Range(
                -_verticalRandomOffset,
                _verticalRandomOffset
            );

        return spawnPosition;
    }
}