using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 비전 폭풍: 전방 지면에 레이저가 내리꽂히는 장판을 만들고, 그 안의 적에게 일정 간격으로 피해를 준다.
    /// 스킬 레벨이 오를수록 지속시간이 늘어난다. (에셋 이름은 예전 스킬 이름인 Earthquake Slam Skill이다 —
    /// 세이브가 스킬을 에셋 이름으로 기억하므로 파일 이름은 바꾸지 않는다)
    ///
    /// 이펙트 수명이 레벨마다 달라서 vfx 배열(고정 수명)을 쓰지 않고 직접 만들어, 판정이 끝나는 순간 함께 사라지게 한다.
    /// 피해 판정은 SkillDamageZone이 따로 맡는다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Laser Rain Skill", menuName = "Skill/Laser Rain")]
    public class LaserRainSkillData : SkillData
    {
        [Header("비전 폭풍")]
        [Tooltip("시전자 앞 몇 m에 장판을 만들지")]
        public float castDistance = 5.5f;
        [Tooltip("장판 반경(m)")]
        public float radius = 4.5f;
        [Tooltip("1레벨 지속시간(초)")]
        public float duration = 5f;
        [Tooltip("레벨당 늘어나는 지속시간(초)")]
        public float durationPerLevel = 1.5f;
        [Tooltip("피해를 주는 간격(초)")]
        public float tickInterval = 0.5f;
        [Tooltip("틱 한 번의 1레벨 고정 피해. 틱 피해 = (이 값 + 공격력 x attackRatio) x 레벨 배율")]
        public float tickDamage = 10f;
        [Tooltip("틱마다 더할 플레이어 공격력 비율 (0.35 = 35%). 틱 수가 많아 한 번 치는 스킬보다 낮게 둔다")]
        public float attackRatio = 0.35f;
        [Tooltip("내려찍는 모션이 땅에 닿기까지의 대기 시간(초)")]
        public float windupDelay = 0.67f;
        [Tooltip("장판 위에 재생할 이펙트. 판정 지속시간 동안만 유지된다")]
        public GameObject zoneVfxPrefab;

        // 장판 이펙트가 끝날 때 줄어들며 사라지는 시간(초)
        private const float ZoneFadeTime = 0.3f;

        /// <summary>스킬 레벨에 따른 지속시간 (툴팁과 실제 판정이 같은 값을 쓴다).</summary>
        public float GetDuration(int skillLevel)
        {
            return duration + durationPerLevel * (ClampLevel(skillLevel) - 1);
        }

        public override string GetEffectText(int skillLevel)
        {
            float damagePerTick = CalculateDamage(tickDamage, attackRatio, GetCurrentPlayerAttack(), skillLevel);
            return $"데미지 {damagePerTick:0} ({tickInterval:0.#}초마다, {FormatRatio(tickDamage, attackRatio)}) · 지속 {GetDuration(skillLevel):0.#}초";
        }

        public override void ApplyEffect(SkillCastContext context)
        {
            context.Caster.StartCoroutine(RainRoutine(context));
        }

        private IEnumerator RainRoutine(SkillCastContext context)
        {
            yield return new WaitForSeconds(windupDelay);

            Transform player = context.PlayerTransform;
            if (player == null)
            {
                yield break;
            }

            // windupDelay가 지난 시점의 플레이어 앞쪽. 이펙트와 판정이 같은 위치를 쓴다
            Vector3 center = player.position + player.forward * castDistance;
            float damagePerTick = CalculateDamage(tickDamage, attackRatio, GetCasterAttack(context), context.SkillLevel);
            float leveledDuration = GetDuration(context.SkillLevel);

            SkillDamageZone.Spawn(center, radius, leveledDuration, tickInterval, damagePerTick, context.TargetLayer);

            if (zoneVfxPrefab != null)
            {
                GameObject effect = Instantiate(zoneVfxPrefab, center + Vector3.up * 0.05f, Quaternion.identity);
                FitEffectToDuration(effect, leveledDuration);

                // 줄어드는 시간까지 지속시간 안에 넣어, 이펙트가 완전히 사라지는 순간 피해도 끝나게 한다.
                // 지속시간이 끝난 뒤에 줄이기 시작하면 판정이 끝났는데도 이펙트가 남아 보인다
                TimedSkillVfx.Attach(effect, Mathf.Max(0f, leveledDuration - ZoneFadeTime), ZoneFadeTime);
            }
        }

        // 이펙트 프리팹은 5초 주기로 반복(loop)되게 만들어져 있다. 지속시간이 5초를 넘는 레벨에서는 반복될 때마다
        // 시작 연출(섬광·불꽃·연기·마법진 생성)이 다시 터져, 스킬이 끝나고 또 발동된 것처럼 보인다.
        // 그래서 반복을 끄고, 시작 연출은 한 번만 내되 레이저·마법진은 지속시간 끝까지 이어지게 맞춘다
        private static void FitEffectToDuration(GameObject effect, float effectDuration)
        {
            foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                // 재생 중에는 duration을 바꿀 수 없다. 생성 직후라 지워도 보이는 것은 없다
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = particle.main;
                main.loop = false;
                main.duration = effectDuration;

                // 마법진처럼 한 번 나와 오래 사는 파티클은 수명을 지속시간에 맞춘다
                // (3초 미만의 짧은 파티클은 레이저·불꽃 같은 순간 연출이라 그대로 둔다)
                if (main.startLifetime.mode == ParticleSystemCurveMode.Constant && main.startLifetime.constant >= 3f)
                {
                    main.startLifetime = effectDuration;
                }

                // 정해진 횟수만 반복 발사하는 버스트(레이저)는 지속시간이 끝날 때까지 계속 쏘게 한다 (0 = 무한 반복)
                ParticleSystem.EmissionModule emission = particle.emission;
                for (int ix = 0; ix < emission.burstCount; ++ix)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(ix);
                    if (burst.cycleCount > 1)
                    {
                        burst.cycleCount = 0;
                        emission.SetBurst(ix, burst);
                    }
                }

                particle.Play(false);
            }
        }
    }
}
