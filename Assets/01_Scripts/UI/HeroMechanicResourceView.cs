using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터의 전용 자원을 타입에 맞는 스택, 게이지, 토글로 표시함.
/// </summary>
public class HeroMechanicResourceView : MonoBehaviour
{
    [Header("Resource Groups")]
    [SerializeField] private RectTransform _stackRoot;
    [SerializeField] private GameObject _gaugeRoot;
    [SerializeField] private GameObject _toggleRoot;

    [Header("Stack")]
    [SerializeField] private Image[] _stackFills;
    [SerializeField] private Vector2 _stackCellSize = new Vector2(32f, 24f);
    [SerializeField] private float _stackSpacing = 4f;
    private int _visibleStackCount = -1;
    private float _stackAreaWidth = -1f;

    [Header("Gauge / Toggle")]
    [SerializeField] private Image _gaugeFill;
    [SerializeField] private Image _toggleFill;

    #region [자원 표시]
    /// <summary>
    /// 전달받은 자원 타입과 수치로 하나의 표시 그룹만 갱신함.
    /// </summary>
    public void RefreshResource(HeroMechanicResourceType resourceType, float current, float maximum)
    {
        bool isStack = resourceType == HeroMechanicResourceType.Stack && maximum > 0f;
        bool isGauge = resourceType == HeroMechanicResourceType.Gauge && maximum > 0f;
        bool isToggle = resourceType == HeroMechanicResourceType.Toggle;

        SetGroupActive(_stackRoot != null ? _stackRoot.gameObject : null, isStack);
        SetGroupActive(_gaugeRoot, isGauge);
        SetGroupActive(_toggleRoot, isToggle);

        if (isStack)
        {
            RefreshStack(Mathf.Clamp(current, 0f, maximum), Mathf.CeilToInt(maximum));
        }
        else if (isGauge && _gaugeFill != null)
        {
            _gaugeFill.fillAmount = Mathf.Clamp01(current / maximum);
        }
        else if (isToggle && _toggleFill != null)
        {
            _toggleFill.enabled = current > 0f;
        }
    }

    /// <summary>
    /// 정수 단위별 스택과 마지막 칸의 부분 충전량을 표시함.
    /// </summary>
    private void RefreshStack(float current, int count)
    {
        if (_stackRoot == null || _stackFills == null || _stackFills.Length == 0) return;

        EnsureStackCapacity(count);
        float areaWidth = _stackRoot.rect.width;
        if (_visibleStackCount != count || !Mathf.Approximately(_stackAreaWidth, areaWidth))
        {
            float totalWidth = count * _stackCellSize.x + (count - 1) * _stackSpacing;
            float scale = totalWidth > 0f ? Mathf.Min(1f, areaWidth / totalWidth) : 1f;
            float startX = (areaWidth - totalWidth * scale) * 0.5f;

            for (int i = 0; i < _stackFills.Length; i++)
            {
                RectTransform cell = (RectTransform)_stackFills[i].transform.parent;
                cell.gameObject.SetActive(i < count);
                cell.sizeDelta = _stackCellSize * scale;
                cell.anchoredPosition = new Vector2(startX + i * (_stackCellSize.x + _stackSpacing) * scale, 0f);
            }

            _visibleStackCount = count;
            _stackAreaWidth = areaWidth;
        }

        for (int i = 0; i < count; i++)
        {
            _stackFills[i].fillAmount = Mathf.Clamp01(current - i);
        }
    }

    /// <summary>
    /// 준비된 칸보다 최대 스택이 많으면 기존 StackCell과 Fill 구조를 복제함.
    /// </summary>
    private void EnsureStackCapacity(int count)
    {
        if (count <= _stackFills.Length) return;

        var fills = new List<Image>(_stackFills);
        Transform template = _stackFills[0].transform.parent;
        while (fills.Count < count)
        {
            Transform cell = Instantiate(template, _stackRoot, false);
            cell.name = $"StackCell_{fills.Count + 1:00}";
            fills.Add(cell.Find("Fill").GetComponent<Image>());
        }

        _stackFills = fills.ToArray();
    }

    /// <summary>
    /// 활성 상태가 달라질 때만 표시 그룹을 전환함.
    /// </summary>
    private void SetGroupActive(GameObject group, bool active)
    {
        if (group != null && group.activeSelf != active) group.SetActive(active);
    }
    #endregion
}
