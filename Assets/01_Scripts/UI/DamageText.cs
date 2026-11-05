using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(TextMeshProUGUI))]
public class DamageText : MonoBehaviour
{
    private RectTransform _rectTransform;
    private TextMeshProUGUI _textMesh;

    public RectTransform RectTransform => _rectTransform;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 120f;        // 위로 떠오르는 속도
    [SerializeField] private float _disappearSpeed = 2f;     // 알파값이 사라지는 속도
    [SerializeField] private float _lifeTime = 0.5f;         // 텍스트가 유지되는 총 시간

    [Header("Scale Settings")]
    [SerializeField] private float _startScale = 0.5f;
    [SerializeField] private float _peakScale = 1.3f;
    [SerializeField] private float _targetScale = 1f;
    [SerializeField] private float _popDuration = 0.1f;

    [Header("Font Size Settings")]
    [SerializeField] private float _normalFontSize = 48f;
    [SerializeField] private float _criticalFontSize = 96f;

    public Vector3 WorldPosition { get; private set; }  // 타격 순간 확정된 고정 월드 위치

    private Vector2 _canvasBasePosition;                // 월드 위치를 현재 카메라 기준 Canvas 좌표로 변환한 값
    private Vector2 _screenRiseOffset;                  // DamageText가 위로 상승한 화면상 거리


    [Header("Color Settings")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _criticalColor = Color.yellow;

    private float _currentLifeTimer;
    private Color _textColor;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _textMesh = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 매니저(오브젝트 풀)에서 이 텍스트를 꺼내 쓸 때 호출해 줄 초기화 메서드
    /// 데미지 수치와 크리티컬 여부를 받아 텍스트 내용과 색상을 세팅
    /// </summary>
    public void Setup(
    int damageAmount,
    bool isCritical,
    Vector3 worldPosition)
    {
        StopAllCoroutines();

        WorldPosition = worldPosition;

        _canvasBasePosition =
            _rectTransform.anchoredPosition;

        _screenRiseOffset =
            Vector2.zero;

        _textMesh.text = damageAmount.ToString();

        // 크리티컬 여부에 따른 색상 및 크기 차별화
        if (isCritical)
        {
            _textColor = _criticalColor;
            _textMesh.fontSize = _criticalFontSize;
        }
        else
        {
            _textColor = _normalColor;
            _textMesh.fontSize = _normalFontSize;
        }

        _textColor.a = 1f;
        _textMesh.color = _textColor;

        _rectTransform.localScale =
            Vector3.one * _startScale;

        _currentLifeTimer = _lifeTime;

        UpdateAnchoredPosition();

        // 활성화 시점에 연출 코루틴 가동
        StartCoroutine(TextActionRoutine());
    }

    /// <summary>
    /// 월드 위치를 변환한 Canvas 기준 위치 갱신
    /// </summary>
    public void SetCanvasBasePosition(
        Vector2 canvasBasePosition)
    {
        _canvasBasePosition =
            canvasBasePosition;

        UpdateAnchoredPosition();
    }

    /// <summary>
    /// Canvas 기준 위치와 상승 Offset을 최종 위치에 적용
    /// </summary>
    private void UpdateAnchoredPosition()
    {
        _rectTransform.anchoredPosition =
            _canvasBasePosition +
            _screenRiseOffset;
    }

    /// <summary>
    /// 텍스트가 살아있는 동안 매 프레임 위치를 위로 이동시키고
    /// 시간이 지남에 따라 크기 조절 및 알파값을 흐리게 만들어 소멸 연출을 처리하는 코루틴
    /// </summary>
    private IEnumerator TextActionRoutine()
    {
        float elapsed = 0f;

        while (_currentLifeTimer > 0f)
        {
            float deltaTime = Time.unscaledDeltaTime;

            _currentLifeTimer -= deltaTime;
            elapsed += deltaTime;

            // 위로 이동 처리
            _screenRiseOffset += Vector2.up * _moveSpeed * deltaTime;

            UpdateAnchoredPosition();

            // 크기 연출
            UpdateScale(elapsed);

            // 절반 이상 시간이 지나면 서서히 투명화 (Fade Out)
            if (_currentLifeTimer < _lifeTime * 0.5f)
            {
                _textColor.a = Mathf.MoveTowards(
                    _textColor.a,
                    0f,
                    _disappearSpeed * deltaTime
                );

                _textMesh.color = _textColor;
            }

            yield return null;
        }

        // 모든 연출이 끝나면 오브젝트를 가상 Destroy 처리
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ReturnToPool(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 경과 시간에 따른 팝업 크기 연출
    /// </summary>
    private void UpdateScale(float elapsed)
    {
        float currentScale;

        if (_popDuration > 0f && elapsed < _popDuration)
        {
            float popRatio = elapsed / _popDuration;

            currentScale = Mathf.Lerp(
                _startScale,
                _peakScale,
                popRatio
            );
        }
        else
        {
            float settleDuration = Mathf.Max(
                _lifeTime - _popDuration,
                Mathf.Epsilon
            );

            float settleRatio = Mathf.Clamp01(
                (elapsed - _popDuration) / settleDuration
            );

            currentScale = Mathf.Lerp(
                _peakScale,
                _targetScale,
                settleRatio
            );
        }

        _rectTransform.localScale =
            Vector3.one * currentScale;
    }

    /// <summary>
    /// 진행 중인 연출을 중단하고 즉시 풀 반환
    /// </summary>
    public void ForceReturnToPool()
    {
        StopAllCoroutines();

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ReturnToPool(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}