using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 구르기 상태. 아직 미완성이라 입력이 연결돼 있지 않다 (InputManager.IsRoll이 항상 false).
    ///
    /// 달리는 중·멈춘 중 어느 쪽에서 들어와도 어색하지 않도록, 방향은 들어오는 순간 한 번만 정하고
    /// 구르는 도중에는 돌지 않는다. 실제 이동은 Roll 클립의 루트 모션으로 처리된다 (PlayerStateBase.OnAnimatorMove).
    /// </summary>
    public class PlayerRollState : PlayerStateBase
    {
        private static readonly int RollStateHash = Animator.StringToHash("Roll");

        private Transform cameraTransform;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform == null)
            {
                return;
            }

            // 이동 입력이 있으면 그 방향으로 바로 돌고(달리다 구르기), 없으면 지금 보는 방향으로 구른다
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 direction = InputManager.Movement.x * cameraTransform.right + InputManager.Movement.y * cameraForward;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.LookRotation(direction.normalized);
            }
        }

        protected override void Update()
        {
            base.Update();

            // 구르기 애니메이션이 끝나면 입력에 따라 대기 또는 이동으로 돌아간다
            AnimatorStateInfo currentState = refAnimator.GetCurrentAnimatorStateInfo(0);
            if (currentState.shortNameHash == RollStateHash && currentState.normalizedTime >= 1f)
            {
                manager.SetState(InputManager.Movement == Vector2.zero
                    ? PlayerStateManager.State.Idle
                    : PlayerStateManager.State.Move);
            }
        }
    }
}
