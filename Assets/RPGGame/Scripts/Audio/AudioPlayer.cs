using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 같은 오브젝트의 AudioSource로 등록된 소리 중 하나를 무작위로 골라 트는 컴포넌트.
    ///
    /// 몬스터·플레이어의 피격음/사망음/발소리/휘두르는 소리에 쓰인다.
    /// 대부분 Damageable.OnDamageReceived, HPController.OnDead 같은 이벤트에 인스펙터로 Play()가 연결돼 있고,
    /// 발소리처럼 코드(PlayerMoveState)에서 직접 부르기도 한다.
    /// 위치가 있는 3D 소리인지(spatialBlend), 거리 감쇠 등은 AudioSource 설정을 그대로 따른다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        [Tooltip("재생할 소리 목록. 재생할 때마다 하나를 무작위로 고른다")]
        [SerializeField] private AudioClip[] audioClips;

        [Tooltip("켜면 재생할 때마다 음높이를 조금씩 바꿔, 같은 소리가 반복돼도 덜 단조롭게 들린다")]
        [SerializeField] private bool isRandomizePitch;

        [Tooltip("음높이를 흔드는 폭. 0.2면 0.8 ~ 1.2배 사이")]
        [SerializeField] private float pitchRandomRange = 0.2f;

        [Tooltip("Play를 부른 뒤 실제로 소리가 나기까지 기다리는 시간(초)")]
        [SerializeField] private float playDelay;

        private AudioSource audioPlayer;

        private void Awake()
        {
            audioPlayer = GetComponent<AudioSource>();
        }

        /// <summary>
        /// 등록된 소리 중 하나를 튼다. UnityEvent에서 호출되므로 이름과 매개변수를 바꾸지 않는다.
        /// 같은 소스를 쓰므로 이전 소리가 아직 나고 있으면 끊기고 새 소리로 바뀐다.
        /// </summary>
        public void Play()
        {
            Play(0f);
        }

        /// <summary>Play와 같지만 기본 지연(playDelay)에 추가 지연을 더한다. 애니메이션 동작 시점에 소리를 맞출 때 쓴다.</summary>
        public void Play(float extraDelay)
        {
            if (audioClips == null || audioClips.Length == 0)
            {
                return;
            }

            // 오브젝트가 한 번도 켜지지 않은 상태에서 불리면 Awake가 아직 안 돌았을 수 있다
            if (audioPlayer == null)
            {
                audioPlayer = GetComponent<AudioSource>();
            }

            audioPlayer.clip = audioClips[Random.Range(0, audioClips.Length)];
            audioPlayer.pitch = isRandomizePitch ? Random.Range(1f - pitchRandomRange, 1f + pitchRandomRange) : 1f;
            audioPlayer.PlayDelayed(playDelay + Mathf.Max(0f, extraDelay));
        }
    }
}
