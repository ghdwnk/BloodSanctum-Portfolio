using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 스킬 장판 이펙트를 정해진 시간 동안만 유지하고, 끝나면 방출을 멈춘 뒤 빠르게 줄여서 지운다.
    ///
    /// 지속형 장판 이펙트는 방출만 멈추면 이미 나온 큰 파티클(예: 수명 4초짜리 마법진)이 판정이 끝난 뒤에도 한참 남아
    /// "스킬은 끝났는데 장판이 남아 있는" 것처럼 보인다. 그래서 끝나는 순간 크기를 줄여 바로 사라지게 한다.
    /// 비전 폭풍·궁극기 잔불이 이펙트를 만든 직후 Attach로 붙인다.
    /// </summary>
    public class TimedSkillVfx : MonoBehaviour
    {
        private float duration;
        private float fadeTime;
        private float elapsed;
        private Vector3 startScale;
        private bool isFading;

        /// <summary>이펙트를 duration초 동안 유지한 뒤 fadeTime초에 걸쳐 줄여 지운다.</summary>
        public static void Attach(GameObject vfx, float duration, float fadeTime = 0.3f)
        {
            if (vfx == null)
            {
                return;
            }

            TimedSkillVfx timed = vfx.AddComponent<TimedSkillVfx>();
            timed.duration = Mathf.Max(0f, duration);
            timed.fadeTime = Mathf.Max(0.05f, fadeTime);
            timed.startScale = vfx.transform.localScale;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (!isFading)
            {
                if (elapsed >= duration)
                {
                    BeginFade();
                }

                return;
            }

            float t = Mathf.Clamp01(elapsed / fadeTime);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        // 새 파티클은 더 만들지 않고, 이미 나온 파티클은 이후 프레임에서 크기를 줄여 지운다
        private void BeginFade()
        {
            isFading = true;
            elapsed = 0f;

            foreach (ParticleSystem particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
