using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 텔레포트 이동 목록의 한 줄. 장소 이름과 설명을 보여 주고, 마우스 왼쪽 클릭으로 그곳으로 이동한다.
    /// 갈 수 없는 곳(퀘스트 조건 미달)은 붉은 배경과 [잠김] 표시를 하고, 눌러 보면 이유를 안내한다
    /// </summary>
    public class UITeleportListItem : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private Button selectButton;

        private static readonly Color NormalColor = new Color(0.13f, 0.13f, 0.15f, 0.85f);
        private static readonly Color LockedColor = new Color(0.35f, 0.10f, 0.12f, 0.85f);

        /// <param name="onClick">줄을 눌렀을 때 실행할 동작. 잠긴 줄도 눌려야 안내를 띄우므로 버튼은 항상 켜 둔다</param>
        public void Set(string displayName, string description, bool isLocked, UnityAction onClick,
            TeleportNetwork.QuestMarker questMarker = TeleportNetwork.QuestMarker.None)
        {
            if (nameText != null)
            {
                nameText.text = displayName;
            }

            if (infoText != null)
            {
                infoText.text = description;
            }

            if (stateText != null)
            {
                // 잠금 표시가 우선이고, 풀려 있으면 퀘스트 표시를 보여 준다 (NPC 머리 위 표식과 같은 노란색)
                stateText.text = isLocked ? "[잠김]"
                    : questMarker != TeleportNetwork.QuestMarker.None ? "<color=#FFD84A>퀘스트</color>"
                    : string.Empty;
            }

            if (background != null)
            {
                background.color = isLocked ? LockedColor : NormalColor;
            }

            if (selectButton != null)
            {
                // 줄을 재사용하므로 이전 콜백이 남지 않게 매번 비운다
                selectButton.onClick.RemoveAllListeners();

                if (onClick != null)
                {
                    selectButton.onClick.AddListener(onClick);
                }
            }
        }
    }
}
