using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 연속 균열
    /// 보스 앞쪽 일직선으로 폭발이 차례로 터져 나간다. 모든 지점을 한꺼번에 예고하고,
    /// 가까운 곳부터 간격을 두고 터지므로 옆으로 빠지거나 터지는 순서를 읽고 사이로 피해야 한다
    /// </summary>
    [CreateAssetMenu(fileName = "New Line Eruption Pattern", menuName = "Scriptable Objects/Boss Pattern/Line Eruption")]
    public class LineEruptionPatternData : BossPatternData
    {
        [Header("균열")]
        public int burstCount = 5;
        [Tooltip("보스에서 첫 폭발까지, 그리고 폭발 사이의 거리")]
        public float startOffset = 2.5f;
        public float spacing = 3.2f;
        public float burstRadius = 2.4f;
        [Tooltip("첫 폭발까지의 준비 시간과 폭발 사이의 시간 간격")]
        public float windUpTime = 0.8f;
        public float burstInterval = 0.22f;

        [Header("화상")]
        // 폭발에 맞으면 걸리는 화상. 여러 번 맞아도 쌓이지 않고 지속 시간만 갱신된다
        public BossBurnSettings burn = new BossBurnSettings();

        public override void Execute(BossPatternContext context)
        {
            // 방향은 시작 시점의 플레이어 쪽으로 고정한다
            Vector3 direction = context.PlayerTransform.position - context.BossTransform.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude < 0.0001f ? context.BossTransform.forward : direction.normalized;

            for (int ix = 0; ix < burstCount; ++ix)
            {
                Vector3 center = context.BossTransform.position + direction * (startOffset + spacing * ix);
                float delay = windUpTime + burstInterval * ix;

                BossTelegraphView telegraph = BossTelegraph.Circle(center, burstRadius, delay);
                context.Caster.StartCoroutine(BurstRoutine(context, center, delay, telegraph));
            }
        }

        private IEnumerator BurstRoutine(BossPatternContext context, Vector3 center, float delay, BossTelegraphView telegraph)
        {
            yield return new WaitForSeconds(delay);

            if (telegraph != null)
            {
                telegraph.Dismiss();
            }


            SpawnEffect(effectPrefab, center, Quaternion.identity);
            // 폭발마다 재생한다. 소리가 겹쳐 커지므로 패턴의 soundVolume을 낮춰 두었다
            PlaySound(context, center);
            float damage = context.AttackPower * damageMultiplier;

            Collider[] colliders = Physics.OverlapSphere(center, burstRadius, context.TargetLayer);
            foreach (Collider collider in colliders)
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }

                // 화상은 피해가 실제로 들어갔을 때만 건다. 같은 대상의 두 번째 콜라이더는 무적 시간에 막혀 걸리지 않는다
                if (damageable.ReceiveHit(damage, center, 0f, false))
                {
                    burn.Apply(damageable, context.AttackPower);
                }
            }
        }
    }
}
