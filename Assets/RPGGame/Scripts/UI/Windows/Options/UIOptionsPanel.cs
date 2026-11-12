using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement; // 로비 씬 이동용
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 해상도/전체화면/음량 옵션 UI.
    /// </summary>
    public class UIOptionsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject exitPanelRoot;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        // 배경음/효과음을 따로 조절한다. 기존 volumeSlider/muteToggle 연결은 배경음 쪽이 그대로 이어받는다
        [FormerlySerializedAs("volumeSlider")] [SerializeField] private Slider bgmSlider;
        [FormerlySerializedAs("muteToggle")] [SerializeField] private Toggle bgmMuteToggle;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle sfxMuteToggle;
        [SerializeField] private string lobbySceneName = "BloodSanctumGameStart"; // 게임 종료 시 이동할 로비 씬 이름
        [Tooltip("조작키 안내창을 여는 버튼과 창. 비어 있어도 나머지 옵션은 동작한다")]
        [SerializeField] private Button keyGuideButton;
        [SerializeField] private UIKeyGuideWindow keyGuideWindow;

        // 다른 창들과 같은 방식으로 열림 상태를 외부에서 조회할 수 있게 한다 (커서 표시 판단용)
        private static UIOptionsPanel instance;

        public static bool IsOn =>
            instance != null &&
            ((instance.panelRoot != null && instance.panelRoot.activeSelf) ||
             (instance.exitPanelRoot != null && instance.exitPanelRoot.activeSelf));

        // 인스패터에 저장되는 Persistent Listener는 동적(dynamic) int/float/bool 인자 전달이 저장 과정에서 정적 값 0으로 깨지는 문제가 확인되어,
        // 코드에서 직접 AddListener로 연결한다. (실제 UI 조작 시 값이 제대로 전달되는지 보장됨)
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            bgmSlider.onValueChanged.AddListener(GameSettings.SetBgmVolume);
            sfxSlider.onValueChanged.AddListener(GameSettings.SetSfxVolume);
            bgmMuteToggle.onValueChanged.AddListener(GameSettings.SetBgmMuted);
            sfxMuteToggle.onValueChanged.AddListener(GameSettings.SetSfxMuted);

            if (keyGuideButton != null && keyGuideWindow != null)
            {
                keyGuideButton.onClick.AddListener(keyGuideWindow.Open);
            }

            // 환경설정 패널과 나가기 확인창의 모든 버튼에 클릭음을 붙인다
            UISound.AttachClick(panelRoot != null ? panelRoot.transform : transform);
            UISound.AttachClick(exitPanelRoot != null ? exitPanelRoot.transform : null);
        }

        private void OnEnable()
        {
            RefreshFromSavedSettings();
        }

        private void RefreshFromSavedSettings()
        {
            PopulateResolutionDropdown();
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.GetSavedFullscreen());
            bgmSlider.SetValueWithoutNotify(GameSettings.GetSavedBgmVolume());
            sfxSlider.SetValueWithoutNotify(GameSettings.GetSavedSfxVolume());
            bgmMuteToggle.SetIsOnWithoutNotify(GameSettings.GetSavedBgmMuted());
            sfxMuteToggle.SetIsOnWithoutNotify(GameSettings.GetSavedSfxMuted());
        }

        private void PopulateResolutionDropdown()
        {
            Resolution[] resolutions = GameSettings.GetAvailableResolutions();
            List<string> options = new List<string>(resolutions.Length);

            for (int i = 0; i < resolutions.Length; i++)
            {
                options.Add($"{resolutions[i].width} x {resolutions[i].height}");
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(GameSettings.GetSavedResolutionIndex());
        }

        // 해상도/전체화면은 값이 바뀌는 즉시 적용한다 (적용 버튼 제거, 음량과 동일한 실시간 적용 방식으로 통일)
        /// <summary>
        /// 해상도 드롭다운 값이 바뀌면 즉시 적용한다.
        /// TMP_Dropdown의 OnValueChanged(int) 이벤트에 연결한다.
        /// </summary>
        public void OnResolutionChanged(int index)
        {
            GameSettings.SetResolution(index, fullscreenToggle.isOn);
        }

        /// <summary>
        /// 전체화면 토글이 바뀌면 즉시 적용한다.
        /// Toggle의 OnValueChanged(bool) 이벤트에 연결한다.
        /// </summary>
        public void OnFullscreenChanged(bool isFullscreen)
        {
            GameSettings.SetResolution(resolutionDropdown.value, isFullscreen);
        }


        public void Open()
        {
            UISound.SetActive(panelRoot, true);
        }

        public void Close()
        {
            // 안내창은 패널의 자식이라 패널만 끄면 켜진 채로 남아, 다음에 패널을 열 때 같이 보인다
            if (keyGuideWindow != null)
            {
                keyGuideWindow.gameObject.SetActive(false);
            }

            UISound.SetActive(panelRoot, false);
        }

        public void ExitToLobby()
        {
            panelRoot.SetActive(false);
            exitPanelRoot.SetActive(true);
        }

        public void YesButton()
        {
            // 로비로 나가기 전 현재 진행 상황을 저장 (인게임 씬이 아니면 GameSaveLoader가 없어 자연히 무시됨)
            GameSaveLoader saveLoader = FindAnyObjectByType<GameSaveLoader>();
            if (saveLoader != null)
            {
                saveLoader.SaveNow();
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(lobbySceneName);
        }

        public void NoButton()
        {
            exitPanelRoot.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
