using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 스킬을 쓸 때 필요한 정보를 한데 묶어 SkillData.ApplyEffect·PlayVfx·PlaySfx에 넘기는 꾸러미.
    /// PlayerSkillController가 시전할 때마다 만든다.
    /// </summary>
    public class SkillCastContext
    {
        /// <summary>시전자(플레이어)의 위치·방향. 이펙트 생성과 판정 위치의 기준.</summary>
        public Transform PlayerTransform;
        /// <summary>돌진처럼 플레이어를 직접 움직이는 스킬이 쓴다.</summary>
        public CharacterController PlayerController;
        /// <summary>버프를 스탯 계층에 적용하고, 피해 계산에 플레이어 공격력을 읽는 데 쓴다.</summary>
        public PlayerStats PlayerStats;
        /// <summary>스킬이 맞힐 수 있는 레이어 (몬스터).</summary>
        public LayerMask TargetLayer;
        /// <summary>코루틴을 돌릴 컴포넌트 (PlayerSkillController). ScriptableObject는 코루틴을 직접 돌릴 수 없다.</summary>
        public MonoBehaviour Caster;
        /// <summary>현재 스킬 레벨. 각 스킬이 GetPowerMultiplier 등으로 수치를 키우는 데 쓴다.</summary>
        public int SkillLevel = 1;
    }
}
