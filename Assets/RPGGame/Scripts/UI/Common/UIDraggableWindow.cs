using UnityEngine;
using UnityEngine.EventSystems;

namespace RPGGame
{
    /// <summary>
    /// 타이틀 바에 붙여서 좌클릭 드래그로 창(windowTransform)을 이동시키는 범용 스크립트.
    /// 인벤토리/퀘스트 창처럼 제목 표시줄이 있는 모든 창에 재사용 가능.
    /// </summary>
    public class UIDraggableWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("드래그로 움직일 대상 창의 RectTransform (보통 이 오브젝트의 조상)")]
        [SerializeField] private RectTransform windowTransform;

        private RectTransform parentRectTransform;
        private Vector2 pointerOffset;

        private void Awake()
        {
            if (windowTransform == null)
            {
                windowTransform = transform.parent as RectTransform;
            }

            parentRectTransform = windowTransform.parent as RectTransform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            pointerOffset = localPoint - windowTransform.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            windowTransform.anchoredPosition = localPoint - pointerOffset;
        }
    }
}
