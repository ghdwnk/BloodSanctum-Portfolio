using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 머리 위에 닉네임을 띄우는 컴포넌트 (UINPCNameTag와 같은 월드 캔버스 이름표 프리팹을 쓴다).
    /// 닉네임이 바뀌면 이벤트로 따라가므로 매 프레임 갱신하지 않는다
    /// </summary>
    public class UIPlayerNameTag : MonoBehaviour
    {
        // 상태 문구는 닉네임보다 작게, 캔버스 단위(1/100 m)로 이만큼 위에 둔다
        private const float StatusFontSize = 11f;
        private const float StatusHeight = 20f;
        // 상태가 여러 개 겹치면 한 줄씩 위로 쌓는다. 줄 사이 간격
        private const float StatusLineGap = 13f;

        [SerializeField] private TextMeshProUGUI nameText;

        // 상태 이름(slow, burn 등)별 문구와 켜진 순서. 먼저 걸린 상태가 닉네임에 가까운 아래쪽에 놓인다
        private readonly Dictionary<string, TextMeshProUGUI> statusTexts = new Dictionary<string, TextMeshProUGUI>();
        private readonly List<string> statusOrder = new List<string>();

        private void OnEnable()
        {
            Refresh(PlayerProfile.Nickname);
            PlayerProfile.NicknameChanged += Refresh;
        }

        private void OnDisable()
        {
            PlayerProfile.NicknameChanged -= Refresh;
        }

        private void Refresh(string nickname)
        {
            if (nameText != null)
            {
                nameText.text = nickname;
            }
        }

        // 닉네임 위쪽에 상태 문구(느려짐!, 화상피해! 등)를 계속 띄운다. key는 상태 종류를 구분하는 이름이라
        // 같은 key로 다시 부르면 문구만 갱신되고, 다른 key끼리는 한 줄씩 따로 쌓인다.
        // 프리팹을 고치지 않고 닉네임 텍스트를 복제해 쓰므로 폰트와 머티리얼이 닉네임과 같다
        public void SetStatus(string key, string content, Color color)
        {
            if (nameText == null)
            {
                return;
            }

            if (!statusTexts.TryGetValue(key, out TextMeshProUGUI text) || text == null)
            {
                text = Instantiate(nameText, nameText.transform.parent);
                text.name = "Status Text (" + key + ")";
                text.enableAutoSizing = false;
                text.fontSize = StatusFontSize;
                statusTexts[key] = text;
            }

            text.text = content;
            text.color = color;
            text.gameObject.SetActive(true);

            if (!statusOrder.Contains(key))
            {
                statusOrder.Add(key);
            }

            LayoutStatuses();
        }

        public void ClearStatus(string key)
        {
            if (statusTexts.TryGetValue(key, out TextMeshProUGUI text) && text != null)
            {
                text.gameObject.SetActive(false);
            }

            // 켜져 있던 상태가 빠지면 위에 쌓인 문구가 빈자리로 내려오도록 다시 배치한다
            if (statusOrder.Remove(key))
            {
                LayoutStatuses();
            }
        }

        private void LayoutStatuses()
        {
            for (int ix = 0; ix < statusOrder.Count; ++ix)
            {
                TextMeshProUGUI text = statusTexts[statusOrder[ix]];
                if (text != null)
                {
                    text.rectTransform.anchoredPosition =
                        nameText.rectTransform.anchoredPosition + Vector2.up * (StatusHeight + StatusLineGap * ix);
                }
            }
        }
    }
}
