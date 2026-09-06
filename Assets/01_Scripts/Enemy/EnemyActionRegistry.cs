using UnityEngine;

/// <summary>
/// Enemy 공격 패턴의 현재 연계 모션과
/// 모션 내부 타수를 관리하는 컴포넌트
/// </summary>
public class EnemyActionRegistry : MonoBehaviour
{
    public int CurrentMotionIndex => _currentMotionIndex;
    public int CurrentHitIndex => _currentHitIndex;

    private int _currentMotionIndex;
    private int _currentHitIndex;

    /// <summary>
    /// 각 연계 모션 애니메이션 시작 지점에서 호출
    /// 새로운 모션이므로 HitIndex도 0으로 초기화
    /// </summary>
    public void SetMotionIndex(int motionIndex)
    {
        _currentMotionIndex = Mathf.Max(0, motionIndex);
        _currentHitIndex = 0;
    }

    /// <summary>
    /// 현재 HitIndex를 반환한 뒤 다음 타격을 위해 증가
    /// </summary>
    public int GetHitIndexAndAdvance()
    {
        int indexToReturn = _currentHitIndex;
        _currentHitIndex++;
        return indexToReturn;
    }

    public void ResetPatternSequence()
    {
        _currentMotionIndex = 0;
        _currentHitIndex = 0;
    }
}