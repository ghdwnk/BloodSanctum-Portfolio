using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 돌진 강타: 바라보는 방향으로 빠르게 돌진하며 지나간 경로 위의 모든 적을 베고 밀어낸다.
    /// 돌진이 끝난 자리에서 정면으로 한 번 더 훑어, 바로 앞에 있던 적도 확실히 맞힌다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Dash Strike Skill", menuName = "Skill/Dash Strike")]
    public class DashStrikeSkillData : SkillData
    {
        [Header("돌진 강타")]
        [Tooltip("돌진 거리(m)")]
        public float dashDistance = 4f;
        [Tooltip("돌진에 걸리는 시간(초)")]
        public float dashDuration = 0.2f;
        [Tooltip("1레벨 고정 피해. 실제 피해 = (이 값 + 공격력 x attackRatio) x 레벨 배율")]
        public float damage = 60f;
        [Tooltip("더할 플레이어 공격력 비율 (2 = 200%)")]
        public float attackRatio = 2f;
        [Tooltip("맞은 적을 밀어내는 거리(m)")]
        public float knockbackForce = 6f;
        [Tooltip("돌진 경로 주변 판정 반경(m). 이 안에 들어온 적은 모두 맞는다")]
        public float hitRadius = 1.5f;
        [Tooltip("돌진이 끝난 뒤 정면으로 더 훑는 거리(m)")]
        public float finishReach = 1.5f;

        public override string GetEffectText(int skillLevel)
        {
            float total = CalculateDamage(damage, attackRatio, GetCurrentPlayerAttack(), skillLevel);
            return $"데미지 {total:0} ({FormatRatio(damage, attackRatio)})";
        }

        public override void ApplyEffect(SkillCastContext context)
        {
            context.Caster.StartCoroutine(DashRoutine(context));
        }

        private IEnumerator DashRoutine(SkillCastContext context)
        {
            Transform playerTransform = context.PlayerTransform;
            CharacterController controller = context.PlayerController;

            // 한 번의 돌진에서 같은 적을 여러 번 때리지 않도록 기록한다 (포함 여부만 보므로 HashSet)
            HashSet<Damageable> hitTargets = new HashSet<Damageable>();
            float appliedDamage = CalculateDamage(damage, attackRatio, GetCasterAttack(context), context.SkillLevel);

            float elapsed = 0f;
            float speed = dashDistance / dashDuration;
            Vector3 previousPosition = playerTransform.position;

            // 몬스터는 루트에 CharacterController가 있어서, 그대로 돌진하면 첫 적에게 몸이 막히거나 옆으로 밀려
            // 막힌 지점까지만 훑게 된다. 돌진하는 동안만 적 레이어를 충돌에서 빼 적을 통과하며 지나가게 한다
            LayerMask originalExcludeLayers = controller.excludeLayers;
            controller.excludeLayers = originalExcludeLayers | context.TargetLayer;

            try
            {
                while (elapsed < dashDuration)
                {
                    float deltaTime = Time.deltaTime;
                    controller.Move(playerTransform.forward * (speed * deltaTime));
                    elapsed += deltaTime;

                    // 현재 위치만 검사하면 돌진 속도(초속 20m 이상)에서 적을 그냥 지나친다.
                    // 이전 프레임 위치와 현재 위치를 잇는 캡슐로 경로 전체를 훑는다
                    DamageAlong(previousPosition, playerTransform.position, context.TargetLayer, hitTargets, appliedDamage, context.PlayerStats);
                    previousPosition = playerTransform.position;

                    yield return null;
                }
            }
            finally
            {
                // 코루틴이 중간에 끊겨도(플레이어 사망·비활성화) 충돌 설정이 영영 남지 않게 되돌린다
                if (controller != null)
                {
                    controller.excludeLayers = originalExcludeLayers;
                }
            }

            Vector3 finishPoint = playerTransform.position + playerTransform.forward * finishReach;
            DamageAlong(playerTransform.position, finishPoint, context.TargetLayer, hitTargets, appliedDamage, context.PlayerStats);
        }

        // 두 지점을 잇는 캡슐 안의 적에게 피해와 넉백을 준다. 이미 맞은 적은 hitTargets로 거른다
        private void DamageAlong(Vector3 from, Vector3 to, LayerMask targetLayer, HashSet<Damageable> hitTargets, float appliedDamage, PlayerStats caster)
        {
            int count = Physics.OverlapCapsuleNonAlloc(from, to, hitRadius, HitBuffer, targetLayer);

            for (int ix = 0; ix < count; ++ix)
            {
                Collider target = HitBuffer[ix];
                Damageable damageable = target.GetComponent<Damageable>();

                if (damageable == null || !hitTargets.Add(damageable))
                {
                    continue;
                }

                bool isCritical = false;
                float finalDamage = caster != null ? caster.ApplyCritical(appliedDamage, out isCritical) : appliedDamage;
                damageable.ReceiveHit(finalDamage, from, 0f, isCritical);
                ApplyKnockback(target, from, knockbackForce);
            }
        }
    }
}
