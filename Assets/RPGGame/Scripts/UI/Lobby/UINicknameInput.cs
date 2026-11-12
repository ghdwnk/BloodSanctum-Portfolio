using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 새로하기를 누르면 게임 씬에 들어가기 전에 닉네임을 입력받는 창.
    /// 모양은 수량 입력창과 같은 박스·버튼 스프라이트를 쓰는 프리팹(Resources/UI/Nickname Input)이고,
    /// 로비 씬에는 따로 배치하지 않고 Create가 캔버스 아래에 만들어 준다.
    /// 프리팹은 메뉴 RPG/UI/Build Nickname Input으로 다시 만들 수 있다
    /// </summary>
    public class UINicknameInput : MonoBehaviour
    {
        private const string PrefabPath = "UI/Nickname Input";
        private const float FadeTime = 0.2f;
        // 상점 수량 입력창과 같은 높이. 옵션창(1000)보다는 아래다
        private const int SortingOrder = 400;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action<string> onConfirm;
        private Action onCancel;
        private Coroutine fadeRoutine;

        /// <summary>
        /// 캔버스 아래에 창을 만들고 숨겨 둔다. 프리팹이 없으면 null
        /// </summary>
        public static UINicknameInput Create(Canvas canvas)
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null || canvas == null)
            {
                Debug.LogWarning("[UINicknameInput] 닉네임 입력창 프리팹을 찾지 못했습니다: Resources/" + PrefabPath);
                return null;
            }

            GameObject instance = Instantiate(prefab, canvas.rootCanvas.transform);
            instance.name = "Nickname Input";

            // 로비 배경·안개·로고는 각자 정렬 순서를 덮어쓴 캔버스라 그보다 위에 그려야 한다.
            // 프리팹에서는 최상위 캔버스라 이 설정이 저장되지 않으므로 캔버스 아래에 놓은 뒤에 켠다
            Canvas nested = instance.GetComponent<Canvas>();
            nested.overrideSorting = true;
            nested.sortingOrder = SortingOrder;

            instance.SetActive(false);

            return instance.GetComponent<UINicknameInput>();
        }

        private void Awake()
        {
            UISound.AttachClick(transform);
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
            inputField.onSubmit.AddListener(_ => Confirm());
            // 다시 쓰기 시작하면 이전 오류 문구는 지운다
            inputField.onValueChanged.AddListener(_ => messageText.text = string.Empty);
            inputField.characterLimit = PlayerProfile.MaxLength;
        }

        /// <summary>
        /// 창을 연다. 확인하면 onConfirm에 검사를 통과한 닉네임을, 취소하면 onCancel을 호출한다
        /// </summary>
        public void Show(string initial, Action<string> confirmed, Action cancelled)
        {
            onConfirm = confirmed;
            onCancel = cancelled;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            messageText.text = string.Empty;
            inputField.text = initial ?? string.Empty;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            group.alpha = 0f;

            // 창이 막 켜진 프레임에 입력칸을 활성화하면 포커스가 풀리는 경우가 있어 한 프레임 쉰다
            yield return null;
            inputField.ActivateInputField();

            float elapsed = 0f;
            while (elapsed < FadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(elapsed / FadeTime);
                yield return null;
            }

            group.alpha = 1f;
            fadeRoutine = null;
        }

        private void Confirm()
        {
            string nickname = inputField.text.Trim();

            if (!PlayerProfile.Validate(nickname, out string reason))
            {
                messageText.text = reason;
                inputField.ActivateInputField();
                return;
            }

            gameObject.SetActive(false);
            onConfirm?.Invoke(nickname);
        }

        private void Cancel()
        {
            gameObject.SetActive(false);
            onCancel?.Invoke();
        }
    }
}
