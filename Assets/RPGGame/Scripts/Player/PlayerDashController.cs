using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 스페이스바로 앞으로 빠르게 대쉬하는 기능과 쿨타임을 관리한다. 플레이어 루트에 붙어 있다.
    ///
    /// 돌진 강타처럼 CharacterController로 직접 움직이며 피해는 주지 않는다.
    /// 상태 머신에 새 상태를 넣지 않고, 스킬 시전과 같은 방식(IsDashing)으로 다른 입력을 막는다.
    /// 공격 중에 쓰면 공격을 끊는다. 창이 열려 있거나 스킬 시전 중에는 쓸 수 없다.
    /// 대화창(Dialogue)이 같은 스페이스 입력을 먼저 쓸 수 있도록 실행 순서를 뒤(50)로 둔다.
    /// 쿨타임 표시는 UIDashCooldown이 CooldownRemaining / Cooldown으로 그린다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PlayerDashController : MonoBehaviour
    {
        [Tooltip("대쉬 거리(m)")]
        [SerializeField] private float dashDistance = 4.5f;
        [Tooltip("대쉬에 걸리는 시간(초)")]
        [SerializeField] private float dashDuration = 0.33f;
        [Tooltip("기본 쿨타임(초). 장비의 대쉬 쿨타임 감소가 곱해진다")]
        [SerializeField] private float cooldown = 3f;
        [Tooltip("대쉬 애니메이션 트리거 이름")]
        [SerializeField] private string animationTrigger = "Dash";
        [Tooltip("대쉬를 시작할 때 한 번 내는 소리")]
        [SerializeField] private AudioClip dashSound;
        [SerializeField, Range(0f, 1f)] private float dashSoundVolume = 0.88f;

        private Transform refTransform;
        private CharacterController characterController;
        private PlayerStateManager stateManager;
        private PlayerSkillController skillController;
        private PlayerStats playerStats;
        private PlayerAttackState attackState;
        private WeaponController weaponController;
        private PlayerAnimationController animationController;
        private Transform cameraTransform;

        // 마지막 대쉬를 시작한 시각. 쿨타임은 남은 시간을 매 프레임 깎는 대신 이 값으로 계산한다
        private float lastDashTime = -999f;

        // 대쉬 중에는 이동/공격/스킬 입력을 막기 위해 PlayerStateManager와 PlayerSkillController가 참조한다
        public bool IsDashing { get; private set; }
        // 장비의 대쉬 쿨타임 감소가 반영된 실제 쿨타임. UI의 전체 시간 표시도 이 값을 쓴다
        public float Cooldown => cooldown * (playerStats != null ? playerStats.DashCooldownMultiplier : 1f);

        public float CooldownRemaining => Mathf.Max(0f, lastDashTime + Cooldown - Time.time);

        private void Awake()
        {
            refTransform = transform;
            characterController = GetComponent<CharacterController>();
            stateManager = GetComponent<PlayerStateManager>();
            skillController = GetComponent<PlayerSkillController>();
            playerStats = GetComponent<PlayerStats>();
            attackState = GetComponent<PlayerAttackState>();
            weaponController = GetComponentInChildren<WeaponController>();
            animationController = GetComponentInChildren<PlayerAnimationController>();
        }

        private void Update()
        {
            if (InputManager.IsDash)
            {
                TryDash();
            }
        }

        // 대쉬를 시도하고 성공 여부를 돌려준다
        public bool TryDash()
        {
            if (IsDashing || CooldownRemaining > 0f)
            {
                return false;
            }

            if (stateManager == null || stateManager.IsPlayerDead)
            {
                return false;
            }

            // 걷기/서 있기/공격 중에 발동한다. 구르기·사망 상태에서는 쓸 수 없다
            PlayerStateManager.State state = stateManager.CurrentState;
            if (state != PlayerStateManager.State.Idle && state != PlayerStateManager.State.Move &&
                state != PlayerStateManager.State.Attack)
            {
                return false;
            }

            if (skillController != null && skillController.IsCasting)
            {
                return false;
            }

            // 창을 열어 둔 상태(인벤토리, 상점, 선택지가 있는 대화 등)에서는 쓰지 않는다
            if (UIWindowState.IsAnyWindowOpen || UITeleportScreen.IsOn)
            {
                return false;
            }

            // 공격 중이면 대쉬가 공격을 끊는다: 무기 판정, 콤보, 궤적 이펙트를 정리하고 Idle로 돌린다
            if (state == PlayerStateManager.State.Attack)
            {
                CancelAttack();
            }

            lastDashTime = Time.time;
            StartCoroutine(DashRoutine());
            return true;
        }

        private void CancelAttack()
        {
            if (attackState != null)
            {
                attackState.CancelAttack();
            }

            if (weaponController != null)
            {
                weaponController.CancelAttackEffects();
            }
        }

        private IEnumerator DashRoutine()
        {
            IsDashing = true;

            FaceInputDirection();

            // Move 상태로 둔 채 대쉬하면 PlayerMoveState가 이동 입력만큼 더 밀어낸다.
            // 스킬 시전과 같은 이유로 Idle로 내려 이동을 끊는다 (중력은 Idle 상태에서도 적용된다)
            stateManager.SetState(PlayerStateManager.State.Idle);

            if (animationController != null)
            {
                animationController.PlayTrigger(animationTrigger);
            }

            SfxPlayer.Play(dashSound, dashSoundVolume);

            float elapsed = 0f;
            float previousDistance = 0f;

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;

                // 처음에 가장 빠르고 끝으로 갈수록 느려지는 곡선(ease-out)으로 이동 거리를 계산하고,
                // 이전 프레임과의 차이만큼만 움직인다. 프레임 시간이 들쭉날쭉해도 총 이동 거리는 일정하다
                float t = Mathf.Clamp01(elapsed / dashDuration);
                float distance = dashDistance * (1f - (1f - t) * (1f - t));
                characterController.Move(refTransform.forward * (distance - previousDistance));
                previousDistance = distance;

                // 대쉬 도중에 죽거나 텔레포트하면 멈춘다
                if (stateManager.IsPlayerDead)
                {
                    break;
                }

                yield return null;
            }

            IsDashing = false;
        }

        // 이동 입력이 있으면 카메라 기준 입력 방향으로, 없으면 지금 바라보는 방향 그대로 대쉬한다
        private void FaceInputDirection()
        {
            if (InputManager.Movement == Vector2.zero)
            {
                return;
            }

            if (cameraTransform == null)
            {
                cameraTransform = Camera.main != null ? Camera.main.transform : null;

                if (cameraTransform == null)
                {
                    return;
                }
            }

            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 direction = InputManager.Movement.x * cameraTransform.right +
                                InputManager.Movement.y * cameraForward;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // 대쉬 도중 비활성화되면 코루틴이 멈추므로 플래그를 직접 내린다 (켜진 채로 남으면 입력이 영영 막힌다)
        private void OnDisable()
        {
            IsDashing = false;
        }
    }
}
