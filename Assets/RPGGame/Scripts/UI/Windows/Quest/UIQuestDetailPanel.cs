using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 퀘스트 목록에서 선택한 퀘스트 하나의 상세 정보(제목/설명/목표/보상)를 보여주는 패널
    /// </summary>
    public class UIQuestDetailPanel : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private TMPro.TextMeshProUGUI titleText;
        [SerializeField] private TMPro.TextMeshProUGUI descriptionText;
        [SerializeField] private TMPro.TextMeshProUGUI objectiveText;

        [SerializeField] private TMPro.TextMeshProUGUI goldCountText;
        [SerializeField] private TMPro.TextMeshProUGUI expCountText;

        [SerializeField] private GameObject itemRewardSlot;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private TMPro.TextMeshProUGUI itemCountText;

        private int questID = -1;

        public int QuestID => questID;

        public void SetQuest(QuestData.Quest quest, int progress)
        {
            questID = quest.questID;

            if (content != null) content.SetActive(true);

            if (titleText != null) titleText.text = quest.questTitle;
            if (descriptionText != null) descriptionText.text = quest.questDescription;

            if (objectiveText != null)
            {
                string label = string.IsNullOrEmpty(quest.objectiveLabel) ? quest.questTitle : quest.objectiveLabel;
                objectiveText.text = $"→ {label} {progress}/{quest.countToComplete}";
            }

            if (goldCountText != null) goldCountText.text = quest.rewardGold.ToString();
            if (expCountText != null) expCountText.text = quest.exp.ToString("0");

            bool hasItemReward = !string.IsNullOrEmpty(quest.rewardItemDisplayName);
            if (itemRewardSlot != null) itemRewardSlot.SetActive(hasItemReward);
            if (hasItemReward && itemCountText != null)
            {
                itemCountText.text = $"{quest.rewardItemDisplayName} x{quest.rewardItemCount}";
            }

            // 보상 아이콘을 실제 아이템 에셋에서 가져온다.
            // 이걸 빼먹으면 씬에 자리표시자로 넣어 둔 스프라이트(붉은 보석) 하나가 모든 퀘스트에 그대로 남는다.
            // 상점/강화/장비 슬롯과 같은 규칙(sprite가 없으면 Image 자체를 끈다)을 따른다
            if (hasItemReward && itemIconImage != null)
            {
                Sprite icon = quest.rewardItem != null ? quest.rewardItem.sprite : null;
                itemIconImage.sprite = icon;
                itemIconImage.enabled = icon != null;
            }
        }

        // 진행도만 갱신할 때 사용 (선택된 퀘스트의 목표 달성 횟수가 바뀔 때)
        public void UpdateProgress(QuestData.Quest quest, int progress)
        {
            if (questID != quest.questID || objectiveText == null)
            {
                return;
            }

            string label = string.IsNullOrEmpty(quest.objectiveLabel) ? quest.questTitle : quest.objectiveLabel;
            objectiveText.text = $"→ {label} {progress}/{quest.countToComplete}";
        }

        public void Clear()
        {
            questID = -1;

            if (content != null) content.SetActive(false);
        }
    }
}
