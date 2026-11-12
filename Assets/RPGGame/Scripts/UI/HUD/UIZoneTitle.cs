using System.Collections;
using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 지역에 들어설 때 화면 위쪽에 지역 이름을 띄우는 연출.
    /// 게임을 시작하면 플레이어가 이미 시작 지역 안에 있으므로 첫 지역 이름이 자동으로 뜬다.
    /// </summary>
    public class UIZoneTitle : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("연출")]
        [SerializeField] private float fadeInTime = 0.6f;
        [SerializeField] private float holdTime = 2.2f;
        [SerializeField] private float fadeOutTime = 0.9f;
        [Tooltip("나타날 때 살짝 아래에서 떠오르는 거리")]
        [SerializeField] private float riseDistance = 24f;

        [Header("표기")]
        [Tooltip("사냥터/던전 지역에서는 이름 아래에 한 줄 덧붙인다")]
        [SerializeField] private string dungeonSubtitle = "위험 지역";
        [SerializeField] private TextMeshProUGUI subtitleText;

        private RectTransform panelTransform;
        private Vector2 basePosition;
        private Coroutine playRoutine;
        // 이동 연출이 끝나길 기다리는 중인 코루틴
        private Coroutine waitRoutine;

        private void Awake()
        {
            if (panel != null)
            {
                panelTransform = panel.GetComponent<RectTransform>();
                basePosition = panelTransform.anchoredPosition;
                panel.SetActive(false);
            }
        }

        private void Start()
        {
            // 지역 판정은 ZoneTracker가 담당하고, 이 스크립트는 바뀔 때 배너만 띄운다.
            // 안전지대 판정도 같은 결과를 쓰기 때문에 추적을 UI 밖으로 뺐다
            ZoneTracker tracker = FindAnyObjectByType<ZoneTracker>();
            if (tracker != null)
            {
                tracker.SubscribeOnZoneChanged(OnZoneChanged);
            }
        }

        private void OnZoneChanged(ZoneArea zone)
        {
            if (zone == null)
            {
                return;
            }

            // 게이트·부활로 이동하면 로딩 화면(약 2.8초)이 먼저 화면을 덮는데, 구역은 그 사이(이동 직후)에 바뀐다.
            // 바로 띄우면 배너 3.7초 중 대부분이 로딩 화면 뒤에서 지나가 버려서, 로딩이 끝난 뒤에 띄운다
            if (waitRoutine != null)
            {
                StopCoroutine(waitRoutine);
                waitRoutine = null;
            }

            if (UITeleportScreen.IsOn)
            {
                waitRoutine = StartCoroutine(PlayAfterTeleport(zone));
            }
            else
            {
                Play(zone);
            }
        }

        // 이동 연출이 끝나길 기다렸다가, 아직 그 지역에 있으면 배너를 띄운다 (그 사이 다른 지역으로 옮겨졌으면 건너뛴다)
        private IEnumerator PlayAfterTeleport(ZoneArea zone)
        {
            while (UITeleportScreen.IsOn)
            {
                yield return null;
            }

            waitRoutine = null;

            if (ZoneTracker.CurrentZone == zone)
            {
                Play(zone);
            }
        }

        // 코드에서 직접 띄우고 싶을 때 쓸 수 있게 공개해 둔다 (컷신 등)
        public void Play(ZoneArea zone)
        {
            if (panel == null || zone == null || string.IsNullOrEmpty(zone.ZoneName))
            {
                return;
            }

            if (titleText != null)
            {
                titleText.text = zone.ZoneName;
            }

            if (subtitleText != null)
            {
                // 사냥터에 들어설 때만 경고 문구를 붙인다
                subtitleText.gameObject.SetActive(zone.IsDungeon);
                subtitleText.text = dungeonSubtitle;
            }

            // 지역을 빠르게 오갈 때 연출이 겹치지 않도록 이전 것을 끊고 다시 시작한다
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            playRoutine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            panel.SetActive(true);

            yield return Fade(0f, 1f, fadeInTime, true);

            yield return new WaitForSeconds(holdTime);

            yield return Fade(1f, 0f, fadeOutTime, false);

            panel.SetActive(false);
            playRoutine = null;
        }

        // 알파와 위치를 함께 보간한다. rise가 true면 아래에서 떠오르고, false면 제자리에서 사라진다
        private IEnumerator Fade(float from, float to, float duration, bool rise)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (fadeGroup != null)
                {
                    fadeGroup.alpha = Mathf.Lerp(from, to, t);
                }

                if (panelTransform != null && rise)
                {
                    panelTransform.anchoredPosition = basePosition + Vector2.down * Mathf.Lerp(riseDistance, 0f, t);
                }

                yield return null;
            }

            if (fadeGroup != null)
            {
                fadeGroup.alpha = to;
            }

            if (panelTransform != null && rise)
            {
                panelTransform.anchoredPosition = basePosition;
            }
        }
    }
}
