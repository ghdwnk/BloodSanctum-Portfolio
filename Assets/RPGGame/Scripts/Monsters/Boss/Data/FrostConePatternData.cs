using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 패턴: 얼음 파편 (자라톤)
    /// 보스 앞쪽 원뿔 범위를 바닥에 예고한 뒤, 얼음 파편을 부채꼴로 쏘아 보낸다.
    /// 맞은 플레이어는 피해를 입고 일정 시간 이동속도가 느려지며 몸이 푸르게 물든다 (PlayerSlowEffect).
    /// 방향은 예고를 띄우는 순간 정해지므로, 플레이어가 옆으로 빠지면 피할 수 있다
    /// </summary>
    [CreateAssetMenu(fileName = "New Frost Cone Pattern", menuName = "Scriptable Objects/Boss Pattern/Frost Cone")]
    public class FrostConePatternData : BossPatternData
    {
        [Header("원뿔")]
        public float coneLength = 9f;
        [Tooltip("좌우를 합친 각도 (45면 정면에서 좌우 22.5도씩)")]
        public float coneAngle = 45f;
        [Tooltip("예고가 차오르는 시간. 다 차면 얼음 파편이 발사되고 피해가 들어간다")]
        public float windUpTime = 1.2f;

        [Header("이펙트")]
        [Tooltip("이펙트 프리팹이 발사되는 방향. Hovl의 Crystals front attack은 프리팹 로컬 +X 쪽으로 뻗도록 만들어져 있어, 보스 정면(+Z)에 맞추려면 -90도 돌려야 한다. 다른 프리팹으로 바꾸면 이 값만 고친다")]
        public float effectYawOffset = -90f;
        public float effectScale = 1f;
        public float effectLifeTime = 3f;

        [Header("둔화")]
        // 이동속도 배율 (0.5면 절반으로 느려진다)
        [Range(0.1f, 1f)] public float slowMultiplier = 0.5f;
        public float slowDuration = 5f;
        // 둔화 중 몸에 곱해 주는 색. 1보다 작은 채널은 그만큼 어두워져 푸르게 보인다
        public Color slowTint = new Color(0.35f, 0.65f, 1f, 1f);

        public override void Execute(BossPatternContext context)
        {
            context.Caster.StartCoroutine(FrostRoutine(context));
        }

        private IEnumerator FrostRoutine(BossPatternContext context)
        {
            // 방향과 기준점은 시전 시작 시점에 고정한다 (계속 유도되면 피할 수 없다)
            Vector3 origin = context.BossTransform.position;
            Vector3 direction = context.PlayerTransform.position - origin;
            direction.y = 0f;
            direction = direction.sqrMagnitude < 0.0001f ? context.BossTransform.forward : direction.normalized;

            BossTelegraphView telegraph = BossTelegraph.Cone(origin, direction, coneLength, coneAngle, windUpTime);

            yield return new WaitForSeconds(windUpTime);

            // 발사하는 순간 예고는 바로 치운다
            if (telegraph != null)
            {
                telegraph.Dismiss();
            }

            if (effectPrefab != null)
            {
                Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, effectYawOffset, 0f);
                GameObject effect = Instantiate(effectPrefab, origin + Vector3.up * 0.1f, rotation);
                effect.transform.localScale *= Mathf.Max(0.1f, effectScale);
                PlayOnce(effect);
                Destroy(effect, effectLifeTime);
            }

            PlaySound(context, origin);
            Hit(context, origin, direction);
        }

        // Hovl 이펙트의 파티클은 loop가 켜져 있고 길이가 1~2초라, 수명(effectLifeTime)이 3초면 한 번 더 반복 재생된다.
        // 반복을 끄고 처음부터 다시 재생해 한 번만 나오게 한다
        private static void PlayOnce(GameObject effect)
        {
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = particles.main;
                main.loop = false;

                particles.Play(false);
            }
        }

        // 원뿔 안에 있는 대상에게 피해와 둔화를 준다. 한 대상의 콜라이더가 여러 개여도 한 번만 맞는다
        private void Hit(BossPatternContext context, Vector3 origin, Vector3 direction)
        {
            float damage = context.AttackPower * damageMultiplier;
            float halfAngle = coneAngle * 0.5f;
            HashSet<Damageable> hitTargets = new HashSet<Damageable>();

            Collider[] colliders = Physics.OverlapSphere(origin, coneLength, context.TargetLayer);
            foreach (Collider collider in colliders)
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable == null || hitTargets.Contains(damageable))
                {
                    continue;
                }

                Vector3 toTarget = collider.bounds.center - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;

                // 대상의 몸 두께만큼 각도에 여유를 준다. 가까울수록 몸이 차지하는 각도가 커진다
                float margin = distance > 0.01f
                    ? Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp01(0.5f / distance))
                    : 90f;

                if (distance > 0.01f && Vector3.Angle(direction, toTarget) > halfAngle + margin)
                {
                    continue;
                }

                hitTargets.Add(damageable);

                // 피해가 실제로 들어갔을 때만 둔화를 건다. 무적 시간에 막힌 타격으로는 걸리지 않는다
                if (damageable.ReceiveHit(damage, origin, 0f, false))
                {
                    PlayerSlowEffect.Apply(damageable, slowMultiplier, slowDuration, slowTint);
                }
            }
        }
    }
}
