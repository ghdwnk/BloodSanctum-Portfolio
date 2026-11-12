using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 애니메이터의 공격 콤보 상태(AttackCombo1~3)에 붙는 StateMachineBehaviour.
    /// 상태에 들어가는 순간 그 콤보에 맞는 칼 궤적 이펙트를 재생한다 (WeaponController.PlayAttackComboEffect).
    /// </summary>
    public class PlayerAttackComboSMB : StateMachineBehaviour
    {
        [Tooltip("재생할 공격 이펙트 번호 (WeaponController의 이펙트 목록 순서). -1이면 재생하지 않는다")]
        [SerializeField] private int effectIndex = -1;

        [Tooltip("상태에 들어간 뒤 휘두르는 소리를 늦추는 시간(초). 선딜이 있는 동작(3타 찌르기)에서 실제 타격 순간에 소리를 맞춘다")]
        [SerializeField] private float soundDelay;

        [Tooltip("공격속도 보너스를 몇 배로 받을지. 1이면 그대로, 2면 두 배로 적용돼 이 콤보의 선딜·후딜이 더 크게 줄어든다")]
        [SerializeField] private float attackSpeedBonusScale = 1f;

        private WeaponController weaponController;
        private PlayerAnimationController animationController;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 이펙트·소리 지연을 정하기 전에 이 콤보의 재생 속도부터 정한다
            if (animationController == null)
            {
                animationController = animator.GetComponentInChildren<PlayerAnimationController>();
            }

            if (animationController != null)
            {
                animationController.SetComboAttackSpeedBonusScale(attackSpeedBonusScale);
            }

            if (effectIndex < 0)
            {
                return;
            }

            if (weaponController == null)
            {
                weaponController = animator.GetComponentInChildren<WeaponController>();
            }

            if (weaponController != null)
            {
                weaponController.PlayAttackComboEffect(effectIndex, soundDelay, animator.speed);
            }
        }
    }
}
