using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 스킬이 바닥에 남기는 지속 피해 장판(비전 폭풍, 궁극기 잔불). 일정 간격으로 범위 안의 적에게 피해를 준다.
    ///
    /// Spawn으로 빈 오브젝트를 만들어 스스로 동작하고 스스로 사라진다. 그래서 시전 코루틴이 끊겨도
    /// (플레이어 사망 등) 장판이 영영 남지 않고, 시전자가 쓰러져도 장판은 끝까지 유지된다.
    /// 보스 장판(BossDamageZone)과 같은 방식이지만, 보스 사망·리셋 때 함께 지워지는 목록을 공유하지 않도록 따로 두었다.
    /// </summary>
    public class SkillDamageZone : MonoBehaviour
    {
        // 모든 장판이 함께 쓰는 판정 버퍼. 틱마다 배열·집합을 새로 만들지 않도록 재사용한다 (메인 스레드에서만 쓰므로 안전하다)
        private static readonly Collider[] hitBuffer = new Collider[64];
        private static readonly HashSet<Damageable> hitThisTick = new HashSet<Damageable>();

        private Vector3 center;
        private float radius;
        private float duration;
        private float tickInterval;
        private float tickDamage;
        private LayerMask targetLayer;

        private float elapsed;
        private float nextTickTime;

        /// <summary>장판을 만든다. 첫 피해는 tickInterval 뒤에 들어가고, duration 동안 반복된다.</summary>
        public static void Spawn(Vector3 center, float radius, float duration, float tickInterval, float tickDamage, LayerMask targetLayer)
        {
            GameObject root = new GameObject("Skill Damage Zone");
            root.transform.position = center;

            SkillDamageZone zone = root.AddComponent<SkillDamageZone>();
            zone.center = center;
            zone.radius = radius;
            zone.duration = duration;
            zone.tickInterval = Mathf.Max(0.05f, tickInterval);
            zone.tickDamage = tickDamage;
            zone.targetLayer = targetLayer;
            zone.nextTickTime = zone.tickInterval;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // 프레임이 튀어도 놓친 틱을 모두 처리한다. 마지막 틱이 duration과 같은 시각이어도 포함한다
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

        private void Tick()
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, hitBuffer, targetLayer);

            // 한 몬스터의 콜라이더가 여러 개여도 한 틱에 한 번만 맞도록 Damageable 단위로 거른다
            hitThisTick.Clear();

            for (int ix = 0; ix < count; ++ix)
            {
                Damageable damageable = hitBuffer[ix].GetComponent<Damageable>();
                if (damageable == null || !hitThisTick.Add(damageable))
                {
                    continue;
                }

                // 이미 죽은 몬스터에게 피해를 주면 사망 이벤트가 또 발행된다
                HPController hp = damageable.GetComponent<HPController>();
                if (hp != null && hp.CurrentHP <= 0f)
                {
                    continue;
                }

                // 무적 시간을 보지도 켜지도 않는 지속 피해 경로라 다른 공격의 타격을 막지 않는다
                damageable.ReceiveDamageOverTime(tickDamage);
            }
        }
    }
}
