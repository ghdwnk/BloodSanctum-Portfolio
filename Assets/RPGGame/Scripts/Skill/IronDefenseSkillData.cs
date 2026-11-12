using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 강철 방어: 일정 시간 동안 방어력을 높여 받는 피해를 줄인다.
    /// 버프는 PlayerStats의 일시 보너스(ApplyTemporaryBonus)로 넣는다. Damageable의 방어력을 직접 바꾸면
    /// PlayerStats가 스탯을 다시 계산해 넣을 때 버프가 지워지기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Iron Defense Skill", menuName = "Skill/Iron Defense")]
    public class IronDefenseSkillData : SkillData
    {
        [Header("강철 방어")]
        [Tooltip("1레벨 방어력 증가량. 레벨 배율이 곱해진다")]
        public float bonusDefense = 15f;
        [Tooltip("지속시간(초)")]
        public float buffDuration = 5f;

        public override string GetEffectText(int skillLevel)
        {
            return $"방어력 +{bonusDefense * GetPowerMultiplier(skillLevel):0} · 지속 {buffDuration:0.#}초";
        }

        public override void ApplyEffect(SkillCastContext context)
        {
            if (context.PlayerStats == null)
            {
                return;
            }

            float leveledDefense = bonusDefense * GetPowerMultiplier(context.SkillLevel);
            context.PlayerStats.ApplyTemporaryBonus(PlayerStats.StatType.Defense, leveledDefense, buffDuration);
        }
    }
}
