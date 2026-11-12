using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 지면 충격파 (기획서 13-1절 패턴 2)
    /// 준비 동작을 취한 뒤 보스 주변에 광역 피해를 준다
    /// </summary>
    [CreateAssetMenu(fileName = "New Shockwave Pattern", menuName = "Scriptable Objects/Boss Pattern/Shockwave")]
    public class ShockwavePatternData : BossPatternData
    {
        [Header("충격파")]
        [Tooltip("내려찍는 모션이 지면에 닿을 때까지의 대기 시간 (애니메이션과 타이밍을 맞춘다)")]
        public float windUpTime = 0.6f;
        public float shockwaveRadius = 6f;
        [Tooltip("판정 중심을 보스 정면으로 얼마나 밀지 (0이면 보스 중심 = 전방위 충격파, 값을 주면 전방 근접 베기처럼 앞쪽만 때린다)")]
        public float forwardOffset = 0f;
        [Tooltip("바닥에 공격 범위 예고를 띄울지. 기본 공격은 끄고, 스킬만 켠다")]
        public bool showTelegraph = true;

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(ShockwaveRoutine(context));
        }

        private IEnumerator ShockwaveRoutine(BossPatternContext context)
        {
            // 판정 중심은 시전 시작 시점에 정해 바닥에 미리 보여 준다.
            // 준비 동작 동안에는 피해가 없으니 플레이어는 표시를 보고 범위 밖으로 피할 수 있다
            Vector3 center = context.BossTransform.position + context.BossTransform.forward * forwardOffset;
            BossTelegraphView telegraph = showTelegraph ? BossTelegraph.Circle(center, shockwaveRadius, windUpTime) : null;

            yield return new WaitForSeconds(windUpTime);

            if (telegraph != null)
            {
                telegraph.Dismiss();
            }

            SpawnEffect(effectPrefab, center, context.BossTransform.rotation);
            PlaySound(context, center);
            DamageInSphere(center, shockwaveRadius, context.TargetLayer, context.AttackPower * damageMultiplier);
        }
    }
}
