using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 로비의 시작 메뉴. "시작하기" → "새로하기 / 이어하기" 선택 → 새로하기는 닉네임 입력과 배경 이야기를 거쳐, 이어하기는 바로 로딩 화면으로 넘어간다.
    /// 세이브가 없으면 이어하기 버튼을 막는다. 버튼들이 인스펙터로 StartGame / OnClickNewGame / OnClickContinue / CloseNewGameContinuePanel 등을 부른다 (이름을 바꾸지 않는다).
    /// </summary>
    public class UIGameStart : MonoBehaviour
    {
        [SerializeField] private GameObject loadUI;
        [Tooltip("시작하기를 누르면 숨기고, 새로하기/이어하기 선택 팝업을 대신 보여준다")]
        [SerializeField] private GameObject startMenuRoot;
        [SerializeField] private GameObject newGameContinuePanel;
        [SerializeField] private Button continueButton;
        [Tooltip("새로하기를 누르면 로딩 전에 보여주는 배경 이야기 화면. 비어 있으면 곧바로 로딩한다")]
        [SerializeField] private UIIntroScreen introScreen;
        [Tooltip("인게임 환경설정과 같은 조작키 안내창을 로비에서도 여는 버튼과 창")]
        [SerializeField] private Button keyGuideButton;
        [SerializeField] private UIKeyGuideWindow keyGuideWindow;

        // 새로하기 때 처음 한 번 코드로 만든다
        private UINicknameInput nicknameInput;

        private void Awake()
        {
            // 로비의 모든 버튼(시작/키 가이드/설정 창 안의 것 포함)에 클릭음을 붙인다
            Canvas lobbyCanvas = startMenuRoot != null ? startMenuRoot.GetComponentInParent<Canvas>() : null;
            if (lobbyCanvas == null)
            {
                lobbyCanvas = FindAnyObjectByType<Canvas>();
            }

            if (lobbyCanvas != null)
            {
                UISound.AttachClick(lobbyCanvas.rootCanvas.transform);
            }

            if (keyGuideButton != null && keyGuideWindow != null)
            {
                keyGuideButton.onClick.AddListener(keyGuideWindow.Open);
            }
        }

        private void OnEnable()
        {
            RefreshContinueButton();
        }

        // 세이브 데이터가 없으면 이어하기 버튼을 비활성화한다
        private void RefreshContinueButton()
        {
            if (continueButton != null)
            {
                continueButton.interactable = SaveManager.HasSaveData();
            }
        }

        public void StartGame()
        {
            RefreshContinueButton();
            startMenuRoot.SetActive(false);
            newGameContinuePanel.SetActive(true);
        }

        // 새로하기/이어하기 팝업에서 뒤로가기
        public void CloseNewGameContinuePanel()
        {
            newGameContinuePanel.SetActive(false);
            startMenuRoot.SetActive(true);
        }

        // 새로하기 - 먼저 닉네임을 입력받는다. 기존 세이브는 닉네임을 확정한 뒤에 지워서,
        // 입력창에서 뒤로 가면 이어하기 데이터가 그대로 남는다
        public void OnClickNewGame()
        {
            newGameContinuePanel.SetActive(false);

            if (nicknameInput == null)
            {
                Canvas canvas = loadUI != null ? loadUI.GetComponentInParent<Canvas>() : null;
                if (canvas == null)
                {
                    canvas = FindAnyObjectByType<Canvas>();
                }

                nicknameInput = UINicknameInput.Create(canvas);
            }

            // 입력창을 만들지 못하면(프리팹 없음) 닉네임 없이 기본 이름으로 진행한다
            if (nicknameInput == null)
            {
                StartNewGame(string.Empty);
                return;
            }

            nicknameInput.Show(string.Empty, StartNewGame, () => newGameContinuePanel.SetActive(true));
        }

        // 닉네임이 정해진 뒤 실제로 새 게임을 시작한다
        private void StartNewGame(string nickname)
        {
            SaveManager.DeleteSave();
            SaveManager.ShouldLoadOnStart = false;
            PlayerProfile.SetNickname(nickname);

            // 처음부터 시작할 때만 배경 이야기를 보여준다 (이어하기는 이미 본 이야기이므로 건너뛴다)
            if (introScreen != null)
            {
                introScreen.Play(() => loadUI.SetActive(true));
                return;
            }

            loadUI.SetActive(true);
        }

        // 이어하기 - 세이브 데이터가 있을 때만 동작
        public void OnClickContinue()
        {
            if (!SaveManager.HasSaveData())
            {
                return;
            }

            SaveManager.ShouldLoadOnStart = true;
            newGameContinuePanel.SetActive(false);
            loadUI.SetActive(true);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
