using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// 새 게임을 시작할 때 보여주는 배경 이야기 화면.
    /// 검은 바탕에 흰 글씨가 한 문단씩 서서히 나타나고, 스페이스바/클릭으로 다음 문단으로 넘어간다.
    /// 마지막 문단 뒤에는 onFinished가 호출되어 로딩 화면으로 이어진다.
    /// 로비 씬에는 InputManager가 없으므로 입력은 Input System 장치를 직접 읽는다
    /// </summary>
    public class UIIntroScreen : MonoBehaviour
    {
        // 문단 구분자 (대사와 같은 '|')
        private const char PageSeparator = '|';

        [SerializeField] private TMPro.TextMeshProUGUI storyText;
        [Tooltip("\"Space - 계속\" 안내 문구 (문단이 다 나타난 뒤에만 보인다)")]
        [SerializeField] private TMPro.TextMeshProUGUI hintText;
        [SerializeField] private CanvasGroup textGroup;
        [SerializeField] private float fadeTime = 1.2f;
        [Tooltip("문단이 나타난 뒤 입력을 받기 시작하기까지의 최소 시간 (연타로 문단을 못 읽고 넘기는 것을 막는다)")]
        [SerializeField] private float minReadTime = 0.8f;
        [SerializeField, TextArea(3, 10)] private string story =
            "오래전, 대륙 바엘스라는 마계의 군주 자라톤의 군세에 짓밟혔다.|" +
            "봉인자 아엘브리스는 자신의 피를 대가로 자라톤을 블러드생텀 깊은 곳에 가두었다.|" +
            "그러나 봉인은 세월과 함께 금이 가기 시작했고,\n하늘에서는 붉은 비가 내리기 시작했다.|" +
            "바엘스라의 마지막 마을 애쉬헤이븐에는\n마계의 마물들이 몰려들고 있다.|" +
            "당신은 붉은 비 속에서 쓰러진 채 눈을 뜬다.\n당신의 몸에는 아엘브리스의 피가 흐르고 있다.|" +
            "이제, 마지막 봉인자의 이야기가 시작된다.";

        private UnityAction onFinished;

        // 이야기를 처음부터 보여주고 끝나면 onFinished를 호출한다
        public void Play(UnityAction finished)
        {
            onFinished = finished;
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(PlayRoutine());
        }

        private static bool ContinuePressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            return (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        private IEnumerator PlayRoutine()
        {
            string[] pages = story.Split(PageSeparator);

            storyText.text = string.Empty;
            textGroup.alpha = 0f;
            SetHint(string.Empty, 0f);

            // 이 화면을 연 클릭이 곧바로 첫 문단을 넘기지 않도록 한 프레임 쉰다
            yield return null;

            for (int ix = 0; ix < pages.Length; ++ix)
            {
                bool isLast = ix == pages.Length - 1;

                storyText.text = pages[ix].Trim();
                yield return Fade(0f, 1f);

                // 다 나타난 뒤 잠깐은 입력을 받지 않고, 그 뒤에 안내 문구를 띄운다
                yield return new WaitForSecondsRealtime(minReadTime);
                SetHint(isLast ? "Space - 시작" : "Space - 계속", 1f);

                while (!ContinuePressed())
                {
                    yield return null;
                }

                SetHint(string.Empty, 0f);
                yield return Fade(1f, 0f);
            }

            gameObject.SetActive(false);
            onFinished?.Invoke();
        }

        private IEnumerator Fade(float from, float to)
        {
            float time = 0f;

            while (time < fadeTime)
            {
                time += Time.unscaledDeltaTime;
                textGroup.alpha = Mathf.Lerp(from, to, time / fadeTime);
                yield return null;
            }

            textGroup.alpha = to;
        }

        private void SetHint(string text, float alpha)
        {
            if (hintText == null)
            {
                return;
            }

            hintText.text = text;
            hintText.alpha = alpha;
        }
    }
}
