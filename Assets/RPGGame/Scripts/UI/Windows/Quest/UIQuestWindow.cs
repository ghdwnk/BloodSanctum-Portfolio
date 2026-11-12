using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 왼쪽엔 탭별 퀘스트 제목 목록, 오른쪽엔 선택한 퀘스트의 상세 정보(설명/목표/보상)를 보여주는 퀘스트창
    /// </summary>
    public class UIQuestWindow : MonoBehaviour
    {
        public enum Tab { Available, InProgress, Completed }

        private static UIQuestWindow instance;
        [SerializeField] private GameObject window;
        [SerializeField] private UIQuestListItem itemPrefab;
        [SerializeField] private UIQuestDetailPanel detailPanel;

        [SerializeField] private RectTransform availableContent;
        [SerializeField] private RectTransform inProgressContent;
        [SerializeField] private RectTransform completedContent;

        [SerializeField] private GameObject availableScrollView;
        [SerializeField] private GameObject inProgressScrollView;
        [SerializeField] private GameObject completedScrollView;

        [SerializeField] private Button availableTabButton;
        [SerializeField] private Button inProgressTabButton;
        [SerializeField] private Button completedTabButton;

        [Tooltip("선택되지 않은 탭의 흐림 정도(가시성을 위해 선택된 탭만 완전히 밝게 표시)")]
        [SerializeField] private float unselectedTabAlpha = 0.45f;

        // questID를 기준으로 생성된 퀘스트 아이템 UI를 찾기 위한 딕셔너리
        private readonly Dictionary<int, UIQuestListItem> itemsByQuestID = new();
        private Tab currentTab = Tab.Available;
        private int selectedQuestID = -1;

        public static bool IsOn => instance != null && instance.window.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            QuestManager.Instance.SubscribeOnQuestAccepted(OnQuestAccepted);
            QuestManager.Instance.SubscribeOnQuestProgressChanged(OnQuestProgressChanged);
            QuestManager.Instance.SubscribeOnQuestReadyToTurnIn(OnQuestReadyToTurnIn);
            QuestManager.Instance.SubscribeOnQuestCompleted(OnQuestCompleted);
            QuestManager.Instance.SubscribeOnQuestUnlocked(OnQuestUnlocked);
            QuestManager.Instance.SubscribeOnQuestReset(OnQuestReset);
            QuestManager.Instance.SubscribeOnStateLoaded(OnStateLoaded);

            // 퀘스트 목록 UI 생성. 전체 퀘스트를 각자의 현재 상태에 맞는 탭에 배치한다
            foreach (QuestData.Quest quest in DataManager.Instance.questData.quests)
            {
                QuestManager.QuestProgressState state = QuestManager.Instance.GetState(quest.questID);
                UIQuestListItem newItem = Instantiate(itemPrefab, ContentForState(state));
                newItem.Initialize(quest);
                newItem.SetStateVisual(state);
                newItem.OnClicked += OnItemClicked;

                itemsByQuestID[quest.questID] = newItem;
            }

            availableTabButton.onClick.AddListener(() => SelectTab(Tab.Available));
            inProgressTabButton.onClick.AddListener(() => SelectTab(Tab.InProgress));
            completedTabButton.onClick.AddListener(() => SelectTab(Tab.Completed));

            SelectTab(Tab.Available);
        }

        public static void Show()
        {
            UISound.SetActive(instance.window, true);
            // 창을 열 때마다 진행중 탭을 먼저 보여준다
            instance.SelectTab(Tab.InProgress);
        }

        public static void Close()
        {
            UISound.SetActive(instance.window, false);
        }

        // 탭을 전환하는 함수
        public void SelectTab(Tab tab)
        {
            currentTab = tab;

            availableScrollView.SetActive(tab == Tab.Available);
            inProgressScrollView.SetActive(tab == Tab.InProgress);
            completedScrollView.SetActive(tab == Tab.Completed);

            SetTabButtonHighlight(availableTabButton, tab == Tab.Available);
            SetTabButtonHighlight(inProgressTabButton, tab == Tab.InProgress);
            SetTabButtonHighlight(completedTabButton, tab == Tab.Completed);

            // 탭을 바꾸면 그 탭의 첫 번째 퀘스트를 자동으로 선택해 상세 패널을 채운다
            RectTransform content = ContentForState(tab == Tab.Available ? QuestManager.QuestProgressState.Available :
                tab == Tab.InProgress ? QuestManager.QuestProgressState.Accepted : QuestManager.QuestProgressState.Completed);

            UIQuestListItem firstItem = content.GetComponentsInChildren<UIQuestListItem>().FirstOrDefault();
            if (firstItem != null)
            {
                SelectItem(firstItem.QuestID);
            }
            else
            {
                selectedQuestID = -1;
                detailPanel.Clear();
            }
        }

        // 선택되지 않은 탭은 흐리게, 선택된 탭만 원래 색으로 표시해 가시성 있게 표시
        private void SetTabButtonHighlight(Button button, bool selected)
        {
            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = selected ? 1f : unselectedTabAlpha;
            image.color = color;
        }

        // 퀘스트 상태에 맞는 탭 컨텐츠 부모를 반환
        // 잠김 탭이 없으므로 아직 시작 전인 퀘스트(잠김/수락가능)는 모두 진행가능 탭에 모아 전체 목록이 보이게 한다
        // Accepted/ReadyToTurnIn은 진행중 탭에 표시
        private RectTransform ContentForState(QuestManager.QuestProgressState state)
        {
            return state switch
            {
                QuestManager.QuestProgressState.Locked => availableContent,
                QuestManager.QuestProgressState.Available => availableContent,
                QuestManager.QuestProgressState.Completed => completedContent,
                _ => inProgressContent
            };
        }

        private void OnItemClicked(UIQuestListItem item)
        {
            SelectItem(item.QuestID);
        }

        private void SelectItem(int questID)
        {
            selectedQuestID = questID;

            foreach (var kv in itemsByQuestID)
            {
                kv.Value.SetSelected(kv.Key == questID);
            }

            QuestData.Quest quest = QuestManager.Instance.GetQuest(questID);
            if (quest != null)
            {
                detailPanel.SetQuest(quest, QuestManager.Instance.GetProgress(questID));
            }
        }

        private void MoveItem(int questID, QuestManager.QuestProgressState state, int progress)
        {
            if (!itemsByQuestID.TryGetValue(questID, out UIQuestListItem item))
            {
                return;
            }

            item.SetStateVisual(state);
            item.transform.SetParent(ContentForState(state), false);

            if (questID == selectedQuestID)
            {
                QuestData.Quest quest = QuestManager.Instance.GetQuest(questID);
                if (quest != null)
                {
                    detailPanel.SetQuest(quest, progress);
                }
            }
        }

        private void OnQuestAccepted(int questID)
        {
            MoveItem(questID, QuestManager.QuestProgressState.Accepted, 0);
        }

        private void OnQuestProgressChanged(int questID, int progress)
        {
            if (questID == selectedQuestID)
            {
                QuestData.Quest quest = QuestManager.Instance.GetQuest(questID);
                if (quest != null)
                {
                    detailPanel.UpdateProgress(quest, progress);
                }
            }
        }

        private void OnQuestReadyToTurnIn(int questID)
        {
            MoveItem(questID, QuestManager.QuestProgressState.ReadyToTurnIn, QuestManager.Instance.GetProgress(questID));
        }

        private void OnQuestCompleted(int questID)
        {
            MoveItem(questID, QuestManager.QuestProgressState.Completed, QuestManager.Instance.GetProgress(questID));
        }

        private void OnQuestUnlocked(int questID)
        {
            MoveItem(questID, QuestManager.QuestProgressState.Available, 0);
        }

        // 이어하기로 세이브를 불러온 직후: 창은 불러오기 전 상태로 목록을 만들어 두었으므로,
        // 모든 퀘스트를 현재 상태에 맞는 탭·색으로 다시 옮기고 보고 있던 탭의 상세 패널을 새로 채운다
        private void OnStateLoaded()
        {
            foreach (QuestData.Quest quest in DataManager.Instance.questData.quests)
            {
                MoveItem(quest.questID, QuestManager.Instance.GetState(quest.questID), QuestManager.Instance.GetProgress(quest.questID));
            }

            SelectTab(currentTab);
        }

        // 테스트 도구로 처음 상태로 되돌려진 퀘스트: 되돌려진 상태(잠김/수락 가능)에 맞는 자리와 색으로 옮긴다
        private void OnQuestReset(int questID)
        {
            MoveItem(questID, QuestManager.Instance.GetState(questID), 0);
        }
    }
}
