using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 메뉴 버튼에 마우스를 올리면 붉은 광채가 맥동하고 버튼이 살짝 커지는 연출.
    /// 광채는 버튼 모양 그대로의 자식 Image라서 버튼 자체의 색 전환(Button.colors)과 부딪치지 않는다
    /// </summary>
    public class UIButtonHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image glow;
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float glowMin = 0.18f;
        [SerializeField] private float glowMax = 0.42f;
        [SerializeField] private float pulseSpeed = 5f;
        [SerializeField] private float fadeSpeed = 12f;

        private bool hovered;
        private float amount;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }

        private void OnDisable()
        {
            // 팝업으로 가려져 비활성화되면 호버 상태가 남지 않게 초기화한다
            hovered = false;
            amount = 0f;
            Apply();
        }

        private void Update()
        {
            float goal = hovered ? 1f : 0f;
            amount = Mathf.MoveTowards(amount, goal, fadeSpeed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            if (glow != null)
            {
                float pulse = Mathf.Lerp(glowMin, glowMax, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
                Color color = glow.color;
                color.a = pulse * amount;
                glow.color = color;
            }

            transform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, amount);
        }
    }
}
