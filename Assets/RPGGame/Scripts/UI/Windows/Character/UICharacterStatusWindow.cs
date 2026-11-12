using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// U키로 여닫는 캐릭터 정보창. 장비 슬롯(표시 전용, 장비 아이템 시스템은 아직 없음)과 스탯 패널을 보여준다
    /// </summary>
    public class UICharacterStatusWindow : MonoBehaviour
    {
        private static UICharacterStatusWindow instance;

        [SerializeField] private GameObject window;
        [SerializeField] private RectTransform windowTransform;
        [SerializeField] private Vector2 initialPosition = new Vector2(-30f, -50f);

        public static bool IsOn => instance != null && instance.window.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void Show()
        {
            UISound.SetActive(instance.window, true);

            // 드래그로 옮겨놨어도 열 때마다 항상 초기 위치로 되돌림
            if (instance.windowTransform != null)
            {
                instance.windowTransform.anchoredPosition = instance.initialPosition;
            }
        }

        public static void Close()
        {
            UISound.SetActive(instance.window, false);
        }
    }
}
