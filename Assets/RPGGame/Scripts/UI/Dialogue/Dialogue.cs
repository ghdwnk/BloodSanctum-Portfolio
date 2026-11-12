using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGGame
{
    [DefaultExecutionOrder(-1)]
    /// <summary>
    /// 다이얼로그 텍스트 화면을 보여주는 기능을 담당하는 스크립트.
    /// 대사는 페이지 단위로 넘기며, 스페이스바로 타이핑 연출을 건너뛰거나 다음 페이지로 넘어간다
    /// </summary>
    public class Dialogue : MonoBehaviour
    {
        // 대사를 여러 페이지로 나눌 때 사용하는 구분자 (CSV에서 쉼표를 쓸 수 없어 '|'를 쓴다)
        private const char PageSeparator = '|';

        [Tooltip("다이얼로그 창 게임 오브젝트")]
        [SerializeField] private GameObject dialogueWindow;
        [Tooltip("다이얼로그 메시지를 보여줄 때 사용할 텍스트 UI참조 변수")]
        [SerializeField] private TMPro.TextMeshProUGUI dialogueText;
        [Tooltip("\"스페이스바로 넘기기\" 안내 문구 (넘길 것이 있을 때만 표시)")]
        [SerializeField] private GameObject skipHint;
        [Tooltip("다이얼로그를 일정 시간만 보여줄 때 사용할 시간 값")]
        [SerializeField] private float dialogueTempShowTime = 5f;
        [Tooltip("텍스트 애니메이션 시간 간격")]
        [SerializeField] private float textAnimationInterval = 0.04f;
        [Tooltip("퀘스트 수락/거절 버튼을 담는 그룹 (평소에는 비활성화)")]
        [SerializeField] private GameObject choiceButtonGroup;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        // 스태틱 인스턴스
        private static Dialogue instance;

        // 현재 대사의 페이지 목록과 진행 위치
        private string[] pages;
        private int pageIndex;
        private bool isTyping;
        private Coroutine typingRoutine;
        // 마지막 페이지에서 수락/거절 버튼을 띄울지 여부와 그때 실행할 콜백
        private bool hasChoiceOnLastPage;
        private UnityAction pendingAccept;
        private UnityAction pendingDecline;
        // 마지막 페이지에서 플레이어가 직접 넘겨 창을 닫을 때 한 번 실행할 콜백 (버튼 없이 자동으로 수락하는 퀘스트용)
        private UnityAction pendingFinish;
        // 0보다 크면 마지막 페이지를 다 보여준 뒤 그 시간만큼 기다렸다가 창을 닫는다
        private float autoCloseDelay;

        // 안내 문구 텍스트 (skipHint 하위에서 한 번만 찾아 캐시한다)
        private TMPro.TextMeshProUGUI skipHintText;

        // 연출용: 문장부호에서 잠깐 멈추는 정도(기본 글자 간격의 배수)와 타자 소리 간격(글자 수)
        private const float CommaPause = 4f;
        private const float SentencePause = 9f;
        private const int BlipEveryChars = 2;
        private const float FadeInTime = 0.18f;
        private const float HintPulseSpeed = 4f;

        // 화자 이름표. 씬에 따로 배치하지 않고 처음 필요할 때 코드로 만들어 대화창 왼쪽 위에 붙인다
        private string speaker;
        // 페이지마다 [이름]으로 지정한 화자 (null이면 speaker를 쓴다). 퀘스트 대사에서 NPC와 플레이어가 번갈아 말할 때 쓴다
        private string[] pageSpeakers;
        private GameObject speakerPlate;
        private RectTransform speakerPlateRect;
        private Image speakerPlateImage;
        private TMPro.TextMeshProUGUI speakerText;

        // 화자 일러스트. 대화창 왼쪽 위에서 위로 솟아 나오는 반신상으로, 창 뒤에 그려 아래쪽이 창에 가려지게 한다
        private const float PortraitHeight = 340f;
        private const float PortraitLeft = 30f;
        private const float PortraitSink = 30f;
        private static DialoguePortraitData portraitData;
        private GameObject portraitObject;
        private RectTransform portraitRect;
        private Image portraitImage;
        private CanvasGroup portraitGroup;
        private Coroutine portraitRoutine;
        // 이름표가 일러스트 오른쪽에 붙도록 옮길 때 쓰는 기본 위치
        private const float PlateLeftNoPortrait = 24f;
        private static readonly Color DefaultNameColor = new Color(1f, 0.82f, 0.29f);
        // 창/선택지/안내 문구의 부드러운 등장을 위한 CanvasGroup (없으면 자동으로 붙인다)
        private CanvasGroup windowGroup;
        private CanvasGroup choiceGroup;
        private CanvasGroup hintGroup;
        private Coroutine windowFadeRoutine;
        private Coroutine choiceFadeRoutine;

        private bool IsLastPage => pages == null || pageIndex >= pages.Length - 1;

        // 대화창에 클릭할 것(수락/거절 버튼)이 떠 있는지.
        // 대화창 자체가 열려 있는지로 판단하면 "MP가 부족합니다" 같은 짧은 안내에도
        // 전투 도중 커서가 나타나 버리므로, 선택지가 있을 때만 true가 된다
        // 대화창이 떠 있는지. NPC가 대화 종료를 감지해 카메라를 되돌릴 때 쓴다
        public static bool IsOpen =>
            instance != null && instance.dialogueWindow != null && instance.dialogueWindow.activeSelf;

        // activeSelf -> activeInHierarchy.
        // 선택지 그룹은 대화창의 자식이라 대화창이 닫혀 있어도 자기 activeSelf는 true로 남을 수 있다.
        // 그 상태를 "선택지가 떠 있다"로 읽으면 UIWindowState.IsAnyWindowOpen이 계속 true가 되어
        // 게임 시작부터 카메라 좌우 회전이 막힌다. 실제로 화면에 보이는지로 판단해야 한다
        public static bool IsChoiceOn =>
            instance != null && instance.choiceButtonGroup != null && instance.choiceButtonGroup.activeInHierarchy;

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

        private void Update()
        {
            // 대화창이 닫혀 있거나 보여줄 대사가 없으면 입력을 처리하지 않는다
            if (instance != this || pages == null || !dialogueWindow.activeSelf)
            {
                return;
            }

            // 다음으로 넘길 수 있을 때 안내 문구가 숨 쉬듯 깜빡여 "더 눌러도 된다"는 신호가 된다
            if (hintGroup != null && skipHint.activeSelf)
            {
                hintGroup.alpha = isTyping ? 0.5f : 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * HintPulseSpeed);
            }

            if (!InputManager.IsDialogueSkip)
            {
                return;
            }

            // 같은 스페이스바가 대쉬로 이어지지 않게 입력을 소비한다
            InputManager.ConsumeDash();

            // 타이핑 중이면 먼저 전체 텍스트를 즉시 보여준다
            if (isTyping)
            {
                CompleteTyping();
                return;
            }

            // 이미 다 보여준 상태에서 남은 페이지가 있으면 다음 페이지로 넘어간다
            if (!IsLastPage)
            {
                ++pageIndex;
                ShowCurrentPage();
                return;
            }

            // 마지막 페이지에서 한 번 더 누르면 창을 닫는다.
            // 단 수락/거절 선택지가 떠 있을 때는 닫지 않는다 — 스페이스바로 넘겨 버리면
            // 퀘스트를 받을지 말지 정하지 않은 채 제안이 사라지기 때문
            if (choiceButtonGroup != null && choiceButtonGroup.activeSelf)
            {
                return;
            }

            // 콜백 안에서 새 대사가 시작될 수 있으므로 먼저 비우고 닫은 뒤에 실행한다
            UnityAction finish = pendingFinish;
            pendingFinish = null;
            CloseDialogue();
            finish?.Invoke();
        }

        // 대사 한 페이지 맨 앞의 [이름]을 화자로 읽는다. 없으면 화자는 null (= SetSpeaker로 정한 기본 화자).
        // 예) "[봉인자] 여긴 어디죠?" -> 화자 "봉인자", 본문 "여긴 어디죠?"
        // 강조 색 처리(DialogueHighlighter)는 [이름]을 떼어 낸 뒤 본문에만 적용해야 이름이 깨지지 않는다
        private static readonly Regex SpeakerTag = new Regex(@"^\s*\[([^\]]+)\]\s*(.*)$", RegexOptions.Singleline);

        private static string ParsePage(string raw, bool highlight, out string pageSpeaker)
        {
            pageSpeaker = null;
            // 대사 속 "(닉네임)"은 로비에서 정한 플레이어 닉네임으로 바꾼다 (닉네임은 기호를 못 쓰게 검사하므로 태그가 깨지지 않는다)
            string body = raw.Trim().Replace("(닉네임)", PlayerProfile.Nickname);

            Match match = SpeakerTag.Match(body);
            if (match.Success)
            {
                pageSpeaker = match.Groups[1].Value.Trim();
                body = match.Groups[2].Value;
            }

            body = BreakSentences(body);

            return highlight ? DialogueHighlighter.Apply(body) : body;
        }

        // 한 페이지에 문장이 둘 이상이면 문장이 끝날 때마다 줄을 바꿔 읽기 편하게 한다.
        // 말줄임표("여긴... 어디죠?")는 문장이 끝난 게 아니므로 줄을 바꾸지 않는다 (.이 두 개 이상 이어진 경우 제외)
        private static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?])(?<!\.\.)[ \t]+(?=\S)");

        private static string BreakSentences(string text)
        {
            return SentenceEnd.Replace(text, "\n");
        }

        // 문자열을 페이지 단위로 나누는 함수. 구분자가 없으면 한 페이지짜리 배열을 돌려준다.
        // 페이지마다 [이름]으로 지정한 화자는 speakers에 담긴다 (지정이 없으면 null)
        private static string[] SplitPages(string text, out string[] speakers)
        {
            if (string.IsNullOrEmpty(text))
            {
                speakers = new string[1];
                return new[] { string.Empty };
            }

            string[] split = text.Split(PageSeparator);
            speakers = new string[split.Length];

            for (int ix = 0; ix < split.Length; ++ix)
            {
                split[ix] = ParsePage(split[ix], true, out speakers[ix]);
            }

            return split;
        }

        // 다이얼로그를 열 때 사용하는 함수
        public static void ShowDialogue()
        {
            // 모든 코루틴 중지
            instance.StopAllCoroutines();
            instance.typingRoutine = null;
            instance.isTyping = false;
            // 다이얼로그 창 활성화
            instance.dialogueWindow.SetActive(true);
            // 새 대화가 시작되면 이전에 떠 있던 선택지는 항상 숨긴다
            HideChoice();
        }

        // 다이얼로그를 닫을 때 사용하는 함수
        public static void CloseDialogue()
        {
            // 다이얼로그 창 닫기.
            // 참조만 버리고 코루틴을 멈추지 않으면, 타이핑 중이던 코루틴이 계속 돌다가
            // 이미 null이 된 pages를 읽어 NullReferenceException이 난다
            if (instance.typingRoutine != null)
            {
                instance.StopCoroutine(instance.typingRoutine);
                instance.typingRoutine = null;
            }

            instance.dialogueWindow.SetActive(false);
            instance.speaker = null;
            if (instance.portraitObject != null)
            {
                instance.portraitObject.SetActive(false);
            }

            instance.pages = null;
            instance.pendingFinish = null;
            instance.isTyping = false;
            HideChoice();
            instance.UpdateSkipHint();
        }

        // 퀘스트 제안 텍스트와 함께 수락/거절 버튼을 보여주는 함수.
        // 대사가 여러 페이지면 마지막 페이지에서만 버튼이 나타난다
        public static void ShowDialogueChoice(string text, UnityAction onAccept, UnityAction onDecline)
        {
            string[] pages = SplitPages(text, out string[] speakers);
            instance.BeginPages(pages, speakers, onAccept, onDecline, true, 0f);
        }

        // 버튼 없이 대사를 보여 주고, 플레이어가 마지막 페이지를 넘겨 창이 닫힐 때 onFinished를 실행하는 함수.
        // 도중에 걸어 나가는 등으로 창이 닫히면 실행하지 않는다 (끝까지 듣지 않은 제안은 수락되지 않는다)
        public static void ShowDialogueThen(string text, UnityAction onFinished)
        {
            string[] pages = SplitPages(text, out string[] speakers);
            instance.BeginPages(pages, speakers, null, null, false, 0f);
            instance.pendingFinish = onFinished;
        }

        // 수락/거절 버튼 그룹을 숨기는 함수
        private static void HideChoice()
        {
            if (instance.choiceButtonGroup != null)
            {
                instance.choiceButtonGroup.SetActive(false);
            }
        }

        // 전달한 시간 이후에 다이얼로그 창을 닫는 함수
        public static void CloseDialogueAfterTime(float time)
        {
            // 일정 시간을 대기한 후 게임 오브젝트를 비활성화하는 코루틴 함수를 호출한다
            instance.StartCoroutine(instance.CloseDialogueWithDelay(time));
        }

        // 다이얼로그에 텍스트를 보여줄 때 사용하는 함수
        public static void ShowDialogueText(string text)
        {
            string[] pages = SplitPages(text, out string[] speakers);
            instance.BeginPages(pages, speakers, null, null, false, 0f);
        }

        // 일정 시간 동안만 텍스트를 보여줄 때 사용하는 함수.
        // 아이템 획득 알림 등 짧은 안내용이라 페이지를 나누지 않는다
        public static void ShowDialogueTextTemporarily(string text, float time = 0f)
        {
            float showTime = time == 0f ? instance.dialogueTempShowTime : time;
            // 짧은 안내용이라 페이지는 나누지 않지만 [이름] 화자 표기는 읽는다 (보스 전투 대사)
            string body = ParsePage(text, false, out string pageSpeaker);
            instance.BeginPages(new[] { body }, new[] { pageSpeaker }, null, null, false, showTime);
        }

        // 이어지는 대사의 화자 이름을 정한다. 대화창이 닫힐 때까지 유지되고(수락 후 진행 대사 등),
        // 호출하지 않으면 이름표를 숨긴다 (보스 대사처럼 본문에 화자를 적는 경우)
        public static void SetSpeaker(string speakerName)
        {
            if (instance != null)
            {
                instance.speaker = speakerName;
            }
        }

        // 새 대사를 처음부터 보여주기 시작하는 함수
        private void BeginPages(string[] newPages, string[] newSpeakers, UnityAction onAccept, UnityAction onDecline,
            bool showChoice, float closeDelay)
        {
            // 이미 열려 있는 창에 이어 붙는 대사(수락 → 진행 안내)는 다시 페이드하지 않는다
            bool wasOpen = dialogueWindow.activeSelf;

            ShowDialogue();

            if (!wasOpen)
            {
                FadeIn(ref windowGroup, dialogueWindow, ref windowFadeRoutine);
            }
            else if (windowGroup != null)
            {
                // ShowDialogue가 코루틴을 전부 멈추므로, 페이드 도중에 끊겼다면 투명하게 남지 않도록 되돌린다
                windowGroup.alpha = 1f;
            }

            if (portraitGroup != null)
            {
                portraitGroup.alpha = 1f;
            }

            pages = newPages;
            pageSpeakers = newSpeakers;
            pageIndex = 0;
            pendingAccept = onAccept;
            pendingDecline = onDecline;
            pendingFinish = null;
            hasChoiceOnLastPage = showChoice;
            autoCloseDelay = closeDelay;

            ShowCurrentPage();
        }

        // 현재 페이지를 타이핑 연출로 보여주는 함수
        private void ShowCurrentPage()
        {
            if (typingRoutine != null)
            {
                StopCoroutine(typingRoutine);
            }

            HideChoice();
            ApplySpeaker();
            typingRoutine = StartCoroutine(TypeCurrentPage());
        }

        // 다이얼로그 텍스트를 설정할 때 텍스트에 애니메이션을 적용해 보여주는 함수
        private IEnumerator TypeCurrentPage()
        {
            string text = pages[pageIndex];

            isTyping = true;
            UpdateSkipHint();

            // 색 태그(<color>)가 들어 있어도 잘리지 않도록, 전체 문자열을 한 번에 넣고
            // 화면에 보이는 글자 수(maxVisibleCharacters)만 하나씩 늘린다.
            // 태그 문자는 글자 수에 세지 않는다
            dialogueText.text = text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();
            int total = dialogueText.textInfo.characterCount;

            // 코루틴 대기를 위한 객체 생성
            WaitForSeconds interval = new WaitForSeconds(textAnimationInterval);
            WaitForSeconds commaPause = new WaitForSeconds(textAnimationInterval * CommaPause);
            WaitForSeconds sentencePause = new WaitForSeconds(textAnimationInterval * SentencePause);

            for (int count = 1; count <= total; ++count)
            {
                yield return interval;
                dialogueText.maxVisibleCharacters = count;

                char shown = dialogueText.textInfo.characterInfo[count - 1].character;

                // 문장부호에서 잠깐 숨을 고르면 말하는 듯한 호흡이 생긴다.
                // 마지막 글자 뒤에는 쉴 이유가 없으므로 건너뛴다
                if (count < total)
                {
                    if (shown == '.' || shown == '!' || shown == '?' || shown == '…' || shown == '\n')
                    {
                        yield return sentencePause;
                    }
                    else if (shown == ',' || shown == '~')
                    {
                        yield return commaPause;
                    }
                }
            }

            typingRoutine = null;
            OnPageFullyShown();
        }

        // 타이핑 연출을 건너뛰고 현재 페이지를 즉시 전부 보여주는 함수
        private void CompleteTyping()
        {
            if (typingRoutine != null)
            {
                StopCoroutine(typingRoutine);
                typingRoutine = null;
            }

            OnPageFullyShown();
        }

        // 현재 페이지가 전부 표시된 시점에 실행되는 함수
        private void OnPageFullyShown()
        {
            isTyping = false;

            // 표시 도중 창이 닫혔으면 보여줄 페이지가 없다
            if (pages == null || pageIndex >= pages.Length)
            {
                return;
            }

            dialogueText.text = pages[pageIndex];
            dialogueText.maxVisibleCharacters = int.MaxValue;

            // 마지막 페이지에 도달했을 때만 수락/거절 버튼을 띄운다
            if (IsLastPage && hasChoiceOnLastPage)
            {
                ShowChoiceButtons();
            }

            // 임시 안내 메시지는 다 보여준 뒤 자동으로 닫는다
            if (IsLastPage && autoCloseDelay > 0f)
            {
                StartCoroutine(CloseDialogueWithDelay(autoCloseDelay));
            }

            UpdateSkipHint();
        }

        // 마지막 페이지에서 수락/거절 버튼을 활성화하고 콜백을 연결하는 함수
        private void ShowChoiceButtons()
        {
            if (choiceButtonGroup == null || acceptButton == null || declineButton == null)
            {
                return;
            }

            UnityAction onAccept = pendingAccept;
            UnityAction onDecline = pendingDecline;

            // 수락 버튼은 퀘스트 수락음이 따로 있으므로 공용 클릭음을 끈다 (거절 버튼은 클릭음이 그대로 난다)
            if (acceptButton.GetComponent<UIClickSoundSkip>() == null)
            {
                acceptButton.gameObject.AddComponent<UIClickSoundSkip>();
            }

            choiceButtonGroup.SetActive(true);
            FadeIn(ref choiceGroup, choiceButtonGroup, ref choiceFadeRoutine);
            acceptButton.onClick.RemoveAllListeners();
            declineButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() =>
            {
                HideChoice();
                onAccept?.Invoke();
            });
            declineButton.onClick.AddListener(() =>
            {
                HideChoice();
                onDecline?.Invoke();
            });
        }

        // 넘길 것이 남아 있을 때만 스킵 안내 문구를 보여주는 함수
        private void UpdateSkipHint()
        {
            if (skipHint == null)
            {
                return;
            }

            // 선택지가 떠 있으면 스페이스바로 아무것도 할 수 없으므로 안내를 숨긴다
            bool choiceWaiting = choiceButtonGroup != null && choiceButtonGroup.activeSelf;
            bool canPressSpace = pages != null && dialogueWindow.activeSelf && !choiceWaiting;
            skipHint.SetActive(canPressSpace);

            if (!canPressSpace)
            {
                return;
            }

            // 마지막 페이지에서는 넘길 것이 아니라 창이 닫히므로 안내 문구를 바꾼다
            if (skipHintText == null)
            {
                skipHintText = skipHint.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            }

            if (hintGroup == null)
            {
                hintGroup = GetOrAddGroup(skipHint);
            }

            if (skipHintText != null)
            {
                skipHintText.text = isTyping || !IsLastPage ? "Space - 넘기기" : "Space - 닫기";
            }
        }

        private static CanvasGroup GetOrAddGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        // 대상 UI를 투명에서 서서히 나타나게 한다. 이전에 돌던 페이드가 있으면 끊고 새로 시작한다
        private void FadeIn(ref CanvasGroup group, GameObject target, ref Coroutine routine)
        {
            if (group == null)
            {
                group = GetOrAddGroup(target);
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            group.alpha = 0f;
            routine = StartCoroutine(FadeRoutine(group));
        }

        private IEnumerator FadeRoutine(CanvasGroup group)
        {
            float time = 0f;

            while (time < FadeInTime)
            {
                // 대화 중에도 timeScale이 0이 될 수 있어 실시간으로 흘린다
                time += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(time / FadeInTime);
                yield return null;
            }

            group.alpha = 1f;
        }

        // 지금 페이지의 화자(페이지 앞 [이름] > SetSpeaker)를 이름표와 일러스트에 반영한다
        private void ApplySpeaker()
        {
            string who = pageSpeakers != null && pageIndex < pageSpeakers.Length && pageSpeakers[pageIndex] != null
                ? pageSpeakers[pageIndex]
                : speaker;

            if (portraitData == null)
            {
                portraitData = Resources.Load<DialoguePortraitData>("Data/Dialogue Portraits");
            }

            DialoguePortraitData.Entry entry = portraitData != null ? portraitData.Find(who) : null;

            float plateLeft = UpdatePortrait(entry);
            UpdateSpeakerPlate(who, entry, plateLeft);
        }

        // 일러스트를 갈아 끼운다. 이름표를 놓을 왼쪽 위치(일러스트가 없으면 기본 위치)를 돌려준다
        private float UpdatePortrait(DialoguePortraitData.Entry entry)
        {
            bool hasPortrait = entry != null && entry.portrait != null;

            if (!hasPortrait)
            {
                if (portraitObject != null)
                {
                    portraitObject.SetActive(false);
                }

                return PlateLeftNoPortrait;
            }

            if (portraitObject == null)
            {
                BuildPortrait();
            }

            // 화자가 바뀌었거나 이제 막 나타나는 경우에만 등장 연출을 한다
            bool animate = !portraitObject.activeSelf || portraitImage.sprite != entry.portrait;

            Sprite sprite = entry.portrait;
            float aspect = sprite.rect.width / sprite.rect.height;
            portraitImage.sprite = sprite;
            portraitRect.sizeDelta = new Vector2(PortraitHeight * aspect, PortraitHeight);
            portraitObject.SetActive(true);

            if (animate)
            {
                if (portraitRoutine != null)
                {
                    StopCoroutine(portraitRoutine);
                }

                portraitRoutine = StartCoroutine(PortraitInRoutine());
            }

            return PortraitLeft + portraitRect.sizeDelta.x + 14f;
        }

        // 일러스트가 옆에서 살짝 밀려 들어오며 나타난다
        private IEnumerator PortraitInRoutine()
        {
            float time = 0f;

            while (time < FadeInTime)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / FadeInTime);
                portraitGroup.alpha = t;
                portraitRect.anchoredPosition = new Vector2(PortraitLeft - 24f * (1f - t), -PortraitSink);
                yield return null;
            }

            portraitGroup.alpha = 1f;
            portraitRect.anchoredPosition = new Vector2(PortraitLeft, -PortraitSink);
        }

        // 일러스트 UI를 코드로 만든다. 대화창(window)의 형제로 두고 맨 뒤로 보내, 일러스트 아래쪽이 창 틀에 가려지게 한다
        private void BuildPortrait()
        {
            Transform root = dialogueWindow.transform.parent;

            portraitObject = new GameObject("Speaker Portrait", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            portraitObject.transform.SetParent(root, false);
            portraitObject.transform.SetAsFirstSibling();

            portraitRect = portraitObject.GetComponent<RectTransform>();
            // 대화창 루트의 왼쪽 위 모서리 기준. 아래 가운데가 아니라 아래 왼쪽을 피벗으로 둬 왼쪽 위에 얹는다
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 1f);
            portraitRect.pivot = new Vector2(0f, 0f);
            portraitRect.anchoredPosition = new Vector2(PortraitLeft, -PortraitSink);

            portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            portraitGroup = portraitObject.GetComponent<CanvasGroup>();
            portraitGroup.interactable = false;
            portraitGroup.blocksRaycasts = false;
        }

        // 화자 이름이 있으면 대화창 왼쪽 위 이름표에 보여주고, 없으면 숨긴다.
        // 일러스트가 있으면 그 오른쪽(plateLeft)에 놓는다
        private void UpdateSpeakerPlate(string who, DialoguePortraitData.Entry entry, float plateLeft)
        {
            if (string.IsNullOrEmpty(who))
            {
                if (speakerPlate != null)
                {
                    speakerPlate.SetActive(false);
                }

                return;
            }

            if (speakerPlate == null)
            {
                BuildSpeakerPlate();
            }

            // 플레이어 화자([봉인자])는 로비에서 정한 닉네임으로 보여준다. 일러스트와 색은 원래 이름으로 찾는다
            speakerText.text = PlayerProfile.ToDisplayName(who);
            speakerText.color = entry != null ? entry.nameColor : DefaultNameColor;
            speakerPlateRect.anchoredPosition = new Vector2(plateLeft, -6f);
            speakerPlate.SetActive(true);
            // 이름이 길든 짧든 판 크기가 글자에 맞게 다시 계산되도록 한다
            LayoutRebuilder.ForceRebuildLayoutImmediate(speakerPlateRect);
        }

        // 이름표 UI를 코드로 만든다: 어두운 판 + 이름 글자. 글꼴은 본문 텍스트의 것을 그대로 쓴다
        private void BuildSpeakerPlate()
        {
            speakerPlate = new GameObject("Speaker Plate", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            speakerPlate.transform.SetParent(dialogueWindow.transform, false);

            speakerPlateRect = speakerPlate.GetComponent<RectTransform>();
            // 대화창 왼쪽 위 모서리에 걸치도록 앵커와 피벗을 아래-왼쪽으로 둔다
            speakerPlateRect.anchorMin = speakerPlateRect.anchorMax = new Vector2(0f, 1f);
            speakerPlateRect.pivot = new Vector2(0f, 0f);

            speakerPlateImage = speakerPlate.GetComponent<Image>();
            speakerPlateImage.color = new Color(0.07f, 0.06f, 0.1f, 0.92f);
            speakerPlateImage.raycastTarget = false;

            HorizontalLayoutGroup layout = speakerPlate.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 8, 8);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = speakerPlate.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject textObject = new GameObject("Speaker Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObject.transform.SetParent(speakerPlate.transform, false);

            speakerText = textObject.GetComponent<TMPro.TextMeshProUGUI>();
            speakerText.font = dialogueText.font;
            speakerText.fontSize = Mathf.Max(24f, dialogueText.fontSize * 0.85f);
            speakerText.fontStyle = TMPro.FontStyles.Bold;
            speakerText.alignment = TMPro.TextAlignmentOptions.Left;
            speakerText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            speakerText.raycastTarget = false;
        }

        // 지연 시간을 두고 창을 닫을 때 내부에서 사용하는 코루틴 함수
        private IEnumerator CloseDialogueWithDelay(float delay)
        {
            // 함수에 전달받은 시간만큼 대기
            yield return new WaitForSeconds(delay);
            
            CloseDialogue();
        }
    }
}
