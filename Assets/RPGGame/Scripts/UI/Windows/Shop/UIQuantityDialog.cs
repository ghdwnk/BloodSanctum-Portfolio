using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// "몇 개 구매하시겠습니까?" 같은 수량 입력 작은 창. 상점의 구매·판매가 함께 쓴다.
    /// 숫자를 직접 치거나 -/+/최대 버튼으로 정하고, Enter 또는 확인으로 진행한다 (Esc·취소로 닫는다).
    /// 창이 열려 있는 동안에는 숫자 키가 퀵슬롯으로 새지 않도록 플레이어 입력을 막는다 (InputManager가 IsOn을 본다)
    /// </summary>
    public class UIQuantityDialog : MonoBehaviour
    {
        private static UIQuantityDialog instance;

        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI itemText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI totalText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button maxButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        // 합계가 가진 돈을 넘거나 문제가 있을 때 붉게, 평소에는 금색으로 보여 준다
        private static readonly Color NormalTotalColor = new Color(1f, 0.86f, 0.42f, 1f);
        private static readonly Color WarningTotalColor = new Color(1f, 0.35f, 0.3f, 1f);

        private int maxCount;
        private int unitPrice;
        private int currentCount;
        private string totalLabel;
        private Action<int> onConfirm;
        // 값을 코드로 바꾸는 동안에는 입력 이벤트가 되돌아와 다시 처리하지 않게 한다
        private bool isUpdatingField;

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

            minusButton.onClick.AddListener(() => SetCount(currentCount - 1));
            plusButton.onClick.AddListener(() => SetCount(currentCount + 1));
            maxButton.onClick.AddListener(() => SetCount(maxCount));
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Close);

            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.onValueChanged.AddListener(OnInputChanged);
            inputField.onEndEdit.AddListener(OnEndEdit);

            modalRoot.SetActive(false);
        }

        /// <summary>
        /// 수량 창을 연다. max는 고를 수 있는 최대 개수, unitPrice는 한 개 값(합계 표시용, 0이면 합계 줄을 숨긴다).
        /// totalLabel은 합계 앞에 붙는 말 ("합계", "받을 금액" 등)이고, 확인을 누르면 고른 개수가 onConfirm으로 전달된다
        /// </summary>
        public static void Show(string title, string itemName, int max, int unitPrice, string totalLabel, Action<int> onConfirm)
        {
            if (instance == null || max <= 0)
            {
                return;
            }

            instance.Open(title, itemName, max, unitPrice, totalLabel, onConfirm);
        }

        public static void Close()
        {
            if (instance != null && instance.modalRoot != null)
            {
                UISound.SetActive(instance.modalRoot, false);
                instance.onConfirm = null;
            }
        }

        private void Open(string title, string itemName, int max, int price, string label, Action<int> confirmAction)
        {
            maxCount = Mathf.Max(1, max);
            unitPrice = Mathf.Max(0, price);
            totalLabel = label;
            onConfirm = confirmAction;

            titleText.text = title;
            itemText.text = itemName;
            totalText.gameObject.SetActive(unitPrice > 0);

            UISound.SetActive(modalRoot, true);
            // 항상 맨 위에 보이게 한다 (상점 창 위에 떠야 한다)
            modalRoot.transform.SetAsLastSibling();

            SetCount(1);
            FocusInput();
            // 창이 막 켜진 프레임에는 입력창 활성화가 무시되는 경우가 있어, 한 프레임 뒤에 한 번 더 잡는다
            StartCoroutine(FocusNextFrame());
        }

        private IEnumerator FocusNextFrame()
        {
            yield return null;

            if (IsOn)
            {
                FocusInput();
            }
        }

        // 열자마자 바로 숫자를 칠 수 있도록 입력창을 선택하고 내용을 전체 선택한다
        private void FocusInput()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            }

            inputField.ActivateInputField();
            inputField.selectionAnchorPosition = 0;
            inputField.selectionFocusPosition = inputField.text.Length;
        }

        private void Update()
        {
            if (!IsOn || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                Confirm();
            }
            else if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // 이 ESC는 수량 창을 닫는 데 썼으므로 ESC 컨트롤러가 같은 프레임에 또 처리하지 않게 알린다
                UIWindowStack.ConsumeEscape();
                Close();
            }
        }

        // 개수를 1~max 안으로 맞추고 입력창·합계·버튼 상태를 갱신한다
        private void SetCount(int value)
        {
            currentCount = Mathf.Clamp(value, 1, maxCount);

            isUpdatingField = true;
            inputField.text = currentCount.ToString();
            isUpdatingField = false;

            Refresh();
        }

        private void Refresh()
        {
            minusButton.interactable = currentCount > 1;
            plusButton.interactable = currentCount < maxCount;
            maxButton.interactable = currentCount < maxCount;

            if (unitPrice > 0)
            {
                long total = (long)unitPrice * currentCount;
                totalText.text = $"{totalLabel} {total:N0} G";
                totalText.color = NormalTotalColor;
            }
        }

        // 숫자를 치는 동안에도 최대 개수를 넘지 않게 바로 잘라 준다. 빈 칸은 지우는 중일 수 있어 그대로 둔다
        private void OnInputChanged(string text)
        {
            if (isUpdatingField)
            {
                return;
            }

            if (!int.TryParse(text, out int value))
            {
                return;
            }

            if (value > maxCount)
            {
                SetCount(maxCount);
                FocusInput();
                return;
            }

            currentCount = Mathf.Max(1, value);
            Refresh();
        }

        // 입력을 마쳤을 때 빈 칸이나 0 같은 값을 1로 되돌린다
        private void OnEndEdit(string text)
        {
            if (!int.TryParse(text, out int value) || value < 1)
            {
                SetCount(1);
            }
            else
            {
                SetCount(value);
            }
        }

        private void Confirm()
        {
            // 입력 중인 글자가 아직 반영되지 않았을 수 있어 입력창의 현재 값을 다시 읽는다
            int count = currentCount;
            if (int.TryParse(inputField.text, out int typed))
            {
                count = Mathf.Clamp(typed, 1, maxCount);
            }

            Action<int> action = onConfirm;
            Close();
            action?.Invoke(count);
        }
    }
}
