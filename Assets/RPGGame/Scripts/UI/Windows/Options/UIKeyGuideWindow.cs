using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 환경설정(ESC) 창의 '키 가이드' 버튼으로 여는 조작키 안내창.
    /// 환경설정 패널 위에 겹쳐 뜨는 자식 오브젝트이고, 목록(키 아이콘 + 설명)은 씬에 배치돼 있어 이 스크립트는 여닫기만 맡는다
    /// </summary>
    public class UIKeyGuideWindow : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        private static UIKeyGuideWindow instance;

        // 환경설정 패널이 열린 동안에만 의미가 있으므로 UIWindowState에는 따로 넣지 않는다 (패널이 이미 커서/일시정지를 잡고 있다)
        public static bool IsOn => instance != null && instance.gameObject.activeSelf;

        private void Awake()
        {
            instance = this;

            UISound.AttachClick(transform);

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Open()
        {
            UISound.SetActive(gameObject, true);
        }

        public void Close()
        {
            UISound.SetActive(gameObject, false);
        }

        /// <summary>
        /// ESC를 눌렀을 때 키 가이드가 떠 있으면 가이드만 닫고 true를 돌려준다 (환경설정 패널은 그대로 둔다)
        /// </summary>
        public static bool TryClose()
        {
            if (!IsOn)
            {
                return false;
            }

            instance.Close();
            return true;
        }
    }
}
