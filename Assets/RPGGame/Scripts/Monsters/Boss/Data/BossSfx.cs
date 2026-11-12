using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스 효과음을 재생하는 정적 도우미.
    /// AudioSource.PlayClipAtPoint의 기본 감쇠(로그)는 몇 걸음만 떨어져도 거의 안 들릴 만큼 가파르다.
    /// 덩치 큰 보스는 아레나 어디서든 소리가 들려야 하므로, 가까우면 최대 음량·멀어질수록 선형으로 줄어드는 소스를 직접 만든다
    /// </summary>
    public static class BossSfx
    {
        // 이 거리 안에서는 음량 100%, MaxDistance에서 0%
        private const float MinDistance = 14f;
        private const float MaxDistance = 70f;

        // clips 중 하나를 무작위로 골라 position에서 재생한다. 비어 있으면 아무것도 하지 않는다
        public static void Play(AudioClip[] clips, Vector3 position, float volume = 1f, float pitchJitter = 0.05f)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            GameObject host = new GameObject("[BossSfx]");
            host.transform.position = position;

            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitchJitter > 0f ? Random.Range(1f - pitchJitter, 1f + pitchJitter) : 1f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.Play();

            // 피치가 낮아지면 재생이 길어지므로 여유를 둔다
            Object.Destroy(host, clip.length / Mathf.Max(0.5f, source.pitch) + 0.2f);
        }
    }
}
