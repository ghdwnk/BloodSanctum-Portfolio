using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스 패턴 실행에 필요한 정보를 묶어 전달하는 컨텍스트 (SkillCastContext와 같은 역할)
    /// </summary>
    public class BossPatternContext
    {
        public Transform BossTransform;
        public CharacterController BossController;
        public Transform PlayerTransform;
        public LayerMask TargetLayer;
        [Tooltip("현재 페이즈 배율까지 반영된 공격력")]
        public float AttackPower;
        [Tooltip("코루틴을 시작할 대상 (BossStateManager)")]
        public MonoBehaviour Caster;
    }
}
