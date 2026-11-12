using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 애니메이터의 등장(Spawn) 상태에 붙는 StateMachineBehaviour.
    /// 등장 애니메이션이 끝나 상태를 빠져나갈 때 플레이어를 대기 상태로 바꿔 조작을 시작하게 한다.
    /// (그 전까지 PlayerStateManager는 None 상태라 입력을 받지 않는다)
    /// </summary>
    public class PlayerSpawnSMB : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            PlayerStateManager stateManager = animator.GetComponent<PlayerStateManager>();
            if (stateManager != null)
            {
                stateManager.SetState(PlayerStateManager.State.Idle);
            }
        }
    }
}
