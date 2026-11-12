using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 퀘스트 목록 창 왼쪽 목록에서 퀘스트 하나(제목 + 상태 색상)를 표시하는 행.
    /// 클릭하면 OnClicked를 통해 UIQuestWindow에 선택을 알린다
    /// </summary>
    public class UIQuestListItem : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image stateAccentImage;
        [SerializeField] private Color lockedColor;
        [SerializeField] private Color availableColor;
        [SerializeField] private Color progressColor;
        [SerializeField] private Color completeColor;

        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color normalBackgroundColor;
        [SerializeField] private Color selectedBackgroundColor;

        [SerializeField] private TMPro.TextMeshProUGUI questTitleText;

        private int questID;

        public int QuestID => questID;
        public event Action<UIQuestListItem> OnClicked;

        public void Initialize(QuestData.Quest quest)
        {
            questID = quest.questID;

            if (questTitleText != null) questTitleText.text = quest.questTitle;
        }

        public void SetStateVisual(QuestManager.QuestProgressState state)
        {
            Color color = state switch
            {
                QuestManager.QuestProgressState.Locked => lockedColor,
                QuestManager.QuestProgressState.Available => availableColor,
                QuestManager.QuestProgressState.Completed => completeColor,
                _ => progressColor
            };

            if (stateAccentImage != null) stateAccentImage.color = color;
        }

        public void SetSelected(bool selected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? selectedBackgroundColor : normalBackgroundColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke(this);
        }
    }
}
