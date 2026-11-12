using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 비활성화(interactable = false)된 버튼을 눌렀을 때 이유를 작은 안내(토스트)로 알려 주는 컴포넌트.
    /// Unity의 Button은 비활성 상태에서는 onClick을 호출하지 않지만 포인터 이벤트는 여전히 받는다.
    /// 그래서 버튼을 켜 두는 대신(= 회색 처리와 클릭 불가 규칙을 건드리지 않고) 클릭 이벤트만 따로 받아 안내한다.
    /// 이유 문구는 목록이 갱신될 때마다 호출하는 쪽이 SetReason으로 넘겨 준다
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIBlockedButtonNotice : MonoBehaviour, IPointerClickHandler
    {
        private Button button;
        private string reason = string.Empty;

        // 버튼에 이 컴포넌트를 붙이고 이유를 설정하는 편의 함수. 줄이 재사용돼도 컴포넌트는 한 번만 만든다
        public static void Apply(Button target, bool interactable, string blockedReason)
        {
            if (target == null)
            {
                return;
            }

            UIBlockedButtonNotice notice = target.GetComponent<UIBlockedButtonNotice>();

            if (notice == null)
            {
                notice = target.gameObject.AddComponent<UIBlockedButtonNotice>();
            }

            // 눌러도 되는 버튼이면 이유를 비워 두어 안내가 뜨지 않게 한다
            notice.reason = interactable ? string.Empty : blockedReason;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            // 눌러도 되는 버튼은 Button이 직접 처리하므로 막힌 경우에만 안내한다
            if (button != null && !button.interactable && !string.IsNullOrEmpty(reason))
            {
                UISystemMessage.ShowToastWarning(reason);
            }
        }
    }
}
