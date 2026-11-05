using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프에 포함할 불변 능력치 효과 하나를 보관함.
/// </summary>
public readonly struct BuffEffect
{
    public BuffType Type { get; }
    public float Value { get; }
    public BuffValueMode ValueMode { get; }

    #region [초기화]

    /// <summary>
    /// 효과 종류와 증가량을 검증함. 비율 수치 30은 30%를 뜻함.
    /// </summary>
    public BuffEffect(BuffType type, float value, BuffValueMode valueMode = BuffValueMode.Flat)
    {
        if (!Enum.IsDefined(typeof(BuffType), type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(typeof(BuffValueMode), valueMode)) throw new ArgumentOutOfRangeException(nameof(valueMode));
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
        Type = type;
        Value = value;
        ValueMode = valueMode;
    }

    #endregion
}

/// <summary>
/// 하나의 Hero에 적용할 능력치 효과와 유지 시간을 보관함.
/// </summary>
public class Buff
{
    public IReadOnlyList<BuffEffect> Effects { get; }
    public BuffDurationMode DurationMode { get; }
    public float Duration { get; }
    public float RemainTime { get; private set; }
    public bool IsFinished => DurationMode == BuffDurationMode.Timed && RemainTime <= 0f;
    internal HeroStatus Owner { get; private set; }

    #region [초기화 및 수명 관리]

    /// <summary>
    /// 효과를 고정하고 시간제 또는 외부 유지형 버프를 생성함. 비율 수치 30은 30%를 뜻함.
    /// </summary>
    public Buff(BuffType type, float value, float duration = 0f,
        BuffValueMode valueMode = BuffValueMode.Flat,
        BuffDurationMode durationMode = BuffDurationMode.Timed)
        : this(new[] { new BuffEffect(type, value, valueMode) }, duration, durationMode)
    {
    }

    /// <summary>
    /// 여러 효과를 복사해 하나의 소유자와 유지 시간을 공유하는 버프를 생성함.
    /// </summary>
    public Buff(IEnumerable<BuffEffect> effects, float duration = 0f,
        BuffDurationMode durationMode = BuffDurationMode.Timed)
    {
        if (effects == null) throw new ArgumentNullException(nameof(effects));
        if (!Enum.IsDefined(typeof(BuffDurationMode), durationMode)) throw new ArgumentOutOfRangeException(nameof(durationMode));
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f ||
            (durationMode == BuffDurationMode.Timed && duration <= 0f)) throw new ArgumentOutOfRangeException(nameof(duration));

        var copiedEffects = new List<BuffEffect>(effects);
        if (copiedEffects.Count == 0) throw new ArgumentException("버프 효과가 하나 이상 필요함.", nameof(effects));
        Effects = copiedEffects.AsReadOnly();
        DurationMode = durationMode;
        Duration = duration;
        RemainTime = duration;
    }

    /// <summary>
    /// 등록한 소유자를 지정하고 재사용 시 유지 시간을 초기화함.
    /// </summary>
    internal void Attach(HeroStatus owner)
    {
        Owner = owner;
        RemainTime = Duration;
    }

    /// <summary>
    /// 등록 해제 후 소유자 참조를 비움.
    /// </summary>
    internal void Detach()
    {
        Owner = null;
    }

    /// <summary>
    /// 시간제 버프의 남은 시간을 게임 시간만큼 감소시킴.
    /// </summary>
    internal void Tick(float deltaTime)
    {
        if (DurationMode != BuffDurationMode.Timed) return;
        RemainTime = Mathf.Max(0f, RemainTime - deltaTime);
    }

    #endregion

    #region [적용 및 해제 콜백]

    /// <summary>
    /// 능력치에 반영된 뒤 추가 적용 처리를 실행함.
    /// </summary>
    public virtual void OnApplyBuff(HeroStatus status) { }

    /// <summary>
    /// 능력치에서 제거된 뒤 추가 해제 처리를 실행함.
    /// </summary>
    public virtual void OnRemoveBuff(HeroStatus status) { }

    #endregion
}
