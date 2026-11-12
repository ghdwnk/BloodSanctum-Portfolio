using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 근접 돌진 (기획서 13-1절 패턴 1)
    /// 플레이어 방향으로 빠르게 이동하면서 닿는 대상에게 피해를 준다
    /// </summary>
    [CreateAssetMenu(fileName = "New Charge Pattern", menuName = "Scriptable Objects/Boss Pattern/Charge")]
    public class ChargePatternData : BossPatternData
    {
        [Header("돌진")]
        [Tooltip("돌진 방향을 바닥에 보여 주는 준비 시간. 이 동안은 움직이지 않는다")]
        public float windUpTime = 0.6f;
        public float chargeSpeed = 14f;
        public float chargeDuration = 0.6f;
        [Tooltip("돌진 중 피해 판정 반경")]
        public float hitRadius = 2.5f;

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(ChargeRoutine(context));
        }

        private IEnumerator ChargeRoutine(BossPatternContext context)
        {
            // 돌진 방향은 시작 시점의 플레이어 위치로 고정한다 (계속 유도되면 회피가 불가능해진다)
            Vector3 direction = context.PlayerTransform.position - context.BossTransform.position;
            direction.y = 0f;
            direction.Normalize();

            context.BossTransform.rotation = Quaternion.LookRotation(direction);

            // 돌진이 지나갈 길을 폭 = 판정 지름으로 예고한다. 이동거리 + 끝의 판정 반경까지 보여 준다
            if (windUpTime > 0f)
            {
                BossTelegraphView telegraph = BossTelegraph.Line(context.BossTransform.position, direction,
                    chargeSpeed * chargeDuration + hitRadius, hitRadius * 2f, windUpTime);

                yield return new WaitForSeconds(windUpTime);

                if (telegraph != null)
                {
                    telegraph.Dismiss();
                }
            }

            // 돌진 이펙트는 준비가 끝나고 실제로 달려 나가는 순간에 터뜨린다 (예고 중에 미리 터지면 어색하다)
            SpawnEffect(effectPrefab, context.BossTransform.position, context.BossTransform.rotation);
            PlaySound(context, context.BossTransform.position);

            // 한 번의 돌진에서 같은 대상을 여러 번 때리지 않도록 기록한다
            bool hasHit = false;
            float elapsed = 0f;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;

                if (context.BossController != null && context.BossController.enabled)
                {
                    context.BossController.Move(direction * (chargeSpeed * Time.deltaTime));
                }

                if (!hasHit)
                {
                    Collider[] colliders = Physics.OverlapSphere(context.BossTransform.position, hitRadius, context.TargetLayer);
                    foreach (Collider collider in colliders)
                    {
                        Damageable damageable = collider.GetComponent<Damageable>();
                        if (damageable != null)
                        {
                            damageable.ReceiveDamage(context.AttackPower * damageMultiplier);
                            hasHit = true;
                        }
                    }
                }

                yield return null;
            }
        }
    }
}
