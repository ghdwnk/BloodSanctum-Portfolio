using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 로비에 처음 들어왔을 때 메뉴 버튼이 아래에서 한 줄씩 떠오르는 등장 연출.
    /// 로고는 연출 없이 그대로 보여 준다
    /// </summary>
    public class UILobbyIntro : MonoBehaviour
    {
        [Tooltip("등장이 시작되기까지의 대기 시간과 버튼 사이 간격")]
        [SerializeField] private float buttonStartDelay = 0.4f;
        [SerializeField] private float buttonInterval = 0.18f;
        [SerializeField] private float buttonRiseTime = 0.55f;
        [SerializeField] private float buttonRiseDistance = 40f;
        [SerializeField] private RectTransform[] menuButtons;

        private void Start()
        {
            StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            // 버튼은 일단 숨기고, 원래 자리를 기억해 둔다
            int count = menuButtons != null ? menuButtons.Length : 0;
            CanvasGroup[] groups = new CanvasGroup[count];
            Vector2[] homes = new Vector2[count];

            for (int i = 0; i < count; ++i)
            {
                RectTransform button = menuButtons[i];
                homes[i] = button.anchoredPosition;

                groups[i] = button.GetComponent<CanvasGroup>();
                if (groups[i] == null)
                {
                    groups[i] = button.gameObject.AddComponent<CanvasGroup>();
                }

                groups[i].alpha = 0f;
                // 나타나는 동안 눌리지 않게 막는다
                groups[i].blocksRaycasts = false;
                button.anchoredPosition = homes[i] + Vector2.down * buttonRiseDistance;
            }

            float elapsed = 0f;
            float total = buttonStartDelay + buttonInterval * Mathf.Max(0, count - 1) + buttonRiseTime;

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;

                for (int i = 0; i < count; ++i)
                {
                    float t = EaseOut(Mathf.Clamp01((elapsed - buttonStartDelay - i * buttonInterval) / buttonRiseTime));
                    groups[i].alpha = t;
                    menuButtons[i].anchoredPosition = homes[i] + Vector2.down * (buttonRiseDistance * (1f - t));
                }

                yield return null;
            }

            for (int i = 0; i < count; ++i)
            {
                groups[i].alpha = 1f;
                groups[i].blocksRaycasts = true;
                menuButtons[i].anchoredPosition = homes[i];
            }
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }
    }
}
