using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// NPC 상태(NPCIdleState·NPCTalkState)의 공통 부모.
    ///
    /// 상태마다 별도 컴포넌트로 NPC 루트에 붙어 있고, NPCStateManager가 지금 상태의 컴포넌트만 켜 둔다
    /// (켜진 컴포넌트의 Update만 돌므로 상태별 처리를 분기문 없이 나눌 수 있다).
    /// 공통으로 바닥에 붙이는 중력 처리와 대화 거리 판정을 제공한다.
    /// </summary>
    public class NPCStateBase : MonoBehaviour
    {
        protected Transform refTransform;
        protected CharacterController characterController;
        protected NPCStateManager manager;

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

            if (manager == null)
            {
                manager = GetComponent<NPCStateManager>();
            }
        }

        protected virtual void Update()
        {
            // NPC는 걷지 않으므로 바닥에 닿기 전까지만 아래로 내린다.
            // 땅에 선 뒤에도 매 프레임 Move를 부르면 NPC 수만큼 물리 계산이 계속 돌기 때문이다
            // (isGrounded는 마지막 Move 결과라, 한 번 닿으면 그대로 true로 남는다)
            if (characterController != null && !characterController.isGrounded)
            {
                characterController.Move(Physics.gravity * Time.deltaTime);
            }
        }

        /// <summary>플레이어가 이 NPC의 대화 가능 거리(NPCData.interactionSight) 안에 있는지.</summary>
        protected bool CanTalk()
        {
            float sight = manager.Data.interactionSight;
            return (manager.PlayerTransform.position - refTransform.position).sqrMagnitude <= sight * sight;
        }
    }
}
