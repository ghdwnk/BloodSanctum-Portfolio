using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 위치와 상관없이 크기가 일정한 효과음(스킬 시전음, 대쉬음 등)을 재생하는 정적 도우미.
    ///
    /// 처음 쓸 때 씬이 바뀌어도 남는 AudioSource 하나를 만들고, PlayOneShot으로 겹쳐 재생한다.
    /// 그래서 연달아 써도 앞 소리가 끊기지 않는다.
    /// 효과음 음량·음소거는 GameSettings가 AudioListener.volume으로 처리하므로 여기서는 다루지 않는다.
    /// </summary>
    public static class SfxPlayer
    {
        private static AudioSource source;

        /// <param name="clip">재생할 소리. null이면 아무것도 하지 않는다</param>
        /// <param name="volume">0~1 음량 배율</param>
        public static void Play(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            if (source == null)
            {
                GameObject host = new GameObject("[SfxPlayer]");
                Object.DontDestroyOnLoad(host);
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                // 2D 소리: 카메라와의 거리에 따라 작아지지 않는다
                source.spatialBlend = 0f;
            }

            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
