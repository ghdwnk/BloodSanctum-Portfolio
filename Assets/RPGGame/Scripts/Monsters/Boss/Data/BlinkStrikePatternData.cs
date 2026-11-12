using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 순간이동 일격
    /// 플레이어가 서 있던 자리를 예고하고, 시간이 되면 보스가 그 옆으로 순간이동해 내려친다.
    /// 예고 위치는 시작 시점에 고정되므로 움직이면 피할 수 있다
    /// </summary>
    [CreateAssetMenu(fileName = "New Blink Strike Pattern", menuName = "Scriptable Objects/Boss Pattern/Blink Strike")]
    public class BlinkStrikePatternData : BossPatternData
    {
        [Header("순간이동 일격")]
        public float windUpTime = 0.9f;
        public float strikeRadius = 3f;
        [Tooltip("도착 지점에서 보스가 선 위치(예고 중심에서 보스가 오던 쪽으로 떨어진 거리)")]
        public float arriveDistance = 2f;
        [Header("먼 거리 접근")]
        [Tooltip("플레이어가 일격 사거리(maxRange)보다 멀어도 이 거리까지는 순간이동으로 거리를 좁힌다. 한 번에 이동하는 길이는 maxRange 그대로이고, 일격 사거리 안에 들어올 때까지 반복해서 쓴다")]
        public float approachMaxDistance = 80f;
        [Tooltip("먼 거리 접근 순간이동을 준비하는 시간(초). 이 동안 사라지는 이펙트만 보이고 피해는 없다")]
        public float approachWindUpTime = 0.6f;

        [Tooltip("사라질 때와 나타날 때 보스 위치에 생성할 이펙트")]
        public GameObject blinkEffectPrefab;
        [Tooltip("도착한 뒤 재생할 공격 애니메이션. 시전 시작 때가 아니라 도착 직후에 재생해야 내려치는 모습이 맞는다")]
        public string strikeAnimationTrigger = "MeleeSlash";

        // 일격 사거리(maxRange)보다 먼 곳까지 접근용으로 쓸 수 있다
        public override bool IsUsableAt(float distance)
        {
            return distance >= minRange && distance <= Mathf.Max(maxRange, approachMaxDistance);
        }

        public override float MaxUsableRange => Mathf.Max(maxRange, approachMaxDistance);

        public override void Execute(BossPatternContext context)
        {
            Vector3 toPlayer = context.PlayerTransform.position - context.BossTransform.position;
            toPlayer.y = 0f;

            // 일격이 닿는 거리보다 멀면 내려치지 않고 플레이어 쪽으로 거리만 좁힌다
            if (toPlayer.magnitude > maxRange)
            {
                context.Caster.StartCoroutine(ApproachRoutine(context));
                return;
            }

            context.Caster.StartCoroutine(StrikeRoutine(context));
        }

        // 먼 거리 접근: 사라지는 이펙트 → 플레이어 쪽으로 maxRange만큼(남은 거리가 minRange 밑으로 줄지 않게) 이동
        private IEnumerator ApproachRoutine(BossPatternContext context)
        {
            SpawnEffect(blinkEffectPrefab, context.BossTransform.position + Vector3.up * 0.2f, Quaternion.identity);

            yield return new WaitForSeconds(approachWindUpTime);

            // 대기 중 플레이어가 움직였으므로 도착 직전의 위치로 다시 계산한다
            Vector3 from = context.BossTransform.position;
            Vector3 toPlayer = context.PlayerTransform.position - from;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance < 0.01f)
            {
                yield break;
            }

            Vector3 direction = toPlayer / distance;
            float travel = Mathf.Clamp(distance - minRange, 0f, maxRange);
            Vector3 arrival = from + direction * travel;
            arrival.y = FindGroundY(arrival, from.y);

            Util.TeleportCharacter(context.BossTransform, arrival, Quaternion.LookRotation(direction));
            PlaySound(context, arrival);
            SpawnEffect(blinkEffectPrefab, arrival + Vector3.up * 0.2f, Quaternion.identity);
        }

        // 도착 지점의 바닥 높이. 오르막·내리막을 건너도 허공이나 땅속에 서지 않게 위에서 아래로 쏴서 찾고, 못 찾으면 원래 높이를 쓴다
        private static float FindGroundY(Vector3 position, float fallbackY)
        {
            Vector3 origin = new Vector3(position.x, fallbackY + 3f, position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return fallbackY;
        }

        private IEnumerator StrikeRoutine(BossPatternContext context)
        {
            Vector3 target = context.PlayerTransform.position;
            BossTelegraphView telegraph = BossTelegraph.Circle(target, strikeRadius, windUpTime);

            yield return new WaitForSeconds(windUpTime);

            if (telegraph != null)
            {
                telegraph.Dismiss();
            }

            SpawnEffect(blinkEffectPrefab, context.BossTransform.position + Vector3.up * 0.2f, Quaternion.identity);

            // 보스가 오던 쪽(예고 중심 기준)에 도착해 중심을 바라본다
            Vector3 fromBoss = context.BossTransform.position - target;
            fromBoss.y = 0f;
            fromBoss = fromBoss.sqrMagnitude < 0.0001f ? -context.BossTransform.forward : fromBoss.normalized;

            Vector3 arrival = target + fromBoss * arriveDistance;
            arrival.y = context.BossTransform.position.y;

            Util.TeleportCharacter(context.BossTransform, arrival, Quaternion.LookRotation(-fromBoss));
            PlaySound(context, arrival);
            SpawnEffect(blinkEffectPrefab, arrival + Vector3.up * 0.2f, Quaternion.identity);
            (context.Caster as BossStateManager)?.PlayPatternAnimation(strikeAnimationTrigger);

            SpawnEffect(effectPrefab, target, Quaternion.identity);
            DamageInSphere(target, strikeRadius, context.TargetLayer, context.AttackPower * damageMultiplier);
        }
    }
}
