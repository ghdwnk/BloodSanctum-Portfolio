using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGGame
{
    /// <summary>
    /// 마지막 퀘스트를 완료하면 나오는 엔딩 화면.
    /// 이번 판의 기록을 요약해 보여주고, 로비로 돌아가거나 게임을 끝낼 수 있게 한다.
    /// </summary>
    public class UIEndingScreen : MonoBehaviour
    {
        private static UIEndingScreen instance;

        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("설정")]
        [Tooltip("이 퀘스트를 완료하면 엔딩이 시작된다 (마지막 퀘스트)")]
        [SerializeField] private int finalQuestID = 7;
        [Tooltip("보스 사망 연출을 볼 시간")]
        [SerializeField] private float showDelay = 4f;
        [SerializeField] private float fadeDuration = 1.5f;
        [SerializeField] private string lobbySceneName = "BloodSanctumGameStart";

        public static bool IsOn => instance != null && instance.panel != null && instance.panel.activeSelf;
        // 엔딩 화면이 완전히 떠서(페이드 완료) 뒤에 덮어둔 연출 화면을 치워도 되는지
        public static bool IsFadeComplete { get; private set; }

        private bool isSubscribed;
        private bool hasPlayed;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (isSubscribed || QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.SubscribeOnQuestCompleted(OnQuestCompleted);
            isSubscribed = true;
        }

        private void OnQuestCompleted(int questID)
        {
            // 마지막 퀘스트에서만, 그리고 한 번만 재생한다
            if (questID != finalQuestID || hasPlayed)
            {
                return;
            }

            hasPlayed = true;
            StartCoroutine(PlayEndingRoutine(showDelay));
        }

        // 봉인 왕좌 연출처럼 이미 화면이 가려진 상태에서 엔딩 화면을 바로 띄울 때 쓰는 진입점.
        // 퀘스트 완료 이벤트를 기다리지 않고, 보스 사망 연출 대기 시간도 건너뛴다
        public static void BeginEnding()
        {
            if (instance == null || instance.hasPlayed)
            {
                return;
            }

            instance.hasPlayed = true;
            instance.StartCoroutine(instance.PlayEndingRoutine(0f));
        }

        private IEnumerator PlayEndingRoutine(float delay)
        {
            IsFadeComplete = false;

            // 보스가 쓰러지는 장면을 보여준 뒤에 화면을 띄운다
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                // 호출한 쪽이 같은 프레임에 보상 지급 등을 마칠 수 있도록 한 프레임 뒤에 통계를 만든다
                yield return null;
            }

            BuildTexts();

            if (panel != null)
            {
                panel.SetActive(true);
            }

            // 화면이 뜨는 동안 전투가 계속되면 안 되므로 멈춘다.
            // 페이드는 timeScale의 영향을 받지 않는 unscaled 시간을 쓴다
            Time.timeScale = 0f;

            if (fadeGroup != null)
            {
                float elapsed = 0f;
                fadeGroup.alpha = 0f;

                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    fadeGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);

                    yield return null;
                }

                fadeGroup.alpha = 1f;
            }

            IsFadeComplete = true;
        }

        private void BuildTexts()
        {
            if (titleText != null)
            {
                titleText.text = "봉인 완료";
            }

            if (statsText == null)
            {
                return;
            }

            // 씬 로드 이후 경과 시간이 곧 이번 판의 플레이 시간이다
            System.TimeSpan playTime = System.TimeSpan.FromSeconds(Time.timeSinceLevelLoad);

            statsText.text =
                $"플레이 시간      {playTime.Hours:00}:{playTime.Minutes:00}:{playTime.Seconds:00}\n" +
                $"처치한 몬스터    {GameStats.MonsterKills}";
        }

        // 버튼: 마을로 이동하기. 엔딩 화면을 닫고 멈춘 시간을 되돌린 뒤, 마을에서 게임을 이어간다
        public void OnClickReturnToVillage()
        {
            Time.timeScale = 1f;
            // 봉인 컷씬이 숨겨 둔 UI와 막아 둔 입력, 분리한 카메라를 되돌린다
            SealingThrone.EndCutscene();

            if (panel != null)
            {
                panel.SetActive(false);
            }

            // 엔딩 직전까지 열어 둔 인벤토리 등 창을 닫은 채로 마을에 도착하게 한다
            UIWindowStack.CloseAll();

            PlayerRespawnController respawnController = FindAnyObjectByType<PlayerRespawnController>();
            if (respawnController != null)
            {
                // 마을에 도착하면(로딩이 걷히면) 세리나가 봉인 완성을 알린다. 한 번만 하도록 도착 알림은 바로 해제한다
                UITeleportScreen.TeleportFinished += ShowEpilogueDialogue;
                respawnController.ReturnToVillage();
            }
        }

        // 엔딩 뒤 마을로 돌아온 직후 세리나가 건네는 대사. "(닉네임)"은 대사 시스템이 플레이어 닉네임으로 바꾼다
        private const string EpilogueText =
            "[세리나] 봉인이 완성됐어요.  고마워요 (닉네임)님. \n이제 자유롭게 대륙을 탐험해 보세요.";

        private static void ShowEpilogueDialogue()
        {
            UITeleportScreen.TeleportFinished -= ShowEpilogueDialogue;
            Dialogue.ShowDialogueText(EpilogueText);
        }

        // 버튼: 로비로 돌아가기
        public void OnClickReturnToLobby()
        {
            Time.timeScale = 1f;
            // 봉인 컷씬이 숨겨 둔 UI와 막아 둔 입력, 분리한 카메라를 되돌린다
            SealingThrone.EndCutscene();
            SceneManager.LoadScene(lobbySceneName);
        }

        // 버튼: 게임 종료
        public void OnClickQuit()
        {
            Time.timeScale = 1f;
            // 봉인 컷씬이 숨겨 둔 UI와 막아 둔 입력, 분리한 카메라를 되돌린다
            SealingThrone.EndCutscene();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
