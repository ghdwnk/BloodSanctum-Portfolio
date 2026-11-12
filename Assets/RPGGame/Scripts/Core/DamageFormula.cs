using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 피해량 계산 공식을 한곳에 모아 둔 정적 클래스.
    ///
    /// 피해는 여러 경로(기본 공격, 스킬, 몬스터 공격, 투사체, 보스 패턴, 장판·화상)로 들어오지만
    /// 방어력 반영은 모두 Damageable을 거치며 이 공식 하나만 쓴다.
    /// 공식이 흩어져 있으면 밸런스를 고칠 때 한 곳을 빠뜨리게 되기 때문이다.
    /// </summary>
    public static class DamageFormula
    {
        /// <summary>
        /// 방어력이 이 값과 같아지면 피해가 절반이 된다.
        /// 플레이어 방어력이 1레벨 20에서 최대 레벨 68 정도라 100을 기준으로 잡았다.
        /// </summary>
        public const float DefenseConstant = 100f;

        /// <summary>방어력이 아무리 높아도 원래 피해의 이 비율만큼은 들어간다.</summary>
        public const float MinimumRatio = 0.1f;

        /// <summary>
        /// 방어력을 반영한 최종 피해량을 정수 값으로 돌려준다.
        ///
        /// 최종 피해 = 피해 × (DefenseConstant / (DefenseConstant + 방어력))
        /// 뺄셈(피해 - 방어력)이 아니라 비율 감소를 쓰는 이유: 뺄셈은 방어력이 공격력을 따라잡는 순간
        /// 피해가 0이 되어 전투가 성립하지 않는다. 비율 감소는 방어력이 높을수록 효과가 완만하게 줄어든다.
        /// </summary>
        /// <param name="damage">방어력을 반영하기 전의 피해량</param>
        /// <param name="defense">피격자의 방어력 (0 이하면 감소 없음)</param>
        /// <returns>1 이상의 정수 피해량. 원래 피해가 0 이하면 0</returns>
        public static float Apply(float damage, float defense)
        {
            if (damage <= 0f)
            {
                return 0f;
            }

            float reduced = defense > 0f
                ? damage * (DefenseConstant / (DefenseConstant + defense))
                : damage;

            // 방어력이 아무리 높아도 최소 피해는 보장한다
            reduced = Mathf.Max(reduced, damage * MinimumRatio);

            // 데미지 숫자 표시와 실제 체력 감소가 어긋나지 않도록 여기서 한 번만 정수로 내린다.
            // 내림 때문에 0이 되면 "0 피해"가 뜨므로 최소 1로 올린다
            return Mathf.Max(1f, Mathf.Floor(reduced));
        }
    }
}
