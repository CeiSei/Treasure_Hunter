using UnityEngine;

public enum StatType
{
    MoveSpeed,
    AttackPower,
    Defense,
    MaxHealth
}

[System.Serializable]
public class StatusEffect
{
    public StatType TargetStat;
    public float ModifierValue;
    
    // UI 표시를 위한 시간 데이터
    public float Duration;
    public float StartTime;
    
    public StatusEffect(StatType targetStat, float modifierValue, float duration)
    {
        TargetStat = targetStat;
        ModifierValue = modifierValue;
        Duration = duration;
        StartTime = Time.time; // 객체가 생성된(디버프가 걸린) 정확한 시점 기록
    }
}