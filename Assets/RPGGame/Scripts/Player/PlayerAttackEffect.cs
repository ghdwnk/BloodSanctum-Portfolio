using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 기본 공격 콤보 한 단계의 칼 궤적 이펙트. Weapon Controller 아래 콤보마다 하나씩 있고,
    /// 한 번 재생한 뒤 스스로 꺼진다.
    ///
    /// 두 가지 방식을 모두 쓸 수 있다:
    ///  - 레거시 Animation 클립이 붙어 있으면 그 클립을 한 번 재생한다 (옛 칼 궤적 메시 방식)
    ///  - Animation이 없으면 자식 파티클 시스템을 모두 처음부터 재생한다 (파티클 베기 이펙트 방식)
    /// WeaponController.PlayAttackComboEffect가 Activate를, 공격이 끊길 때 CancelAttackEffects가 Cancel을 부른다.
    /// </summary>
    public class PlayerAttackEffect : MonoBehaviour
    {
        [Header("파티클 방식")]
        [Tooltip("콤보 시작(공격 상태 진입)부터 이펙트가 나오기까지 기다리는 시간(초). 휘두르기 시작 전의 준비 동작이 끝난 뒤에 베기 궤적이 나오게 맞춘다")]
        [SerializeField] private float playDelay;
        [Tooltip("파티클을 재생한 뒤 이 시간(초)이 지나면 이펙트를 끈다. 파티클 방식에서만 쓴다")]
        [SerializeField] private float playDuration = 0.6f;

        private Animation refAnimation;
        // 파티클 방식일 때 자식 파티클 시스템 목록. 매번 GetComponentsInChildren을 부르지 않도록 Awake에서 한 번만 모은다
        private ParticleSystem[] particles;

        private void Awake()
        {
            refAnimation = GetComponent<Animation>();
            particles = GetComponentsInChildren<ParticleSystem>(true);

            // 켜지는 순간 저절로 재생되지 않게 한다. 재생 시점은 아래 코루틴이 정한다
            foreach (ParticleSystem particle in particles)
            {
                ParticleSystem.MainModule main = particle.main;
                main.playOnAwake = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>궤적을 처음부터 재생한다.</summary>
        public void Activate(float timeScale = 1f)
        {
            gameObject.SetActive(true);

            // 아직 앞 재생이 끝나지 않았으면 그 타이머가 새 재생을 중간에 끄지 않도록 새로 시작한다
            StopAllCoroutines();

            if (refAnimation != null && refAnimation.clip != null)
            {
                refAnimation.Play();
                StartCoroutine(DisableAfter(refAnimation.clip.length));
                return;
            }

            StartCoroutine(PlayParticles(timeScale));
        }

        /// <summary>재생 중인 궤적을 바로 끈다 (공격을 중간에 끊을 때).</summary>
        public void Cancel()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // 준비 동작이 끝날 때까지 기다렸다가 파티클을 처음부터 재생하고, 일정 시간 뒤에 끈다
        private IEnumerator PlayParticles(float timeScale)
        {
            foreach (ParticleSystem particle in particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (playDelay > 0f)
            {
                yield return new WaitForSeconds(playDelay / timeScale);
            }

            foreach (ParticleSystem particle in particles)
            {
                particle.Play(false);
            }

            yield return new WaitForSeconds(playDuration);
            gameObject.SetActive(false);
        }

        private IEnumerator DisableAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            gameObject.SetActive(false);
        }
    }
}
