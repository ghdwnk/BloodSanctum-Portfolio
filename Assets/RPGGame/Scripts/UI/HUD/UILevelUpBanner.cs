using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 레벨업 순간에 화면 중앙 위쪽으로 "LEVEL UP" 배너 이미지를 띄우고,
    /// 플레이어 발밑에 파티클을 함께 재생하는 연출.
    /// 배너 이미지가 없으면 예전처럼 "LEVEL UP" 텍스트만 띄운다.
    /// </summary>
    public class UILevelUpBanner : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup fadeGroup;
        [Tooltip("\"Level Up\" 로고 이미지. 비워 두면 titleText에 \"LEVEL UP\"을 적어 보여준다")]
        [SerializeField] private Image bannerImage;
        [Tooltip("이미지가 없을 때만 쓰는 대체 텍스트. 이미지를 쓸 때는 로고에 글자가 이미 있으므로 숨긴다")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("연출")]
        [Tooltip("제자리에서 투명도만 바뀐다: 서서히 나타나(fadeIn) -> 머무르고(hold) -> 서서히 사라진다(fadeOut)")]
        [SerializeField] private float fadeInTime = 0.6f;
        [SerializeField] private float holdTime = 1.8f;
        [SerializeField] private float fadeOutTime = 0.8f;

        [Header("파티클")]
        [SerializeField] private GameObject effectPrefab;
        [Tooltip("플레이어 기준 생성 위치 오프셋")]
        [SerializeField] private Vector3 effectOffset = Vector3.zero;
        [SerializeField] private float effectLifeTime = 3f;

        private Coroutine playRoutine;
        private Transform playerTransform;

        private void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Start()
        {
            // 레벨 컨트롤러는 플레이어에 붙어 있고 Awake에서 이벤트를 준비하므로 Start에서 구독한다
            PlayerLevelController levelController = FindAnyObjectByType<PlayerLevelController>();
            if (levelController != null)
            {
                levelController.SubscribeOnLevelUP(OnLevelUp);
                playerTransform = levelController.transform.root;
            }
        }

        private void OnLevelUp(int level)
        {
            if (titleText != null)
            {
                // 이미지가 있으면 로고만 보여준다. 이미지가 없을 때만 예전처럼 텍스트로 대신한다
                bool useText = bannerImage == null;
                titleText.gameObject.SetActive(useText);
                titleText.text = "LEVEL UP";
            }

            SpawnEffect();
            UISound.PlayLevelUp();

            // 연속으로 레벨업하면 배너가 겹치므로 이전 연출을 끊고 새로 시작한다
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            playRoutine = StartCoroutine(PlayRoutine());
        }

        // 플레이어 위치에 파티클을 생성한다.
        // 플레이어를 따라다닐 필요가 있어 자식으로 붙이고, 수명이 지나면 파괴한다
        private void SpawnEffect()
        {
            if (effectPrefab == null || playerTransform == null)
            {
                return;
            }

            GameObject effect = Instantiate(effectPrefab, playerTransform.position + effectOffset,
                Quaternion.identity, playerTransform);

            Destroy(effect, effectLifeTime);
        }

        // 처음과 끝이 느리고 가운데가 빠른 부드러운 곡선 (0~1 입력). 선형으로 바꾸면 시작과 끝이 딱딱해 보인다
        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private void SetAlpha(float alpha)
        {
            if (fadeGroup != null)
            {
                fadeGroup.alpha = alpha;
            }
        }

        private IEnumerator PlayRoutine()
        {
            // 다음 연출이 이전 연출의 흔적(위치/크기)을 물려받지 않도록 투명한 상태에서 시작한다
            SetAlpha(0f);
            panel.SetActive(true);

            // 페이드 인
            float elapsed = 0f;
            while (elapsed < fadeInTime)
            {
                elapsed += Time.deltaTime;
                SetAlpha(SmoothStep(Mathf.Clamp01(elapsed / fadeInTime)));
                yield return null;
            }

            SetAlpha(1f);

            // 머무르기
            yield return new WaitForSeconds(holdTime);

            // 페이드 아웃
            elapsed = 0f;
            while (elapsed < fadeOutTime)
            {
                elapsed += Time.deltaTime;
                SetAlpha(1f - SmoothStep(Mathf.Clamp01(elapsed / fadeOutTime)));
                yield return null;
            }

            SetAlpha(0f);
            panel.SetActive(false);
            playRoutine = null;
        }
    }
}
