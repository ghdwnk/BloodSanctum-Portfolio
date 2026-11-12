using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 해상도·전체화면·음량 설정을 저장하고 적용하는 정적 클래스. 로비와 게임 씬의 환경설정 창이 함께 쓴다.
    ///
    /// 값은 PlayerPrefs에 저장되고, 앱이 시작될 때(첫 씬이 로드되기 전) 자동으로 적용된다.
    /// 음량은 두 갈래로 나뉜다.
    ///  - 효과음: AudioListener.volume에 넣는다. 효과음 소스를 하나하나 믹서에 연결하지 않아도 한 번에 조절된다
    ///  - 배경음: BackgroundMusicPlayer가 AudioListener.volume을 무시하고(ignoreListenerVolume) BgmGain을 직접 곱한다
    ///
    /// 음량 값은 메모리에 캐시해 둔다. 배경음 페이드처럼 매 프레임 BgmGain을 읽는 곳이 있어
    /// 그때마다 PlayerPrefs를 읽지 않기 위해서다.
    /// </summary>
    public static class GameSettings
    {
        private const string KeyResolutionIndex = "Settings_ResolutionIndex";
        private const string KeyFullscreen = "Settings_Fullscreen";
        private const string KeyBgmVolume = "Settings_BgmVolume";
        private const string KeySfxVolume = "Settings_SfxVolume";
        private const string KeyBgmMuted = "Settings_BgmMuted";
        private const string KeySfxMuted = "Settings_SfxMuted";
        // 예전에는 음량·음소거가 하나뿐이었다. 배경음/효과음으로 나눈 뒤에도 예전 값을 두 곳의 초기값으로 이어받는다
        private const string KeyLegacyVolume = "Settings_MasterVolume";
        private const string KeyLegacyMuted = "Settings_Muted";

        /// <summary>음량·음소거가 바뀔 때마다 알린다. 배경음 플레이어가 구독해 재생 중인 곡의 음량을 바로 바꾼다.</summary>
        public static event Action AudioChanged;

        private static Resolution[] cachedResolutions;

        // 음량 캐시. 처음 읽을 때 PlayerPrefs에서 불러오고, 바꿀 때 캐시와 PlayerPrefs를 함께 갱신한다
        private static bool audioLoaded;
        private static float bgmVolume;
        private static float sfxVolume;
        private static bool bgmMuted;
        private static bool sfxMuted;

        // ───────────────────────── 음량 ─────────────────────────

        public static float GetSavedBgmVolume() { EnsureAudioLoaded(); return bgmVolume; }
        public static float GetSavedSfxVolume() { EnsureAudioLoaded(); return sfxVolume; }
        public static bool GetSavedBgmMuted() { EnsureAudioLoaded(); return bgmMuted; }
        public static bool GetSavedSfxMuted() { EnsureAudioLoaded(); return sfxMuted; }

        /// <summary>배경음에 실제로 곱할 값 (음소거면 0). 슬라이더 값은 음소거와 상관없이 그대로 저장돼 있다.</summary>
        public static float BgmGain => GetSavedBgmMuted() ? 0f : GetSavedBgmVolume();

        /// <summary>효과음에 실제로 곱할 값 (음소거면 0).</summary>
        public static float SfxGain => GetSavedSfxMuted() ? 0f : GetSavedSfxVolume();

        public static void SetBgmVolume(float volume01)
        {
            EnsureAudioLoaded();
            bgmVolume = Mathf.Clamp01(volume01);
            PlayerPrefs.SetFloat(KeyBgmVolume, bgmVolume);
            SaveAndApplyAudio();
        }

        public static void SetSfxVolume(float volume01)
        {
            EnsureAudioLoaded();
            sfxVolume = Mathf.Clamp01(volume01);
            PlayerPrefs.SetFloat(KeySfxVolume, sfxVolume);
            SaveAndApplyAudio();
        }

        /// <summary>음소거는 저장된 음량 값을 그대로 두고 실제 출력만 껐다 켠다.</summary>
        public static void SetBgmMuted(bool muted)
        {
            EnsureAudioLoaded();
            bgmMuted = muted;
            PlayerPrefs.SetInt(KeyBgmMuted, muted ? 1 : 0);
            SaveAndApplyAudio();
        }

        public static void SetSfxMuted(bool muted)
        {
            EnsureAudioLoaded();
            sfxMuted = muted;
            PlayerPrefs.SetInt(KeySfxMuted, muted ? 1 : 0);
            SaveAndApplyAudio();
        }

        private static void EnsureAudioLoaded()
        {
            if (audioLoaded)
            {
                return;
            }

            float legacyVolume = PlayerPrefs.GetFloat(KeyLegacyVolume, 1f);
            int legacyMuted = PlayerPrefs.GetInt(KeyLegacyMuted, 0);

            bgmVolume = PlayerPrefs.GetFloat(KeyBgmVolume, legacyVolume);
            sfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, legacyVolume);
            bgmMuted = PlayerPrefs.GetInt(KeyBgmMuted, legacyMuted) == 1;
            sfxMuted = PlayerPrefs.GetInt(KeySfxMuted, legacyMuted) == 1;
            audioLoaded = true;
        }

        private static void SaveAndApplyAudio()
        {
            PlayerPrefs.Save();
            AudioListener.volume = SfxGain;
            AudioChanged?.Invoke();
        }

        // ───────────────────────── 화면 ─────────────────────────

        /// <summary>같은 가로·세로에 주사율만 다른 항목을 빼고 남긴 해상도 목록.</summary>
        public static Resolution[] GetAvailableResolutions()
        {
            if (cachedResolutions != null)
            {
                return cachedResolutions;
            }

            List<Resolution> distinct = new List<Resolution>();

            foreach (Resolution resolution in Screen.resolutions)
            {
                if (!distinct.Exists(r => r.width == resolution.width && r.height == resolution.height))
                {
                    distinct.Add(resolution);
                }
            }

            // 플랫폼에 따라 Screen.resolutions가 비어 있을 수 있으므로(일부 에디터 환경 등) 현재 해상도라도 넣어 둔다
            if (distinct.Count == 0)
            {
                distinct.Add(Screen.currentResolution);
            }

            cachedResolutions = distinct.ToArray();
            return cachedResolutions;
        }

        /// <summary>저장된 해상도 번호. 저장된 값이 없으면 현재 해상도의 번호(없으면 0).</summary>
        public static int GetSavedResolutionIndex()
        {
            Resolution[] resolutions = GetAvailableResolutions();
            int currentIndex = Array.FindIndex(resolutions, r =>
                r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height);

            return PlayerPrefs.GetInt(KeyResolutionIndex, currentIndex >= 0 ? currentIndex : 0);
        }

        public static bool GetSavedFullscreen()
        {
            // PlayerPrefs에는 bool이 없어 0/1 정수로 저장한다
            return PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        }

        public static void SetResolution(int index, bool fullscreen)
        {
            Resolution[] resolutions = GetAvailableResolutions();
            if (index < 0 || index >= resolutions.Length)
            {
                Debug.LogWarning($"[GameSettings] 잘못된 해상도 번호: {index}");
                return;
            }

            Resolution target = resolutions[index];
            Screen.SetResolution(target.width, target.height, fullscreen);

            PlayerPrefs.SetInt(KeyResolutionIndex, index);
            PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ───────────────────────── 시작 시 적용 ─────────────────────────

        /// <summary>
        /// 앱이 시작될 때 첫 씬이 로드되기 전에 저장된 설정을 적용한다.
        /// 씬에 따로 오브젝트를 둘 필요가 없다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedSettingsOnLoad()
        {
            // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 최신 값을 읽도록 캐시를 비운다
            audioLoaded = false;

            // 효과음 음량을 적용한다. 배경음은 BackgroundMusicPlayer가 재생할 때 적용한다
            AudioListener.volume = SfxGain;

            // 해상도는 저장된 값이 있을 때만 강제로 바꾼다 (처음 실행할 때는 OS·플레이어 기본 해상도를 그대로 둔다)
            if (!PlayerPrefs.HasKey(KeyResolutionIndex))
            {
                return;
            }

            Resolution[] resolutions = GetAvailableResolutions();
            int index = PlayerPrefs.GetInt(KeyResolutionIndex);

            if (index >= 0 && index < resolutions.Length)
            {
                Resolution target = resolutions[index];
                Screen.SetResolution(target.width, target.height, GetSavedFullscreen());
            }
        }
    }
}
