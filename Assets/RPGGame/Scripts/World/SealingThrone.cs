using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGGame
{
    /// <summary>
    /// 보스 아레나의 붉은 수정 왕좌. 최종 보스를 쓰러뜨리기(최종 퀘스트 목표 달성) 전에는 꺼져 있다가,
    /// 쓰러뜨리면 붉게 빛난다. 수정 앞으로 가면 세리나가 안내하고, 인벤토리의 블러드스톤을 끌어다
    /// 수정 앞 바닥에 떨어뜨리면 봉인이 시작된다 (블러드스톤은 최종 퀘스트 수락 때 받는다. F키는 쓰지 않는다).
    /// 봉인이 시작되면 모든 UI가 사라지고 컷씬이 시작된다: 카메라가 붉은 보석으로 다가가고, 보석이 점차 작아지며 봉인된 뒤
    /// 섬광과 함께 어두워지면서 퀘스트를 완료 처리하고 결과 화면(UIEndingScreen)으로 이어진다
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SealingThrone : MonoBehaviour
    {
        [Header("조건")]
        [Tooltip("이 퀘스트가 '완료 보고 대기' 상태가 되면(최종 보스 처치) 왕좌가 깨어난다")]
        [SerializeField] private int questID = 7;

        [Header("외형")]
        [SerializeField] private Renderer[] glowRenderers;
        [SerializeField] private Light glowLight;
        // 꺼진 상태의 색 (어두운 잿빛)과 켜졌을 때 발광 색
        [SerializeField] private Color dormantTint = new Color(0.28f, 0.25f, 0.25f, 1f);
        [SerializeField, ColorUsage(false, true)] private Color activeEmission = new Color(2.6f, 0.35f, 0.3f, 1f);
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float lightIntensity = 6f;

        [Header("각성 연출")]
        [Tooltip("왕좌가 깨어나 있는 동안(봉인을 완성하기 전까지) 왕좌 위에 띄워 둘 이펙트 (붉은 수정). 비우면 없음")]
        [SerializeField] private GameObject activationEffectPrefab;
        [SerializeField] private Vector3 activationEffectOffset = new Vector3(0f, 2.5f, 0f);

        [Header("봉인 연출")]
        [Tooltip("컷씬 끝에서 화면을 덮는 전체 화면 캔버스. 하얀 섬광에 이어 검게 변하는 데도 쓴다")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private CanvasGroup overlayGroup;
        [Tooltip("섬광 이후 화면이 완전히 어두워지기까지의 시간")]
        [SerializeField] private float fadeToBlackTime = 1.2f;

        [Header("엔딩 음악")]
        [Tooltip("봉인을 완성하려고 왕좌와 상호작용하는 순간부터 결과 화면이 끝날 때까지 흐르는 음악 (06_Silence)")]
        [SerializeField] private AudioClip endingMusic;
        [SerializeField, Range(0f, 1f)] private float endingMusicVolume = 0.45f;

        [Header("엔딩 스토리 (검은 화면 위 글자)")]
        [Tooltip("섬광 뒤 화면이 완전히 검어지면 결과 화면이 뜨기 전에 한 쪽씩 보여 준다. 비우면 이 단계를 건너뛴다. 글자는 봉인 오버레이 안의 TextMeshPro를 쓴다 (storyText를 비우면 오버레이 아래에서 찾는다)")]
        [SerializeField] private TextMeshProUGUI storyText;
        [SerializeField, TextArea(2, 4)] private string[] storyPages =
        {
            "자라톤은 블러드 생텀의 깊은 어둠 속에 다시 봉인되었다.",
            "그러나 영원히 지속되는 봉인은 없는 법.",
            "억겁의 세월이 흘러 봉인이 풀리는 날, 우리는 또다시 그와 맞설 것이다."
        };
        [Tooltip("한 쪽이 서서히 나타나는 시간 / 머무는 시간 / 서서히 사라지는 시간 / 다음 쪽까지의 간격")]
        [SerializeField] private float storyFadeInTime = 0.9f;
        [SerializeField] private float storyHoldTime = 2.6f;
        [SerializeField] private float storyFadeOutTime = 0.7f;
        [SerializeField] private float storyGapTime = 0.4f;

        [Header("컷씬 카메라")]
        [Tooltip("카메라가 현재 위치에서 붉은 보석 앞까지 다가가는 시간 / 보석이 작아지며 봉인되는 시간")]
        [SerializeField] private float zoomTime = 3.2f;
        [SerializeField] private float sealTime = 3.8f;
        [Tooltip("보석까지의 거리. 줌인이 끝났을 때(cameraDistance)에서 봉인이 끝날 때(cameraDistanceEnd)까지 천천히 더 다가간다")]
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private float cameraDistanceEnd = 3.6f;
        [Tooltip("보석 높이를 기준으로 한 카메라 높이 오프셋과, 줌인했을 때의 시야각 (좁을수록 보석이 크게 보인다)")]
        [SerializeField] private float cameraHeight = 0.3f;
        [SerializeField] private float zoomFieldOfView = 34f;
        [Tooltip("봉인이 끝나 갈수록 커지는 카메라 떨림의 최대 크기")]
        [SerializeField] private float shakeMax = 0.05f;
        [Tooltip("하얀 섬광이 차오르는 시간")]
        [SerializeField] private float flashTime = 0.25f;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        private Material[] materials;
        private bool isActive;
        private bool isUsed;
        private GameObject activationEffect;
        private bool playerInRange;
        private Transform playerTransform;

        // 컷씬 중에만 쓰는 상태. 연출이 끝나면 RestoreFromCutscene이 원래대로 되돌린다
        private static SealingThrone current;
        private bool cutsceneActive;
        private Camera cutsceneCamera;
        private CameraRig cameraRig;
        private Quaternion savedCameraLocalRotation;
        private float savedFieldOfView;
        private Damageable playerDamageable;

        // 수정 앞에 왔을 때 세리나가 하는 안내 대사 ([이름]은 화자 표기)
        private const string GuideDialogue = "[세리나] 블러드 스톤을 제단에 바쳐 자라톤을 봉인하세요!";
        private const float GuideDialogueTime = 6f;

        // 장면에 하나뿐인 왕좌. 인벤토리에서 아이템을 바닥에 떨어뜨렸을 때 여기로 알린다
        private static SealingThrone instance;

        // 이번에 수정 앞에 들어온 뒤 안내 대사를 이미 띄웠는지 (범위를 나갔다 다시 들어오면 다시 띄운다)
        private bool guideShown;

        private void Awake()
        {
            instance = this;
            GetComponent<Collider>().isTrigger = true;

            // 렌더러의 머티리얼을 인스턴스로 가져와 색을 바꾼다 (에셋 원본은 건드리지 않는다)
            var list = new System.Collections.Generic.List<Material>();
            foreach (Renderer glowRenderer in glowRenderers)
            {
                if (glowRenderer != null)
                {
                    list.AddRange(glowRenderer.materials);
                }
            }

            materials = list.ToArray();

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            ApplyDormant();
        }

        private void Update()
        {
            if (isUsed)
            {
                return;
            }

            bool shouldBeActive = QuestManager.Instance != null &&
                                  QuestManager.Instance.GetState(questID) == QuestManager.QuestProgressState.ReadyToTurnIn;

            if (shouldBeActive != isActive)
            {
                isActive = shouldBeActive;
                if (isActive)
                {
                    ApplyActiveBase();
                    ShowActivationEffect();
                }
                else
                {
                    ApplyDormant();
                    HideActivationEffect();
                }
            }

            if (!isActive)
            {
                return;
            }

            // 숨 쉬듯 일렁이는 붉은 빛
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);
            foreach (Material material in materials)
            {
                material.SetColor(EmissionColorID, activeEmission * pulse);
            }

            if (glowLight != null)
            {
                glowLight.enabled = true;
                glowLight.intensity = lightIntensity * pulse;
            }

            // 수정 앞에 오면 세리나가 안내한다. 다른 대사가 열려 있으면 끝날 때까지 기다렸다가 띄운다
            if (playerInRange && !guideShown && !Dialogue.IsOpen)
            {
                guideShown = true;
                Dialogue.ShowDialogueTextTemporarily(GuideDialogue, GuideDialogueTime);
            }
        }

        private void ApplyDormant()
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                material.SetColor(BaseColorID, dormantTint);
                material.SetColor(EmissionColorID, Color.black);
            }

            if (glowLight != null)
            {
                glowLight.enabled = false;
            }
        }

        // 왕좌가 깨어나 있는 동안 왕좌 위에 붉은 수정 이펙트를 띄운다. 왕좌의 자식으로 붙여 함께 움직인다
        private void ShowActivationEffect()
        {
            if (activationEffectPrefab == null || activationEffect != null)
            {
                return;
            }

            activationEffect = Instantiate(activationEffectPrefab, transform);
            activationEffect.transform.localPosition = activationEffectOffset;
        }

        private void HideActivationEffect()
        {
            if (activationEffect != null)
            {
                Destroy(activationEffect);
                activationEffect = null;
            }
        }

        private void ApplyActiveBase()
        {
            foreach (Material material in materials)
            {
                material.SetColor(BaseColorID, Color.white);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 태그가 아니라 컴포넌트로 판별한다 (Player 태그는 모델 자식에만 붙어 있다)
            PlayerStateManager player = other.GetComponentInParent<PlayerStateManager>();
            if (player != null)
            {
                playerInRange = true;
                playerTransform = player.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerStateManager>() != null)
            {
                playerInRange = false;
                guideShown = false;
            }
        }

        /// <summary>
        /// 인벤토리에서 끌어온 아이템이 UI 밖(바닥)에 놓였을 때 불린다.
        /// 봉인용 블러드스톤을 깨어난 왕좌 앞에서 떨어뜨렸을 때만 봉인을 시작하고, 다른 아이템은 아무 일도 없다.
        /// </summary>
        public static void OnItemDroppedOnGround(Item item)
        {
            if (instance == null || item == null)
            {
                return;
            }

            Item sealStone = QuestManager.Instance != null ? QuestManager.Instance.GetQuest(instance.questID)?.acceptItem : null;
            if (sealStone == null || item != sealStone)
            {
                return;
            }

            if (!instance.isActive || instance.isUsed)
            {
                UISystemMessage.ShowToastWarning("지금은 봉인할 수 없습니다");
                return;
            }

            if (!instance.playerInRange)
            {
                UISystemMessage.ShowToastWarning("붉은 수정 앞으로 가서 바닥에 놓아야 합니다");
                return;
            }

            instance.BeginSealing(sealStone);
        }

        // 블러드스톤 한 개를 바치고 봉인 연출을 시작한다. 인벤토리에 없으면 시작하지 않는다
        private void BeginSealing(Item sealStone)
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null || !inventory.RemoveItem(sealStone, 1))
            {
                UISystemMessage.ShowToastWarning($"봉인에 쓸 {sealStone.ColoredName}이(가) 없습니다");
                return;
            }

            isUsed = true;
            // 안내 대사가 떠 있으면 거둔다
            if (Dialogue.IsOpen)
            {
                Dialogue.CloseDialogue();
            }

            StartEndingMusic();
            StartCoroutine(SealRoutine());
        }

        // 상호작용하는 순간 엔딩 음악을 튼다. 긴 음원이라 장면에 미리 올려 두지 않고 이때 불러온다
        // (음악 쪽이 이전 곡을 줄이는 1초 동안 불러오기가 끝나므로 끊기지 않는다)
        private void StartEndingMusic()
        {
            if (endingMusic == null)
            {
                return;
            }

            endingMusic.LoadAudioData();
            BackgroundMusicPlayer.PlayEndingMusic(endingMusic, endingMusicVolume);
        }

        private IEnumerator SealRoutine()
        {
            BeginCutscene();

            // 보석 위치는 이 시점에 한 번만 잡는다. 이후 보석이 작아져도 중심은 움직이지 않는다
            Vector3 focus = CrystalPosition;

            // 카메라가 서 있는 쪽(보석에서 카메라를 향한 수평 방향)을 그대로 유지한 채 다가간다
            Vector3 away = cutsceneCamera != null ? cutsceneCamera.transform.position - focus : -transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;

            // 1) 카메라가 붉은 보석으로 다가간다
            yield return ZoomToCrystal(focus, away);

            // 2) 보석이 점차 작아지며 봉인된다
            yield return SealCrystal(focus, away);

            // 3) 섬광이 번쩍인 뒤 어두워진다
            yield return FlashAndFadeToBlack();

            // 3-1) 검은 화면 위에 스토리 엔딩을 글자로 들려 준다. 다 끝나야 결과 화면으로 넘어간다
            yield return ShowStory();

            // 4) 결과 화면. 퀘스트 완료 이벤트가 먼저 엔딩을 (보스 사망 대기 시간과 함께) 시작해 버리지 않도록
            // 엔딩을 먼저 예약한 뒤 퀘스트를 완료 처리(보상 지급)한다.
            // 엔딩 화면은 한 프레임 뒤에 통계를 만들기 때문에 지급된 보상이 통계에 반영된다.
            // 결과 화면이 들어 있는 캔버스만 다시 켠다 (나머지 HUD는 되돌리기 전까지 숨겨 둔다)
            UIEndingScreen ending = FindAnyObjectByType<UIEndingScreen>(FindObjectsInactive.Include);
            if (ending != null)
            {
                CinematicUI.Show(ending.GetComponentInParent<Canvas>()?.rootCanvas);
            }

            UIEndingScreen.BeginEnding();
            QuestManager.Instance?.GrantReward(questID);

            // 엔딩 화면이 완전히 뜰 때까지 검은 화면을 유지한다 (장면이 비쳐 보이지 않도록).
            // 엔딩 화면이 없는 씬이면 기다리지 않는다
            float waited = 0f;
            while (!UIEndingScreen.IsFadeComplete && waited < 6f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!UIEndingScreen.IsFadeComplete)
            {
                // 결과 화면이 없으면 연출만 끝내고 게임으로 돌아간다
                RestoreFromCutscene();
            }

            yield return FadeGroup(overlayGroup, 1f, 0f, 0.8f);

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
        }

        // 컷씬을 시작한다: 입력과 피해를 막고, 모든 UI를 숨기고, 카메라를 플레이어 추적에서 분리한다
        private void BeginCutscene()
        {
            current = this;
            cutsceneActive = true;

            InputManager.IsBlocked = true;

            if (playerTransform != null)
            {
                // 연출 중 몬스터에게 맞아 쓰러지면 컷씬이 깨지므로 면역으로 둔다
                playerDamageable = playerTransform.GetComponentInChildren<Damageable>();
                if (playerDamageable != null)
                {
                    playerDamageable.IsImmune = true;
                }

                // 하던 동작(공격 등)을 끊고 서 있는 자세로 둔다
                playerTransform.GetComponent<PlayerStateManager>()?.SetState(PlayerStateManager.State.Idle);
            }

            cutsceneCamera = Camera.main;
            cameraRig = FindAnyObjectByType<CameraRig>();

            if (cutsceneCamera != null)
            {
                savedCameraLocalRotation = cutsceneCamera.transform.localRotation;
                savedFieldOfView = cutsceneCamera.fieldOfView;
            }

            // 리그가 계속 따라다니면 카메라가 서로 밀어내므로 멈춘다 (대화 줌인과 같은 방식)
            if (cameraRig != null)
            {
                cameraRig.enabled = false;
            }

            // F키를 누르는 순간 모든 UI(HUD, 이름표, 안내 문구, 체력바 등)를 숨긴다
            CinematicUI.HideAll(overlayRoot != null ? overlayRoot.GetComponent<Canvas>() : null);
        }

        // 컷씬이 바꾼 것을 모두 원래대로 돌린다. 결과 화면의 버튼(마을로/로비로/종료)과 파괴 시점에서 호출된다
        private void RestoreFromCutscene()
        {
            if (!cutsceneActive)
            {
                return;
            }

            cutsceneActive = false;

            // 엔딩 음악을 끝낸다. 마을로 돌아가면 이동 로딩이 끝난 뒤 현재 지역의 음악이 나온다
            BackgroundMusicPlayer.StopEndingMusic();

            CinematicUI.RestoreAll();
            InputManager.IsBlocked = false;

            if (playerDamageable != null)
            {
                playerDamageable.IsImmune = false;
            }

            if (cutsceneCamera != null)
            {
                cutsceneCamera.transform.localRotation = savedCameraLocalRotation;
                cutsceneCamera.fieldOfView = savedFieldOfView;
            }

            if (cameraRig != null)
            {
                cameraRig.enabled = true;
            }

            if (current == this)
            {
                current = null;
            }
        }

        /// <summary>
        /// 결과 화면에서 게임으로 돌아갈 때 컷씬이 바꿔 둔 UI·입력·카메라를 되돌린다. 컷씬 중이 아니면 아무것도 하지 않는다
        /// </summary>
        public static void EndCutscene()
        {
            if (current != null)
            {
                current.RestoreFromCutscene();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            // 연출 도중 씬이 바뀌어도 입력 차단이나 숨긴 UI가 다음 씬까지 남지 않게 한다
            RestoreFromCutscene();
        }

        // 보석(활성화 이펙트)의 월드 위치. 이펙트가 없으면 이펙트가 놓일 자리를 쓴다
        private Vector3 CrystalPosition => activationEffect != null
            ? activationEffect.transform.position
            : transform.TransformPoint(activationEffectOffset);

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // 카메라를 보석 정면의 정해진 거리에 두고 보석을 바라보게 한다
        private static void PlaceCamera(Transform cameraTransform, Vector3 focus, Vector3 away, float distance, float height, Vector3 shake)
        {
            Vector3 position = focus + away * distance + Vector3.up * height + shake;
            cameraTransform.position = position;
            cameraTransform.rotation = Quaternion.LookRotation(focus - position);
        }

        private IEnumerator ZoomToCrystal(Vector3 focus, Vector3 away)
        {
            if (cutsceneCamera == null)
            {
                yield return new WaitForSeconds(zoomTime);
                yield break;
            }

            Transform cam = cutsceneCamera.transform;
            Vector3 startPosition = cam.position;
            Quaternion startRotation = cam.rotation;
            float startFov = cutsceneCamera.fieldOfView;

            Vector3 endPosition = focus + away * cameraDistance + Vector3.up * cameraHeight;
            Quaternion endRotation = Quaternion.LookRotation(focus - endPosition);

            float elapsed = 0f;
            while (elapsed < zoomTime)
            {
                elapsed += Time.deltaTime;
                // 처음과 끝이 느린 곡선: 천천히 출발해 보석 앞에서 부드럽게 멈춘다
                float t = SmoothStep(Mathf.Clamp01(elapsed / zoomTime));

                cam.position = Vector3.Lerp(startPosition, endPosition, t);
                cam.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                cutsceneCamera.fieldOfView = Mathf.Lerp(startFov, zoomFieldOfView, t);

                yield return null;
            }

            cam.position = endPosition;
            cam.rotation = endRotation;
            cutsceneCamera.fieldOfView = zoomFieldOfView;
        }

        // 보석이 점차 작아지는 동안 왕좌의 붉은 빛이 잦아들고, 카메라는 천천히 더 다가가며 떨림이 커진다
        private IEnumerator SealCrystal(Vector3 focus, Vector3 away)
        {
            Transform crystal = activationEffect != null ? activationEffect.transform : null;
            float startScale = crystal != null ? crystal.localScale.x : 1f;

            float elapsed = 0f;
            while (elapsed < sealTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / sealTime);
                float eased = SmoothStep(t);

                if (crystal != null)
                {
                    crystal.localScale = Vector3.one * (startScale * (1f - eased));
                }

                // 왕좌의 빛: 점점 약해지고, 꺼지기 직전일수록 빠르게 깜빡인다
                float flicker = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed * (1f + 4f * t));
                foreach (Material material in materials)
                {
                    material.SetColor(EmissionColorID, activeEmission * flicker * (1f - eased));
                }

                if (glowLight != null)
                {
                    glowLight.intensity = lightIntensity * flicker * (1f - eased);
                }

                if (cutsceneCamera != null)
                {
                    float distance = Mathf.Lerp(cameraDistance, cameraDistanceEnd, eased);
                    PlaceCamera(cutsceneCamera.transform, focus, away, distance, cameraHeight,
                        Random.insideUnitSphere * (shakeMax * eased));
                }

                yield return null;
            }

            // 봉인 완료: 보석은 사라지고 왕좌는 다시 어두운 돌로 돌아간다
            HideActivationEffect();
            ApplyDormant();
        }

        // 하얀 섬광이 번쩍 차오른 뒤 서서히 검게 변한다. 섬광과 어둠은 같은 이미지의 색만 바꿔 처리한다
        private IEnumerator FlashAndFadeToBlack()
        {
            UnityEngine.UI.Image fill = overlayRoot != null ? overlayRoot.GetComponentInChildren<UnityEngine.UI.Image>(true) : null;
            Color originalColor = fill != null ? fill.color : Color.black;

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }

            if (fill != null)
            {
                fill.color = Color.white;
            }

            yield return FadeGroup(overlayGroup, 0f, 1f, flashTime);

            float elapsed = 0f;
            while (elapsed < fadeToBlackTime)
            {
                elapsed += Time.deltaTime;

                if (fill != null)
                {
                    fill.color = Color.Lerp(Color.white, originalColor, SmoothStep(Mathf.Clamp01(elapsed / fadeToBlackTime)));
                }

                yield return null;
            }

            if (fill != null)
            {
                fill.color = originalColor;
            }

            yield return new WaitForSecondsRealtime(0.4f);
        }

        // 검은 오버레이 위에 스토리를 한 쪽씩 띄운다. 쪽마다 서서히 나타났다가 머문 뒤 사라진다.
        // 스페이스/엔터/마우스 왼쪽 버튼으로 지금 쪽을 건너뛸 수 있다 (컷씬 중에는 InputManager가 막혀 있어 장치를 직접 읽는다)
        private IEnumerator ShowStory()
        {
            TextMeshProUGUI text = storyText != null
                ? storyText
                : overlayRoot != null ? overlayRoot.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            if (text == null || storyPages == null || storyPages.Length == 0)
            {
                yield break;
            }

            text.gameObject.SetActive(true);
            text.alpha = 0f;

            foreach (string page in storyPages)
            {
                if (string.IsNullOrEmpty(page))
                {
                    continue;
                }

                text.text = page;

                yield return FadeText(text, 0f, 1f, storyFadeInTime);
                yield return WaitOrSkip(storyHoldTime);
                yield return FadeText(text, 1f, 0f, storyFadeOutTime);
                yield return new WaitForSecondsRealtime(storyGapTime);
            }

            // 결과 화면이 뜨면서 오버레이가 걷힐 때 글자가 비쳐 보이지 않게 비워 둔다
            text.text = string.Empty;
            text.alpha = 0f;
        }

        private static IEnumerator FadeText(TextMeshProUGUI text, float from, float to, float duration)
        {
            float elapsed = 0f;
            text.alpha = from;

            while (elapsed < duration)
            {
                // 건너뛰기를 누르면 이 단계를 바로 끝낸다 (나타나는 중이면 완전히 보이게, 사라지는 중이면 바로 사라지게)
                if (SkipPressed())
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                text.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            text.alpha = to;
        }

        private static IEnumerator WaitOrSkip(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !SkipPressed())
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static bool SkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            return (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
        }
    }
}
