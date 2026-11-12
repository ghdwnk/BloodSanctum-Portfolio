using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 배경음악을 상황(지역·전투·보스 처치·엔딩·로딩)에 맞춰 바꿔 트는 컴포넌트. 게임 씬의 플레이어에 붙어 있다.
    ///
    /// 곡 선택 규칙 (PlayForCurrentZone)
    ///  - 안전지대(마을)가 아닌 지역(사냥터·던전·보스 던전/아레나)이면 전투 곡
    ///  - 안전지대(마을)면 마을 곡 (musics[2]가 비어 있으면 필드 곡)
    ///  - 어느 지역에도 속하지 않은 곳은 필드 곡
    /// 다음 상황에서는 곡을 바꾸지 않는다
    ///  - 보스전 중 (PlayBattleMusic ~ PlayNormalMusic 사이)
    ///  - 로딩 화면이 떠 있는 동안 (끝나면 현재 지역 곡을 이어서 튼다)
    ///  - 엔딩 음악이 흐르는 동안
    ///
    /// 실제 음량 = 곡별 기본 음량(페이드 포함) × 환경설정의 배경음 음량(GameSettings.BgmGain) × 전체 배율(overallVolumeScale).
    /// 효과음 음량은 AudioListener.volume이 맡으므로, 배경음 소스는 그것을 무시하도록 설정한다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BackgroundMusicPlayer : MonoBehaviour
    {
        /// <summary>곡 하나와 그 곡의 기본 음량. 파일마다 원래 크기가 달라 곡별로 맞춘다.</summary>
        [System.Serializable]
        public class AudioInfo
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float defaultVolume = 1f;
        }

        private const int FieldTrack = 0;
        private const int BattleTrack = 1;
        private const int VillageTrack = 2;

        // 로딩 화면이 뜰 때 음악을 줄이는 시간(초). 화면이 덮이는 순간에 맞춰 짧게 끈다
        private const float LoadingFadeTime = 0.3f;

        [Tooltip("배경음을 재생할 소스. 비워 두면 같은 오브젝트의 AudioSource를 쓴다")]
        [SerializeField] private AudioSource audioPlayer;

        [Tooltip("0 = 필드, 1 = 전투, 2 = 마을(선택. 비워 두면 마을에서도 필드 곡을 쓴다)")]
        [SerializeField] private AudioInfo[] musics;

        [Tooltip("모든 곡에 공통으로 곱하는 배율. 곡별 기본 음량과 환경설정의 배경음 값은 그대로 두고 전체 배경음만 한 번에 낮추거나 높일 때 쓴다")]
        [SerializeField, Range(0f, 1f)] private float overallVolumeScale = 0.5f;

        [Tooltip("곡을 바꿀 때 이전 곡이 줄어드는 시간과 새 곡이 커지는 시간(초)")]
        [SerializeField] private float transitionTime = 1f;

        // 로딩 화면이 떠 있는지. 로딩 화면 쪽 코드가 플레이어를 찾지 않고 알릴 수 있게 정적으로 둔다
        private static bool loadingScreenActive;
        private static BackgroundMusicPlayer current;

        private bool endingMusicActive;
        private bool isInBattle;

        // 지금 곡의 음량(0 ~ 곡별 기본값). 페이드는 이 값을 움직이고, 실제 소스 음량은 ApplyVolume이 만든다
        private float trackVolume;
        // 전환이 겹치면 음량이 엇갈리므로, 새 전환을 시작할 때 이전 코루틴을 끊는다
        private Coroutine transitionRoutine;

        private void OnEnable()
        {
            if (audioPlayer == null)
            {
                audioPlayer = GetComponent<AudioSource>();
            }

            // 배경음 음량은 환경설정의 '배경음' 값으로만 정한다. AudioListener.volume(효과음 음량)은 무시한다
            audioPlayer.ignoreListenerVolume = true;
            // 배경음은 거리와 무관해야 한다. 3D로 두면 멀리 떨어진 지역에서 카메라와의 거리가 커져 무음이 된다
            audioPlayer.spatialBlend = 0f;

            current = this;
            // 새 씬이 시작될 때 이전 씬의 로딩 상태가 남아 있지 않게 한다
            loadingScreenActive = false;
            GameSettings.AudioChanged += ApplyVolume;

            PlayNormalMusic();
        }

        private void Start()
        {
            // 마을/필드 구분은 ZoneTracker의 지역 정보를 그대로 쓴다. ZoneTracker가 없는 씬이면 구독하지 않는다
            ZoneTracker zoneTracker = FindAnyObjectByType<ZoneTracker>();
            if (zoneTracker != null)
            {
                zoneTracker.SubscribeOnZoneChanged(OnZoneChanged);
            }
        }

        private void OnDisable()
        {
            GameSettings.AudioChanged -= ApplyVolume;

            if (current == this)
            {
                current = null;
            }
        }

        // ───────────────────────── 외부 호출 ─────────────────────────

        /// <summary>
        /// 로딩 화면이 켜지고 꺼질 때 호출한다. 켜지면 음악을 빠르게 줄여 일시정지하고,
        /// 꺼지면 현재 지역의 곡을 다시 튼다 (같은 곡이면 멈춘 자리부터 이어진다).
        /// </summary>
        public static void SetLoadingScreen(bool isOn)
        {
            loadingScreenActive = isOn;

            if (current == null)
            {
                return;
            }

            if (isOn)
            {
                current.StopMusic(LoadingFadeTime, true);
            }
            else
            {
                current.PlayForCurrentZone();
            }
        }

        /// <summary>
        /// 엔딩 연출용 곡을 튼다. StopEndingMusic을 부르기 전까지는 지역 이동·전투 시작이 와도 이 곡을 유지한다.
        /// </summary>
        public static void PlayEndingMusic(AudioClip clip, float volume)
        {
            if (current == null || clip == null)
            {
                return;
            }

            current.endingMusicActive = true;
            current.isInBattle = false;
            current.StartTransition(new AudioInfo { clip = clip, defaultVolume = volume });
        }

        /// <summary>
        /// 엔딩 음악을 끝내고 평시 규칙으로 되돌린다. 곡을 바로 바꾸지는 않는다.
        /// 결과 화면에서 마을로 돌아갈 때는 이동 로딩이 끝난 뒤 현재 지역의 곡이 나온다.
        /// </summary>
        public static void StopEndingMusic()
        {
            if (current == null)
            {
                return;
            }

            current.endingMusicActive = false;
        }

        /// <summary>평시 음악(현재 지역의 곡)으로 돌아간다. 보스전이 끝나거나 리셋될 때 부른다.</summary>
        public void PlayNormalMusic()
        {
            isInBattle = false;
            PlayForCurrentZone();
        }

        /// <summary>보스전 음악을 튼다. PlayNormalMusic을 부르기 전까지 지역이 바뀌어도 유지된다.</summary>
        public void PlayBattleMusic()
        {
            if (endingMusicActive)
            {
                return;
            }

            isInBattle = true;

            if (loadingScreenActive || !HasTrack(BattleTrack))
            {
                return;
            }

            StartTransition(musics[BattleTrack]);
        }

        // ───────────────────────── 곡 선택 ─────────────────────────

        private void OnZoneChanged(ZoneArea zone)
        {
            // 보스전 중에는 지역이 바뀌어도(아레나 진입 등) 전투 곡을 유지한다
            if (!isInBattle)
            {
                PlayForCurrentZone();
            }
        }

        private void PlayForCurrentZone()
        {
            if (loadingScreenActive || endingMusicActive)
            {
                return;
            }

            ZoneArea zone = ZoneTracker.CurrentZone;

            // 마을(안전지대)을 뺀 모든 지역(사냥터·던전·보스 던전/아레나)은 전투 곡을 쓴다. 보스가 죽은 뒤에도 그 지역에서는 유지된다.
            // zone.UseBattleMusic은 안전지대인데도 전투 곡을 쓰고 싶은 지역을 위해 남겨 둔다
            if (zone != null && (zone.UseBattleMusic || !zone.IsSafeZone) && HasTrack(BattleTrack))
            {
                StartTransition(musics[BattleTrack]);
                return;
            }

            bool useVillageTrack = zone != null && zone.IsSafeZone && HasTrack(VillageTrack);
            int track = useVillageTrack ? VillageTrack : FieldTrack;

            if (HasTrack(track))
            {
                StartTransition(musics[track]);
            }
        }

        private bool HasTrack(int index)
        {
            return musics != null && index < musics.Length && musics[index] != null && musics[index].clip != null;
        }

        // ───────────────────────── 재생·페이드 ─────────────────────────

        private void StartTransition(AudioInfo target)
        {
            // 같은 곡이 이미 재생 중이면 다시 시작하지 않는다 (같은 곡을 쓰는 지역끼리 오갈 때 끊기지 않게)
            if (audioPlayer.clip == target.clip && audioPlayer.isPlaying)
            {
                trackVolume = target.defaultVolume;
                ApplyVolume();
                return;
            }

            RestartRoutine(TransitionMusic(target));
        }

        // 재생 중인 곡을 fadeTime 동안 줄여서 멈춘다. keepPosition이 true면 일시정지해서 재생 위치를 남겨 둔다
        private void StopMusic(float fadeTime, bool keepPosition = false)
        {
            RestartRoutine(FadeOutAndStop(fadeTime, keepPosition));
        }

        private void RestartRoutine(IEnumerator routine)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(routine);
        }

        // 이전 곡을 줄이고 → 새 곡으로 바꿔 → 키운다.
        // 페이드는 실제 시간(unscaled)으로 센다. 환경설정·결과 화면이 게임 시간을 멈춰도(timeScale 0)
        // 음량이 중간에 멈춰 버리지 않게 하기 위해서다
        private IEnumerator TransitionMusic(AudioInfo target)
        {
            yield return FadeTrackVolume(trackVolume, 0f, transitionTime);

            // 로딩 화면 때문에 일시정지된 같은 곡이면 처음부터가 아니라 멈춘 자리부터 이어서 튼다
            bool resume = audioPlayer.clip == target.clip && !audioPlayer.isPlaying && audioPlayer.time > 0f;

            if (resume)
            {
                audioPlayer.UnPause();
            }
            else
            {
                audioPlayer.clip = target.clip;
                // 같은 곡이 반복돼도 매번 조금씩 다르게 들리도록 음높이를 아주 살짝 흔든다
                audioPlayer.pitch = Random.Range(0.98f, 1.02f);
                audioPlayer.Play();
            }

            yield return FadeTrackVolume(0f, target.defaultVolume, transitionTime);
            transitionRoutine = null;
        }

        private IEnumerator FadeOutAndStop(float fadeTime, bool keepPosition)
        {
            yield return FadeTrackVolume(trackVolume, 0f, fadeTime);

            if (keepPosition)
            {
                audioPlayer.Pause();
            }
            else
            {
                audioPlayer.Stop();
            }

            transitionRoutine = null;
        }

        // 곡 음량을 from에서 to로 duration 동안 바꾼다 (실제 시간 기준)
        private IEnumerator FadeTrackVolume(float from, float to, float duration)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                trackVolume = Mathf.Lerp(from, to, time / duration);
                ApplyVolume();
                yield return null;
            }

            trackVolume = to;
            ApplyVolume();
        }

        // 곡 음량에 환경설정의 배경음 음량을 곱해 실제 소스 음량으로 넣는다 (설정이 바뀔 때도 불린다)
        private void ApplyVolume()
        {
            audioPlayer.volume = trackVolume * GameSettings.BgmGain * overallVolumeScale;
        }
    }
}
