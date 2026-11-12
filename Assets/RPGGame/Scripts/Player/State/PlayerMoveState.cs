using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 이동 상태. 카메라가 보는 방향을 기준으로 WASD 입력 방향으로 돌고 달린다.
    ///
    /// 달리기 클립(Running)에는 루트 모션 이동량이 없어서 이동은 코드로 한다.
    /// 속도 = PlayerData.moveSpeed x 둔화 배율(PlayerSlowEffect) x 장비 이동속도 배율(PlayerStats).
    /// 발소리는 클립에 이벤트를 심는 대신 일정 간격 타이머로 낸다 (빨리 달리면 그만큼 촘촘해진다).
    /// </summary>
    public class PlayerMoveState : PlayerStateBase
    {
        [Header("발소리")]
        [Tooltip("발소리를 낼 AudioPlayer")]
        [SerializeField] private AudioPlayer footstepAudio;
        [Tooltip("발소리 간격(초, 기본 속도 기준)")]
        [SerializeField] private float stepInterval = 0.32f;

        private Transform cameraTransform;
        private PlayerStats playerStats;
        // 둔화는 처음 맞을 때 컴포넌트가 런타임에 붙으므로 없으면 다시 찾는다
        private PlayerSlowEffect slowEffect;
        private float stepTimer;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (playerStats == null)
            {
                playerStats = GetComponent<PlayerStats>();
            }
        }

        protected override void Update()
        {
            base.Update();

            Vector3 direction = GetMoveDirection();

            if (direction == Vector3.zero)
            {
                // 멈추는 순간 다음 걸음에서 바로 발소리가 나도록 타이머를 채워 둔다
                stepTimer = stepInterval;
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            refTransform.rotation = Quaternion.RotateTowards(refTransform.rotation, targetRotation,
                data.rotationSpeed * Time.deltaTime);

            float speedMultiplier = GetSpeedMultiplier();
            characterController.Move(direction * (data.moveSpeed * speedMultiplier * Time.deltaTime));

            // 실제로 움직인 프레임에만 타이머를 진행해 서 있을 때는 발소리가 나지 않게 한다
            stepTimer += Time.deltaTime * speedMultiplier;
            if (stepTimer >= stepInterval)
            {
                stepTimer = 0f;

                if (footstepAudio != null)
                {
                    footstepAudio.Play();
                }
            }
        }

        // 카메라 기준 입력 방향 (수평, 길이 최대 1). 입력이 없으면 zero
        private Vector3 GetMoveDirection()
        {
            if (cameraTransform == null)
            {
                return Vector3.zero;
            }

            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 direction = InputManager.Movement.x * cameraTransform.right + InputManager.Movement.y * cameraForward;
            direction.y = 0f;

            // 대각선 입력이 더 빠르지 않도록 길이를 1로 자른다
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return direction;
        }

        // 둔화와 장비 이동속도 증가는 곱으로 겹친다
        private float GetSpeedMultiplier()
        {
            if (slowEffect == null)
            {
                slowEffect = GetComponent<PlayerSlowEffect>();
            }

            float multiplier = slowEffect != null ? slowEffect.MoveSpeedMultiplier : 1f;

            if (playerStats != null)
            {
                multiplier *= playerStats.MoveSpeedMultiplier;
            }

            return multiplier;
        }
    }
}
