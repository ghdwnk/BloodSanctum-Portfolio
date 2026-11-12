using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 상태 컴포넌트(PlayerIdleState·MoveState·RollState·AttackState·DeadState)의 공통 부모.
    ///
    /// PlayerStateManager가 지금 상태의 컴포넌트만 켜 두므로, 켜진 컴포넌트의 Update·OnAnimatorMove만 돈다.
    /// 공통으로 하는 일:
    ///  - 중력: 매 프레임 아래로 내려 땅에 붙인다
    ///  - 루트 모션: 애니메이션 클립에 들어 있는 이동량(공격 내딛기·구르기 등)을 CharacterController로 옮긴다
    /// </summary>
    public class PlayerStateBase : MonoBehaviour
    {
        protected Transform refTransform;
        protected CharacterController characterController;
        protected Animator refAnimator;
        protected PlayerStateManager manager;
        protected PlayerAnimationController animationController;
        // 이동 속도·회전 속도 등 (PlayerStateManager가 Awake에서 넘겨준다)
        protected PlayerData data;

        protected virtual void OnEnable()
        {
            if (refTransform == null)
            {
                refTransform = transform;
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (refAnimator == null)
            {
                refAnimator = GetComponent<Animator>();
            }

            if (manager == null)
            {
                manager = GetComponent<PlayerStateManager>();
            }

            if (animationController == null)
            {
                animationController = GetComponentInChildren<PlayerAnimationController>();
            }
        }

        protected virtual void Update()
        {
            characterController.Move(Physics.gravity * Time.deltaTime);
        }

        // 이 함수가 있으면 애니메이터가 루트 모션을 직접 적용하지 않고 스크립트에 맡긴다.
        // CharacterController로 옮겨야 벽·지형 충돌이 지켜진다
        protected virtual void OnAnimatorMove()
        {
            characterController.Move(refAnimator.deltaPosition);
        }

        /// <summary>PlayerStateManager가 공용 데이터를 넘겨줄 때 부른다.</summary>
        public void SetData(PlayerData playerData)
        {
            data = playerData;
        }
    }
}
