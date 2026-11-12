using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 시스템 안내를 띄우는 작은 창.
    /// 하단 대화창(Dialogue)은 NPC 대사 전용으로 두고, 구매 실패·강화 결과 같은 시스템 메시지는 전부 여기로 보낸다.
    ///
    /// 두 가지 형태를 제공한다.
    ///  - Show   : 확인 버튼이 달린 모달. 사용자가 눌러야 닫히므로 반드시 읽어야 하는 안내에 쓴다
    ///  - ShowToast : 버튼 없이 잠깐 떴다 사라지는 알림. 전투 중처럼 손을 멈추면 안 되는 상황에 쓴다
    /// </summary>
    public class UISystemMessage : MonoBehaviour
    {
        private static UISystemMessage instance;

        [Header("모달")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TextMeshProUGUI modalTitleText;
        [SerializeField] private TextMeshProUGUI modalMessageText;
        [SerializeField] private Button confirmButton;
        [Tooltip("제목이 없을 때 본문을 위로 끌어올릴 거리. 제목 자리가 비어 아래로 치우쳐 보이는 것을 막는다")]
        [SerializeField] private float messageOffsetWithoutTitle = 28f;

        [Header("토스트")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TextMeshProUGUI toastText;
        [SerializeField] private float toastHoldTime = 1.4f;
        [Tooltip("나타날 때는 짧게, 사라질 때는 조금 더 길게 투명도를 바꾼다")]
        [SerializeField] private float toastFadeInTime = 0.2f;
        [SerializeField] private float toastFadeTime = 0.45f;
        // 배경 없이 글자만 띄우므로, 종류별 색으로 한눈에 구분되게 한다
        [SerializeField] private Color toastNormalColor = new Color(0.96f, 0.94f, 0.86f);
        [SerializeField] private Color toastWarningColor = new Color(1f, 0.55f, 0.45f);

        private static readonly Color DefaultTitleColor = new Color(1f, 0.86f, 0.42f);

        private Coroutine toastRoutine;
        // 제목이 있을 때의 본문 위치. Awake에서 한 번만 기억해 두고 그 값을 기준으로 옮긴다
        private Vector2 messageBasePosition;

        // 창이 열려 있는지 (커서 표시와 카메라 회전 차단 판정에 쓰인다).
        // 토스트는 클릭할 것이 없으므로 포함하지 않는다
        public static bool IsOn => instance != null && instance.modalRoot != null && instance.modalRoot.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (modalMessageText != null)
            {
                messageBasePosition = modalMessageText.rectTransform.anchoredPosition;
            }

            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }

            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>확인 버튼이 달린 안내창. 제목 없이 본문만 보여준다</summary>
        public static void Show(string message)
        {
            Show(string.Empty, message, DefaultTitleColor);
        }

        /// <summary>제목과 본문을 가진 안내창 (강화 성공/실패처럼 결과를 강조할 때)</summary>
        public static void Show(string title, string message, Color titleColor)
        {
            if (instance == null || instance.modalRoot == null)
            {
                return;
            }

            bool hasTitle = !string.IsNullOrEmpty(title);

            if (instance.modalTitleText != null)
            {
                instance.modalTitleText.gameObject.SetActive(hasTitle);
                instance.modalTitleText.text = title;
                instance.modalTitleText.color = titleColor;
            }

            if (instance.modalMessageText != null)
            {
                instance.modalMessageText.text = message;
                instance.modalMessageText.rectTransform.anchoredPosition = hasTitle
                    ? instance.messageBasePosition
                    : instance.messageBasePosition + Vector2.up * instance.messageOffsetWithoutTitle;
            }

            // 이미 떠 있으면 내용만 바꾼다. 안내가 밀려도 마지막 것만 보여주면 충분하다
            UISound.SetActive(instance.modalRoot, true);
        }

        /// <summary>버튼 없이 잠깐 떴다 사라지는 알림 (획득/장착 등 일반 안내)</summary>
        public static void ShowToast(string message)
        {
            ShowToast(message, instance != null ? instance.toastNormalColor : Color.white);
        }

        /// <summary>실패·부족처럼 주의가 필요한 알림. 색만 다르고 동작은 같다</summary>
        public static void ShowToastWarning(string message)
        {
            ShowToast(message, instance != null ? instance.toastWarningColor : Color.white);
        }

        public static void ShowToast(string message, Color color)
        {
            if (instance == null || instance.toastRoot == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (instance.toastText != null)
            {
                instance.toastText.text = message;
                // 아이템 이름 등에 들어 있는 리치텍스트 색은 그대로 살아 있고, 나머지 글자에만 이 색이 적용된다
                instance.toastText.color = color;
            }

            if (instance.toastRoutine != null)
            {
                instance.StopCoroutine(instance.toastRoutine);
            }

            instance.toastRoutine = instance.StartCoroutine(instance.ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            // 이미 떠 있는 토스트를 새 메시지가 덮을 때는 현재 투명도에서 이어서 올려, 깜빡이지 않게 한다
            float startAlpha = toastRoot.activeSelf && toastGroup != null ? toastGroup.alpha : 0f;
            toastRoot.SetActive(true);

            float fadeIn = 0f;
            while (fadeIn < toastFadeInTime)
            {
                fadeIn += Time.deltaTime;

                if (toastGroup != null)
                {
                    toastGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(fadeIn / toastFadeInTime));
                }

                yield return null;
            }

            if (toastGroup != null)
            {
                toastGroup.alpha = 1f;
            }

            yield return new WaitForSeconds(toastHoldTime);

            float elapsed = 0f;
            while (elapsed < toastFadeTime)
            {
                elapsed += Time.deltaTime;

                if (toastGroup != null)
                {
                    toastGroup.alpha = 1f - Mathf.Clamp01(elapsed / toastFadeTime);
                }

                yield return null;
            }

            toastRoot.SetActive(false);
            toastRoutine = null;
        }

        private void Update()
        {
            // 모달이 떠 있는 동안에는 카메라 회전이 막히므로, 마우스를 쓰지 못하는 상황에서도
            // 키보드로 닫을 수 있어야 한다 (창이 안 닫혀 조작이 잠기는 것을 막는 안전장치)
            if (modalRoot == null || !modalRoot.activeSelf || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.enterKey.wasPressedThisFrame
                || Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // ESC로 닫는 경우 ESC 컨트롤러가 같은 프레임에 환경설정을 또 열지 않게 알린다
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    UIWindowStack.ConsumeEscape();
                }

                OnClickConfirm();
            }
        }

        // 확인 버튼에서 호출
        public void OnClickConfirm()
        {
            if (modalRoot != null)
            {
                UISound.SetActive(modalRoot, false);
            }
        }
    }
}
