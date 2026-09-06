using NUnit.Framework;
using UnityEngine;

// 버프 클래스 정의
// 추후 상속받는 자식 클래스로 상세한 버프 구현?
public class Buff
{
    public BuffType Type { get; protected set; }
    public float Value { get; protected set; }
    public float Duration { get; protected set; }
    public float RemainTime { get; set; }

    public bool IsFinished => RemainTime <= 0f;

    public Buff(BuffType type, float value, float duration)
    {
        Type = type;
        Value = value;
        Duration = duration;
        RemainTime = duration;
    }

    // 매 프레임 버프의 타이머를 줄이는 공용 메서드
    public virtual void Tick(float deltaTime)
    {
        RemainTime -= deltaTime;
    }

    // 버프가 켜질 때, 꺼질 때 실행할 특별한 로직이 있다면 상속용으로 개방
    public virtual void OnApplyBuff(HeroStatus status) { }
    public virtual void OnRemoveBuff(HeroStatus status) { }
}
