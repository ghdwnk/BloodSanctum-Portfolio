using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 스킬/아이템을 퀵슬롯으로 드래그할 때 커서를 따라다니는 아이콘 생성을 돕는 공용 헬퍼
    /// </summary>
    public static class UIDragIcon
    {
        public static RectTransform Create(Sprite sprite, Transform canvasTransform, Vector2 size)
        {
            if (sprite == null || canvasTransform == null)
            {
                return null;
            }

            GameObject go = new GameObject("Drag Icon", typeof(RectTransform));
            go.transform.SetParent(canvasTransform, false);
            go.transform.SetAsLastSibling();

            RectTransform rectTransform = go.GetComponent<RectTransform>();
            rectTransform.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            CanvasGroup canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.85f;
            canvasGroup.blocksRaycasts = false;

            return rectTransform;
        }
    }
}
