using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 분노의 함성: 일정 시간 동안 자신의 공격력을 높인다.
    /// 버프는 PlayerStats의 일시 보너스(ApplyTemporaryBonus)로 넣는다. 무기나 Damageable 값을 직접 바꾸면
    /// 장비 교체·레벨업으로 스탯을 다시 계산할 때 버프가 지워지기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "New War Cry Skill", menuName = "Skill/War Cry")]
    public class WarCrySkillData : SkillData
    {
        [Header("분노의 함성")]
        [Tooltip("1레벨 공격력 배율. 1.9면 +90%이고, 증가분(0.9)에 레벨 배율이 곱해진다")]
        public float attackMultiplier = 1.5f;
        [Tooltip("지속시간(초)")]
        public float buffDuration = 8f;

        public override string GetEffectText(int skillLevel)
        {
            float bonusPercent = GetBonusRatio(skillLevel) * 100f;
            return $"공격력 +{bonusPercent:0}% · 지속 {buffDuration:0.#}초";
        }

        public override void ApplyEffect(SkillCastContext context)
        {
            if (context.PlayerStats == null)
            {
                return;
            }

            // 시전 순간의 공격력을 기준으로 증가량을 정해 고정값으로 더한다
            float bonusAttack = context.PlayerStats.Attack * GetBonusRatio(context.SkillLevel);
            context.PlayerStats.ApplyTemporaryBonus(PlayerStats.StatType.Attack, bonusAttack, buffDuration);
        }

        // 레벨에 따른 공격력 증가 비율. 지금 에셋 값(1.9배, 레벨당 0.35)이면 1레벨 +90% → 4레벨 약 +185%
        private float GetBonusRatio(int skillLevel)
        {
            return (attackMultiplier - 1f) * GetPowerMultiplier(skillLevel);
        }
    }
}
