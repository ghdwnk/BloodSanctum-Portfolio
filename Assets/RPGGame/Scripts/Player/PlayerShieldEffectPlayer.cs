using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 맞았을 때 잠깐 보이는 방어막 이펙트. 이펙트 오브젝트 자신에 붙어 있다.
    /// 플레이어 Damageable.OnDamageReceived에 인스펙터로 Play가 연결돼 있다.
    /// </summary>
    public class PlayerShieldEffectPlayer : MonoBehaviour
    {
        [Tooltip("이펙트를 켜 두는 시간(초)")]
        [SerializeField] private float playTime = 0.5f;

        /// <summary>이펙트를 켜고 playTime 뒤에 끈다. UnityEvent에서 호출되므로 이름과 매개변수를 바꾸지 않는다.</summary>
        public void Play()
        {
            gameObject.SetActive(true);

            // 연달아 맞으면 앞의 타이머가 이펙트를 일찍 끄지 않도록 새로 시작한다
            StopAllCoroutines();
            StartCoroutine(TurnOffAfter(playTime));
        }

        private IEnumerator TurnOffAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }
    }
}
