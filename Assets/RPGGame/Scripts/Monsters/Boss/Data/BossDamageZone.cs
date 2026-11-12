using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 일정 시간 바닥에 남아, 범위 안에 있는 대상에게 일정 간격으로 피해를 주는 장판.
    /// 보스의 코루틴이 아니라 자기 Update로 동작한다. 보스가 페이즈 전환으로 코루틴을 끊어도
    /// 장판이 중간에 사라지거나 영영 남지 않게 하기 위해서다 (BossFallingVisual과 같은 이유)
    /// </summary>
    public class BossDamageZone : MonoBehaviour
    {
        private static readonly List<BossDamageZone> active = new List<BossDamageZone>();

        private Vector3 center;
        private float radius;
        private float duration;
        private float tickInterval;
        private float tickDamage;
        private LayerMask targetLayer;

        private float elapsed;
        private float nextTickTime;

        /// <summary>
        /// 장판을 만든다. activateHpRatio가 0보다 크면 펴지는 순간 범위 안의 대상에게 현재 체력의 그 비율만큼 한 번 피해를 준다.
        /// 지속 피해의 첫 틱은 tickInterval 뒤에 들어가고 duration 동안 반복된다.
        /// </summary>
        public static void Spawn(Vector3 center, float radius, float duration, float tickInterval, float tickDamage, LayerMask targetLayer,
            float activateHpRatio = 0f)
        {
            GameObject root = new GameObject("Boss Damage Zone");
            root.transform.position = center;

            BossDamageZone zone = root.AddComponent<BossDamageZone>();
            zone.center = center;
            zone.radius = radius;
            zone.duration = duration;
            zone.tickInterval = Mathf.Max(0.05f, tickInterval);
            zone.tickDamage = tickDamage;
            zone.targetLayer = targetLayer;
            zone.nextTickTime = zone.tickInterval;

            active.Add(zone);

            if (activateHpRatio > 0f)
            {
                zone.ActivateBurst(activateHpRatio);
            }
        }

        /// <summary>남아 있는 장판을 모두 지운다 (보스 사망·전투 리셋 때).</summary>
        public static void ClearAll()
        {
            for (int ix = active.Count - 1; ix >= 0; --ix)
            {
                if (active[ix] != null)
                {
                    Destroy(active[ix].gameObject);
                }
            }

            active.Clear();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // 프레임이 길게 튀어도 놓친 틱을 모두 처리한다. 마지막 틱이 duration과 같은 시각이어도 포함한다
            while (nextTickTime <= duration + 0.0001f && elapsed >= nextTickTime)
            {
                Tick();
                nextTickTime += tickInterval;
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            active.Remove(this);
        }

        private void Tick()
        {
            ForEachLivingTarget((damageable, hp) => damageable.ReceiveDamageOverTime(tickDamage));
        }

        // 장판이 펴지는 순간 한 번 들어가는 비율 피해. 현재 체력 기준이라 체력이 많을수록 크게 들어가고,
        // 절반이므로 이것만으로는 죽지 않는다. 방어력은 적용하지 않는다 (정확히 그 비율만큼 깎이게)
        private void ActivateBurst(float hpRatio)
        {
            ForEachLivingTarget((damageable, hp) =>
            {
                if (hp != null)
                {
                    damageable.ReceiveFixedDamage(Mathf.Floor(hp.CurrentHP * hpRatio));
                }
            });
        }

        // 대상의 체력 컴포넌트를 찾는다. 플레이어는 Damageable(Damage Controller)과 HPController(HP Controller)가
        // 같은 오브젝트가 아니라 부모(Player) 아래 서로 다른 자식에 있다. 같은 오브젝트만 보면 체력을 못 찾아
        // 현재 체력 비율 피해가 조용히 건너뛰어졌다. 그래서 같은 오브젝트에 없으면 부모 아래 자식까지 찾는다
        private static HPController FindHP(Damageable damageable)
        {
            HPController hp = damageable.GetComponent<HPController>();
            if (hp == null && damageable.transform.parent != null)
            {
                hp = damageable.transform.parent.GetComponentInChildren<HPController>();
            }

            return hp;
        }

        // 범위 안의 살아 있는 대상마다 한 번씩 apply를 부른다
        private void ForEachLivingTarget(System.Action<Damageable, HPController> apply)
        {
            Collider[] colliders = Physics.OverlapSphere(center, radius, targetLayer);

            // 플레이어 콜라이더가 여러 개여도 한 번만 맞도록 Damageable 단위로 걸러 낸다
            HashSet<Damageable> hit = new HashSet<Damageable>();

            foreach (Collider collider in colliders)
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable == null || !hit.Add(damageable))
                {
                    continue;
                }

                // 이미 죽은 대상에게 피해를 주면 사망 이벤트가 또 발행된다
                HPController hp = FindHP(damageable);
                if (hp != null && hp.CurrentHP <= 0f)
                {
                    continue;
                }

                apply(damageable, hp);
            }
        }
    }
}
