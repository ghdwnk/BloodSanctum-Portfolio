using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 상호작용 대상(NPC·채집물·텔레포트 게이트·봉인 왕좌) 머리 위에 뜨는 "F : 대화하기" 같은 안내 UI.
    ///
    /// 대상 오브젝트의 자식에 월드 스페이스 Canvas로 붙어 있고, 각 대상이 플레이어가 범위에 들어오면 Show,
    /// 나가면 Hide를 부른다. 켜져 있는 동안에는 글자가 항상 카메라 정면을 보도록 돌린다.
    /// </summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [Tooltip("켜고 끌 안내 UI의 최상위 오브젝트 (월드 스페이스 Canvas)")]
        [SerializeField] private GameObject promptRoot;

        [Tooltip("안내 문구를 표시할 텍스트")]
        [SerializeField] private TextMeshProUGUI promptText;

        private Camera mainCamera;

        private void Awake()
        {
            // 처음에는 숨겨 둔다. 플레이어가 범위에 들어오면 대상 쪽에서 Show를 부른다
            if (promptRoot != null)
            {
                promptRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 문구를 바꾸고 안내를 켠다. 대상이 범위 안에 있는 동안 매 프레임 불리므로,
        /// 문구가 같으면 글자를 다시 만들지 않고 이미 켜져 있으면 SetActive도 건너뛴다.
        /// </summary>
        public void Show(string text)
        {
            if (promptText != null && promptText.text != text)
            {
                promptText.text = text;
            }

            if (promptRoot != null && !promptRoot.activeSelf)
            {
                promptRoot.SetActive(true);
            }
        }

        /// <summary>안내를 끈다.</summary>
        public void Hide()
        {
            if (promptRoot != null && promptRoot.activeSelf)
            {
                promptRoot.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // 꺼져 있으면 회전시킬 필요가 없다 (대부분의 시간은 여기서 끝난다)
            if (promptRoot == null || !promptRoot.activeSelf)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }
            }

            // 빌보드: 카메라와 같은 방향을 보게 해 글자가 뒤집히거나 기울지 않게 한다
            transform.forward = mainCamera.transform.forward;
        }
    }
}
