using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터 애니메이터의 피격(Hit) 상태에 붙는 StateMachineBehaviour.
    /// 상태에 들어가는 순간 Hit 트리거를 지운다. 트리거는 쓰이기 전까지 남아 있어서,
    /// 지우지 않으면 피격 모션이 끝난 직후 같은 트리거로 다시 피격 모션이 재생된다.
    /// </summary>
    public class MonsterHitSMB : StateMachineBehaviour
    {
        private static readonly int HitHash = Animator.StringToHash("Hit");

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.ResetTrigger(HitHash);
        }
    }
}
