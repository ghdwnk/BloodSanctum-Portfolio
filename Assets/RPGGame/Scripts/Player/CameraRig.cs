using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 3인칭 카메라 리그. 리그(이 오브젝트)가 플레이어를 부드럽게 따라가고, 자식인 메인 카메라는
    /// 리그 기준의 고정된 각도에서 플레이어를 내려다본다.
    ///
    /// - 이동: 플레이어 위치로 Lerp (movementDelay)
    /// - 회전: 마우스 좌우 이동으로만 돈다. 상하 각도는 카메라의 고정 기울기를 그대로 쓴다
    /// - 거리: 줌 기능은 없고 고정 시점이다. 씬에 놓인 카메라 배치에서 시선 반대 방향으로 fixedPullBackDistance만큼 물러나 있다
    /// - 흔들림: AddShake로 요청 (타격·피격)
    /// 대화 줌인 중에는 DialogueCameraController가 이 컴포넌트를 잠시 끄고 카메라를 직접 움직인다.
    /// 플레이어가 순간이동(텔레포트·부활)하면 SnapToTarget으로 바로 붙인다.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        // 텔레포트·부활·흔들림을 정적 함수로 요청하기 위한 인스턴스
        private static CameraRig instance;

        [Header("따라가기")]
        [Tooltip("따라갈 대상. 비워 두면 Player 태그 오브젝트를 찾는다")]
        [SerializeField] private Transform followTarget;
        [Tooltip("플레이어 상태 (스폰 전·사망 중에는 카메라를 움직이지 않는다). 비워 두면 찾는다")]
        [SerializeField] private PlayerStateManager playerStateManager;
        [Tooltip("위치를 따라가는 빠르기. 클수록 바로 붙는다")]
        [SerializeField] private float movementDelay = 5f;

        [Header("회전")]
        [Tooltip("회전이 목표 각도를 따라가는 빠르기")]
        [SerializeField] private float rotationDelay = 5f;
        [Tooltip("마우스 이동 1당 도는 각도")]
        [SerializeField] private float rotationSpeed = 0.2f;

        [Header("시점 거리")]
        [Tooltip("씬에 놓인 카메라 배치에서 시선 반대 방향으로 물러나 있는 고정 거리(m). 예전 줌 범위(0~7m)의 중간값이다")]
        [SerializeField] private float fixedPullBackDistance = 3.5f;

        private Transform refTransform;
        private Camera refCamera;
        // 목표 좌우 각도
        private float yRotation;

        // 흔들림을 얹기 전의 카메라 로컬 위치와, 카메라가 물러나는 방향(시선의 반대)
        private Vector3 baseCameraLocalPosition;
        private Vector3 pullBackDirection;

        // 흔들림 상태. 줌과 합쳐 매 프레임 카메라 로컬 위치를 다시 만든다 (ApplyCameraOffsets)
        private float shakeMagnitude;
        private float shakeDuration;
        private float shakeEndTime;

        private void Awake()
        {
            instance = this;
            refTransform = transform;

            if (followTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                followTarget = player != null ? player.transform : null;
            }

            if (playerStateManager == null)
            {
                playerStateManager = FindAnyObjectByType<PlayerStateManager>();
            }

            refCamera = Camera.main;
            if (refCamera != null)
            {
                // 씬에 놓인 카메라 배치를 기준점으로 기억한다. 시선 반대 방향으로만 물러나므로
                // 플레이어가 화면 가운데에 머물고 기울기도 그대로 유지된다
                baseCameraLocalPosition = refCamera.transform.localPosition;
                pullBackDirection = -(refCamera.transform.localRotation * Vector3.forward);
            }

            if (followTarget == null)
            {
                return;
            }

            // 시작하자마자 플레이어 위치로 붙인다 (다른 위치에서 서서히 따라오는 모습이 보이지 않게)
            refTransform.position = followTarget.position;

            // 플레이어가 바라보는 방향의 뒤에서 시작한다.
            // 0도로 두면 씬에 놓인 플레이어 방향과 어긋나 처음 켰을 때 캐릭터를 정면에서 보게 된다
            Transform facing = playerStateManager != null ? playerStateManager.transform : followTarget;
            yRotation = facing.eulerAngles.y;
            refTransform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ───────────────────────── 외부 요청 ─────────────────────────

        /// <summary>바라보는 좌우 각도를 즉시 맞춘다 (보스전 테스트 시작처럼 정해진 방향을 보여 줄 때).</summary>
        public static void SetYaw(float yaw)
        {
            if (instance == null)
            {
                return;
            }

            instance.yRotation = yaw;
            instance.refTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// 리그를 플레이어 위치로 즉시 옮긴다. 수백 미터 떨어진 곳으로 순간이동했을 때
        /// 1~2초 동안 카메라가 허공을 가로질러 날아오는 것을 막는다.
        /// </summary>
        public static void SnapToTarget()
        {
            if (instance == null || instance.followTarget == null)
            {
                return;
            }

            instance.refTransform.position = instance.followTarget.position;
        }

        /// <summary>카메라를 짧게 흔든다. 약한 흔들림이 진행 중인 더 강한 흔들림을 덮어쓰지 않는다.</summary>
        public static void AddShake(float magnitude, float duration)
        {
            if (instance == null || magnitude <= 0f)
            {
                return;
            }

            float remaining = Mathf.Max(0f, instance.shakeEndTime - Time.time);
            float currentMagnitude = instance.shakeDuration > 0f
                ? instance.shakeMagnitude * remaining / instance.shakeDuration
                : 0f;

            if (magnitude < currentMagnitude)
            {
                return;
            }

            instance.shakeMagnitude = magnitude;
            instance.shakeDuration = duration;
            instance.shakeEndTime = Time.time + duration;
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        // 플레이어가 이번 프레임에 움직인 뒤 따라가야 떨리지 않으므로 LateUpdate에서 처리한다
        private void LateUpdate()
        {
            ApplyCameraOffsets();

            if (followTarget == null || playerStateManager == null)
            {
                return;
            }

            // 스폰 연출 전이거나 죽어 있으면 카메라를 멈춘다
            PlayerStateManager.State currentState = playerStateManager.CurrentState;
            if (currentState is PlayerStateManager.State.None or PlayerStateManager.State.Dead)
            {
                return;
            }

            refTransform.position = Vector3.Lerp(refTransform.position, followTarget.position, movementDelay * Time.deltaTime);

            // 창이 열려 있으면 마우스는 UI의 몫이라 돌리지 않는다
            if (UIWindowState.IsAnyWindowOpen)
            {
                return;
            }

            // 좌우만 돈다 (상하 각도는 카메라 자체의 고정 기울기)
            yRotation += InputManager.MouseMove.x * rotationSpeed;
            Quaternion endRotation = Quaternion.Euler(0f, yRotation, 0f);
            refTransform.rotation = Quaternion.Slerp(refTransform.rotation, endRotation, rotationDelay * Time.deltaTime);
        }

        // 카메라 위치 = 기본 위치 + 고정 물러남 거리 + 흔들림. 매 프레임 처음부터 다시 만들어서,
        // 다른 연출(대화 줌인 복귀 등)이 카메라 위치를 건드려도 다음 프레임에 어긋남이 남지 않는다
        private void ApplyCameraOffsets()
        {
            if (refCamera == null)
            {
                return;
            }

            Vector3 shake = Vector3.zero;
            float remaining = shakeEndTime - Time.time;

            if (remaining > 0f && shakeDuration > 0f)
            {
                // 시간이 지날수록 약해진다
                shake = Random.insideUnitSphere * (shakeMagnitude * remaining / shakeDuration);
            }

            refCamera.transform.localPosition = baseCameraLocalPosition + pullBackDirection * fixedPullBackDistance + shake;
        }
    }
}
