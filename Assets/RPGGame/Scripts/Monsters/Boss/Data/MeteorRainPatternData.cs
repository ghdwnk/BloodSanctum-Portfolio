using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 낙하 마법진
    /// 플레이어 주변 여러 곳에 마법진이 차례로 나타났다가 터진다. 첫 번째는 플레이어 발밑을 노리고,
    /// 나머지는 주변에 흩어져 한 곳에 머무르지 못하게 한다
    /// </summary>
    [CreateAssetMenu(fileName = "New Meteor Rain Pattern", menuName = "Scriptable Objects/Boss Pattern/Meteor Rain")]
    public class MeteorRainPatternData : BossPatternData
    {
        public override bool IsGroundZone => true;

        [Header("낙하")]
        public int count = 6;
        public float radius = 2.6f;
        [Tooltip("플레이어 주변 흩어지는 반경")]
        public float scatterRadius = 7f;
        [Tooltip("예고가 뜬 뒤 터지기까지의 시간")]
        public float delay = 1.1f;
        [Tooltip("마법진이 하나씩 생기는 간격")]
        public float spawnInterval = 0.3f;

        [Header("낙하 연출")]
        [Tooltip("하늘에서 떨어지는 운석(불덩이) 모습. 예고가 차오르는 시간(delay) 동안 떨어져 바닥에 닿는 순간 폭발한다")]
        public GameObject fallPrefab;
        public float fallHeight = 24f;
        [Tooltip("비스듬히 떨어지도록 출발 지점을 옆으로 얼마나 밀지")]
        public float fallSideOffset = 6f;

        [Header("화상")]
        [Tooltip("운석에 맞으면 피해와 별개로 걸리는 화상. 여러 발 맞아도 쌓이지 않고 지속 시간만 갱신된다")]
        public float burnDuration = 5f;
        public float burnTickInterval = 1f;
        [Tooltip("틱 한 번의 피해는 보스 공격력 x 이 값")]
        public float burnDamageMultiplier = 0.2f;
        [Tooltip("화상 중인 대상에게 붙일 이펙트 (비워 두면 표시 없음)")]
        public GameObject burnEffectPrefab;

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(RainRoutine(context));
        }

        private IEnumerator RainRoutine(BossPatternContext context)
        {
            for (int ix = 0; ix < count; ++ix)
            {
                if (context.PlayerTransform == null)
                {
                    yield break;
                }

                Vector3 center = context.PlayerTransform.position;
                if (ix > 0)
                {
                    Vector2 offset = Random.insideUnitCircle * scatterRadius;
                    center += new Vector3(offset.x, 0f, offset.y);
                }

                BossTelegraphView telegraph = BossTelegraph.Circle(center, radius, delay);
                context.Caster.StartCoroutine(ExplodeRoutine(context, center, telegraph));

                if (fallPrefab != null)
                {
                    SpawnFall(center);
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // 운석을 하늘에서 예고 지점까지 떨어뜨린다. 닿는 시각이 폭발 시각(delay)과 같도록 이동 시간을 delay로 맞춘다.
        // 이동은 오브젝트가 스스로 한다 (보스 코루틴이 끊겨도 하늘에 멈춰 남지 않게)
        private void SpawnFall(Vector3 target)
        {
            Vector2 side = Random.insideUnitCircle.normalized;
            Vector3 start = target + Vector3.up * fallHeight + new Vector3(side.x, 0f, side.y) * fallSideOffset;

            GameObject meteor = Instantiate(fallPrefab, start, Quaternion.LookRotation((target - start).normalized));
            meteor.AddComponent<BossFallingVisual>().Initialize(start, target, delay);
        }

        private IEnumerator ExplodeRoutine(BossPatternContext context, Vector3 center, BossTelegraphView telegraph)
        {
            yield return new WaitForSeconds(delay);

            // 터지는 순간 예고는 바로 치운다
            if (telegraph != null)
            {
                telegraph.Dismiss();
            }


            SpawnEffect(effectPrefab, center, Quaternion.identity);
            // 폭발마다 재생한다. 여섯 발이 연달아 터져 소리가 겹치므로 패턴의 soundVolume을 낮춰 두었다
            PlaySound(context, center);
            float damage = context.AttackPower * damageMultiplier;
            float burnDamage = context.AttackPower * burnDamageMultiplier;

            Collider[] colliders = Physics.OverlapSphere(center, radius, context.TargetLayer);
            foreach (Collider collider in colliders)
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }

                // 화상은 실제로 피해가 들어갔을 때만 건다. 무적 시간에 막힌 타격이나 같은 대상의 두 번째 콜라이더로는 걸리지 않는다
                if (damageable.ReceiveHit(damage, center, 0f, false))
                {
                    BurnEffect.Apply(damageable, burnDamage, burnDuration, burnTickInterval, burnEffectPrefab);
                }
            }
        }
    }
}
