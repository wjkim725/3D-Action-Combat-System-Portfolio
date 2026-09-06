using System.Collections;
using UnityEngine;
using TMPro;

public class DamageText : MonoBehaviour
{
    private TextMeshPro _textMesh;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 1f;          // 위로 떠오르는 속도
    [SerializeField] private float _disappearSpeed = 2f;     // 알파값이 사라지는 속도
    [SerializeField] private float _lifeTime = 0.5f;         // 텍스트가 유지되는 총 시간

    [Header("Color Settings")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _criticalColor = Color.yellow;

    private float _currentLifeTimer;
    private Color _textColor;

    private void Awake()
    {
        _textMesh = GetComponent<TextMeshPro>();
    }

    // 매니저(오브젝트 풀)에서 이 텍스트를 꺼내 쓸 때 호출해 줄 초기화 메서드
    // 데미지 수치와 크리티컬 여부를 받아 텍스트 내용과 색상을 세팅
    public void Setup(int damageAmount, bool isCritical)
    {
        _textMesh.text = damageAmount.ToString();

        // 크리티컬 여부에 따른 색상 및 크기 차별화
        if (isCritical)
        {
            _textColor = _criticalColor;
            _textMesh.fontSize = 5f; // 크리티컬은 더 크게
        }
        else
        {
            _textColor = _normalColor;
            _textMesh.fontSize = 3f; // 일반 타격 크기
        }
        
        _textMesh.color = _textColor;
        _currentLifeTimer = _lifeTime;

        // 활성화 시점에 연출 코루틴 가동
        StartCoroutine(TextActionRoutine());
    }

    // 텍스트가 살아있는 동안 매 프레임 위치를 물리적으로 위로 이동시키고
    // 시간이 지남에 따라 크기 조절 및 알파값을 흐리게 만들어 소멸 연출을 처리하는 코루틴
    private IEnumerator TextActionRoutine()
    {
        Vector3 startScale = Vector3.one * 0.5f;
        Vector3 peakScale = Vector3.one * 1.3f;
        Vector3 targetScale = Vector3.one * 1.0f;

        float elapsed = 0f;
        // 처음 아주 짧은 시간 동안 팝업 튀어나오는 손맛 연출 (0.1초 동안 팝업)
        float popDuration = 0.1f;

        while (_currentLifeTimer > 0)
        {
            float deltaTime = Time.unscaledDeltaTime;

            _currentLifeTimer -= deltaTime;
            elapsed += deltaTime;

            // 1. 위로 이동 처리
            transform.Translate(Vector3.up * _moveSpeed * Time.deltaTime);

            // 2. 크기 연출
            if (elapsed < popDuration)
            {
                transform.localScale = Vector3.Lerp(startScale, peakScale, elapsed / popDuration);
            }
            else
            {
                transform.localScale = Vector3.Lerp(peakScale, targetScale, (elapsed - popDuration) / (_lifeTime - popDuration));
            }

            // 절반 이상 시간이 지나면 서서히 투명화 (Fade Out)
            if (_currentLifeTimer < _lifeTime * 0.5f)
            {
                _textColor.a -= _disappearSpeed * Time.deltaTime;
                _textMesh.color = _textColor;
            }

            yield return null;
        }

        // 모든 연출이 끝나면 오브젝트를 가상 Destroy 처리
        DamageTextManager.Instance.ReturnToPool(this);
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