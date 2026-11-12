using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 궁극기 화염 폭발: 시전자 앞에서 거대한 화염 돔이 터져 범위 안의 모든 적에게 큰 피해와 넉백을 주고,
    /// 그 자리에 불길(잔불)이 남아 잠시 더 피해를 준다. 위력이 큰 대신 마나·쿨타임이 무겁고 요구 레벨이 있다.
    /// 폭발 이펙트는 vfx 배열로, 잔불 이펙트는 판정 시간에 맞춰 여기서 직접 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Ultimate Blast Skill", menuName = "Skill/Ultimate Blast")]
    public class UltimateBlastSkillData : SkillData
    {
        [Header("화염 폭발")]
        [Tooltip("폭발 반경(m)")]
        public float radius = 8f;
        [Tooltip("시전자 앞 몇 m에서 터뜨릴지. 폭발 이펙트 위치는 vfx[0]의 localOffset.z가 정하므로 이 값을 바꾸면 그것도 같이 맞춘다")]
        public float castDistance = 5.5f;
        [Tooltip("폭발 1레벨 고정 피해. 피해 = (이 값 + 공격력 x attackRatio) x 레벨 배율")]
        public float damage = 280f;
        [Tooltip("더할 플레이어 공격력 비율")]
        public float attackRatio = 4.4f;
        [Tooltip("맞은 적을 밀어내는 거리(m)")]
        public float knockbackForce = 6f;
        [Tooltip("모션이 땅을 내려치기까지의 대기 시간(초)")]
        public float windupDelay = 0.67f;

        [Header("잔불")]
        [Tooltip("폭발 뒤 불길이 남는 시간(초). 0이면 잔불 없음")]
        public float burnDuration = 3f;
        [Tooltip("잔불 피해 간격(초)")]
        public float burnTickInterval = 1f;
        [Tooltip("잔불 반경(m). 폭발보다 좁게 잡아 중심부만 타게 한다")]
        public float burnRadius = 5f;
        [Tooltip("잔불 틱의 1레벨 고정 피해")]
        public float burnTickDamage = 25f;
        [Tooltip("잔불 틱에 더할 플레이어 공격력 비율")]
        public float burnAttackRatio = 0.6f;
        [Tooltip("잔불이 타는 동안 바닥에 깔 불길 이펙트. 비워 두면 판정만 남는다")]
        public GameObject emberVfxPrefab;
        [Tooltip("불길 이펙트 프리팹이 기준으로 만들어진 반경. 실제 잔불 반경과의 비율만큼 이펙트 크기를 맞춘다")]
        public float emberVfxBaseRadius = 8.5f;

        public override string GetEffectText(int skillLevel)
        {
            float attack = GetCurrentPlayerAttack();
            float blast = CalculateDamage(damage, attackRatio, attack, skillLevel);
            string text = $"데미지 {blast:0} ({FormatRatio(damage, attackRatio)})";

            if (burnDuration <= 0f)
            {
                return text;
            }

            float burn = CalculateDamage(burnTickDamage, burnAttackRatio, attack, skillLevel);
            return $"{text} + 잔불 {burn:0}/{burnTickInterval:0.#}초";
        }

        public override void ApplyEffect(SkillCastContext context)
        {
            context.Caster.StartCoroutine(BlastRoutine(context));
        }

        private IEnumerator BlastRoutine(SkillCastContext context)
        {
            yield return new WaitForSeconds(windupDelay);

            Transform player = context.PlayerTransform;
            if (player == null)
            {
                yield break;
            }

            // windupDelay가 지난 시점의 위치·방향을 기준으로 해서 이펙트와 판정이 같은 자리를 쓴다
            Vector3 center = player.position + player.forward * castDistance;
            float playerAttack = GetCasterAttack(context);
            float blastDamage = CalculateDamage(damage, attackRatio, playerAttack, context.SkillLevel);

            int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, context.TargetLayer);
            for (int ix = 0; ix < count; ++ix)
            {
                DamageTarget(HitBuffer[ix], blastDamage, context.PlayerStats);
                ApplyKnockback(HitBuffer[ix], center, knockbackForce);
            }

            if (burnDuration <= 0f)
            {
                yield break;
            }

            float burnDamage = CalculateDamage(burnTickDamage, burnAttackRatio, playerAttack, context.SkillLevel);
            SkillDamageZone.Spawn(center, burnRadius, burnDuration, burnTickInterval, burnDamage, context.TargetLayer);

            // 피해를 주는 바닥이 눈에 보이도록 같은 반경·같은 시간 동안 불길을 깐다
            if (emberVfxPrefab != null)
            {
                GameObject embers = Instantiate(emberVfxPrefab, center + Vector3.up * 0.05f, Quaternion.identity);
                embers.transform.localScale = Vector3.one * (burnRadius / Mathf.Max(0.1f, emberVfxBaseRadius));
                TimedSkillVfx.Attach(embers, burnDuration);
            }
        }
    }
}
