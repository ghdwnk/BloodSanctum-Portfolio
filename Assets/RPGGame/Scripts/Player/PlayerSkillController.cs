using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 전투 스킬의 습득·레벨·MP 소모·쿨타임·시전을 관리한다. 플레이어 루트에 붙어 있다.
    ///
    /// 어떤 입력이 어떤 스킬을 쓸지는 밖에서 정한다 (퀵슬롯이 TryCastSkill을 부른다).
    /// 시전 순서: 조건 확인(배웠는지·쿨타임·MP·상태) → MP 소모·쿨타임 시작 → 입력 방향으로 회전 →
    ///           애니메이션 트리거 → 이펙트·소리·효과 → castLockDuration 동안 다른 입력을 막는다(IsCasting).
    ///
    /// 스킬 레벨은 스킬 창에서 스킬 포인트로 올린다 (TryLevelUpSkill). 0레벨 = 아직 배우지 않음.
    /// 세이브에는 배운 스킬의 에셋 이름과 레벨 목록이 저장된다.
    /// </summary>
    public class PlayerSkillController : MonoBehaviour
    {
        [Tooltip("플레이어가 가진 스킬 목록. 스킬 창에 이 순서대로 보인다")]
        [SerializeField] private SkillData[] skills = new SkillData[4];
        [Tooltip("스킬이 맞힐 수 있는 레이어 (몬스터)")]
        [SerializeField] private LayerMask attackTargetLayer;
        [Tooltip("스킬 레벨을 1 올리는 데 드는 스킬 포인트")]
        [SerializeField] private int skillPointCostPerLevel = 1;

        // 스킬별 쿨타임이 끝나는 시각 (Time.time 기준). 매 프레임 줄이지 않고 남은 시간이 필요할 때만 뺀다
        private readonly Dictionary<SkillData, float> cooldownEndTimes = new();
        // 배운 스킬과 레벨 (1~maxLevel). 배우지 않은 스킬은 키가 없다
        private readonly Dictionary<SkillData, int> skillLevels = new();

        // 스킬을 배우거나 레벨이 바뀔 때 발행 (스킬 창·튜토리얼 퀘스트). 코드에서만 구독한다
        private readonly UnityEvent onSkillUnlockChanged = new UnityEvent();

        private Transform refTransform;
        private CharacterController characterController;
        private MPController mpController;
        private PlayerAnimationController animationController;
        private PlayerStateManager stateManager;
        private WeaponController weaponController;
        private PlayerStats playerStats;
        private PlayerDashController dashController;
        private PlayerAttackState attackState;
        private Transform cameraTransform;

        /// <summary>시전 중인지. 이 동안 PlayerStateManager가 이동·공격 입력을 무시한다.</summary>
        public bool IsCasting { get; private set; }

        public int SkillCount => skills != null ? skills.Length : 0;

        private void Awake()
        {
            refTransform = transform;
            characterController = GetComponent<CharacterController>();
            mpController = GetComponentInChildren<MPController>();
            animationController = GetComponentInChildren<PlayerAnimationController>();
            stateManager = GetComponent<PlayerStateManager>();
            weaponController = GetComponentInChildren<WeaponController>();
            playerStats = GetComponent<PlayerStats>();
            dashController = GetComponent<PlayerDashController>();
            attackState = GetComponent<PlayerAttackState>();
        }

        public void SubscribeOnSkillUnlockChanged(UnityAction listener)
        {
            onSkillUnlockChanged.AddListener(listener);
        }

        // ───────────────────────── 조회 ─────────────────────────

        /// <summary>스킬 창 slotIndex번 스킬. 범위 밖이면 null.</summary>
        public SkillData GetSkill(int slotIndex)
        {
            return skills != null && slotIndex >= 0 && slotIndex < skills.Length ? skills[slotIndex] : null;
        }

        /// <summary>스킬의 현재 레벨. 배우지 않았으면 0.</summary>
        public int GetSkillLevel(SkillData skill)
        {
            return skill != null && skillLevels.TryGetValue(skill, out int level) ? level : 0;
        }

        /// <summary>배웠는지 (1레벨 이상).</summary>
        public bool IsSkillUnlocked(SkillData skill)
        {
            return GetSkillLevel(skill) > 0;
        }

        /// <summary>남은 쿨타임(초). 쿨타임이 아니면 0.</summary>
        public float GetCooldownRemaining(SkillData skill)
        {
            if (skill == null || !cooldownEndTimes.TryGetValue(skill, out float endTime))
            {
                return 0f;
            }

            return Mathf.Max(0f, endTime - Time.time);
        }

        /// <summary>
        /// 스킬 레벨과 장비의 쿨타임 감소가 모두 반영된 실제 쿨타임(초).
        /// 시전할 때 쓰는 값과 퀵슬롯 UI가 그리는 전체 시간이 어긋나지 않도록 한 곳에서 계산한다.
        /// </summary>
        public float GetCooldownDuration(SkillData skill)
        {
            if (skill == null)
            {
                return 0f;
            }

            float multiplier = playerStats != null ? playerStats.CooldownMultiplier : 1f;
            return skill.GetCooldown(Mathf.Max(1, GetSkillLevel(skill))) * multiplier;
        }

        // ───────────────────────── 습득·레벨업 ─────────────────────────

        /// <summary>
        /// 스킬 포인트를 써서 레벨을 1 올린다. 배우지 않은 스킬이면 1레벨로 배운다.
        /// 최대 레벨·요구 플레이어 레벨·포인트 부족이면 이유를 안내하고 false.
        /// </summary>
        public bool TryLevelUpSkill(SkillData skill)
        {
            if (skill == null)
            {
                return false;
            }

            int currentLevel = GetSkillLevel(skill);

            if (currentLevel >= skill.maxLevel)
            {
                UISystemMessage.Show($"{skill.skillName}은(는) 이미 최대 레벨입니다.");
                return false;
            }

            // 궁극기처럼 요구 플레이어 레벨이 있는 스킬은 그 레벨이 돼야 배울 수 있다 (레벨업할 때만 확인하므로 캐시하지 않는다)
            int requiredLevel = skill.GetRequiredPlayerLevel(currentLevel + 1);
            if (requiredLevel > 1)
            {
                PlayerLevelController levelController = FindAnyObjectByType<PlayerLevelController>();
                if (levelController != null && levelController.Level < requiredLevel)
                {
                    UISystemMessage.Show(currentLevel == 0
                        ? $"{skill.skillName}은(는) 레벨 {requiredLevel} 이상부터 배울 수 있습니다."
                        : $"{skill.skillName}을(를) 올리려면 레벨 {requiredLevel} 이상이어야 합니다.");
                    return false;
                }
            }

            if (playerStats == null || !playerStats.TryConsumeSkillPoint(skillPointCostPerLevel))
            {
                UISystemMessage.Show("스킬 포인트가 부족합니다.");
                return false;
            }

            int newLevel = currentLevel + 1;
            skillLevels[skill] = newLevel;
            onSkillUnlockChanged.Invoke();

            UISystemMessage.ShowToast(currentLevel == 0
                ? $"{skill.skillName}을(를) 배웠습니다."
                : $"{skill.skillName} 레벨 {newLevel} 달성");

            return true;
        }

        // ───────────────────────── 시전 ─────────────────────────

        /// <summary>스킬 시전을 시도한다 (퀵슬롯이 부른다). 시전했으면 true.</summary>
        public bool TryCastSkill(SkillData skill)
        {
            if (skill == null || IsCasting || (dashController != null && dashController.IsDashing))
            {
                return false;
            }

            // 기본 공격 중에는 쓸 수 없지만, 공격을 끊고 쓰는 스킬(cancelsBasicAttack)은 예외다
            if (stateManager != null &&
                (stateManager.IsPlayerDead ||
                 stateManager.CurrentState == PlayerStateManager.State.Roll ||
                 (stateManager.CurrentState == PlayerStateManager.State.Attack && !skill.cancelsBasicAttack)))
            {
                return false;
            }

            int skillLevel = GetSkillLevel(skill);
            if (skillLevel <= 0)
            {
                UISystemMessage.ShowToastWarning($"{skill.skillName}은(는) 아직 배우지 않은 스킬입니다.");
                return false;
            }

            if (GetCooldownRemaining(skill) > 0f)
            {
                UISystemMessage.ShowToastWarning($"{skill.skillName} 재사용 대기 중입니다.");
                return false;
            }

            // MP 소모와 쿨타임은 스킬 레벨에 따라 줄어든 값을 쓴다
            if (mpController == null || !mpController.TryConsumeMP(skill.GetMPCost(skillLevel)))
            {
                UISystemMessage.ShowToastWarning("MP가 부족합니다.");
                return false;
            }

            cooldownEndTimes[skill] = Time.time + GetCooldownDuration(skill);

            // 시전이 확정된 뒤에 기본 공격을 끊는다 (MP 부족·쿨타임으로 실패했을 때는 공격이 그대로 이어진다)
            if (skill.cancelsBasicAttack && stateManager != null &&
                stateManager.CurrentState == PlayerStateManager.State.Attack)
            {
                CancelBasicAttack();
            }

            StartCoroutine(CastRoutine(skill, skillLevel));
            return true;
        }

        // 진행 중인 기본 공격을 끊는다. 대쉬(PlayerDashController)와 같은 정리를 한다:
        // 무기 판정을 끄고 콤보·상태를 대기로 되돌려, 공격 애니메이션 이벤트가 오지 않아도 꼬이지 않게 한다
        private void CancelBasicAttack()
        {
            if (attackState != null)
            {
                attackState.CancelAttack();
            }

            if (weaponController != null)
            {
                weaponController.CancelAttackEffects();
            }

            // 애니메이터의 콤보 값도 비운다. 남겨 두면 스킬이 끝난 뒤 이전 콤보가 이어지는 것처럼 보일 수 있다
            if (animationController != null)
            {
                animationController.SetAttackComboState((int)PlayerStateManager.AttackCombo.None);
            }
        }

        private IEnumerator CastRoutine(SkillData skill, int skillLevel)
        {
            IsCasting = true;

            // 돌진 강타는 바라보는 방향으로 돌진하므로, 시전 직전에 입력 방향으로 몸을 돌려 둔다
            FaceInputDirection();

            // 시전 중에는 제자리에 서 있어야 한다. PlayerStateManager는 시전 중 입력만 무시하고 상태는 그대로 두므로,
            // 이동 상태인 채로 시전하면 PlayerMoveState가 계속 캐릭터를 민다. 대기 상태로 내려 이동을 끊는다
            if (stateManager != null)
            {
                stateManager.SetState(PlayerStateManager.State.Idle);
            }

            if (animationController != null)
            {
                animationController.PlayTrigger(skill.animationTrigger);
            }

            SkillCastContext context = new SkillCastContext
            {
                PlayerTransform = refTransform,
                PlayerController = characterController,
                PlayerStats = playerStats,
                TargetLayer = attackTargetLayer,
                Caster = this,
                SkillLevel = skillLevel
            };

            // 이펙트·소리는 효과와 따로, 각자의 지연 시간에 맞춰 생긴다
            skill.PlayVfx(context);
            skill.PlaySfx(context);
            skill.ApplyEffect(context);

            yield return new WaitForSeconds(skill.castLockDuration);

            IsCasting = false;
        }

        // 이동 입력이 있으면 카메라 기준 입력 방향으로 바로 돌린다. 입력이 없으면 지금 방향 그대로 쓴다
        private void FaceInputDirection()
        {
            if (InputManager.Movement == Vector2.zero)
            {
                return;
            }

            if (cameraTransform == null)
            {
                if (Camera.main == null)
                {
                    return;
                }

                cameraTransform = Camera.main.transform;
            }

            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 direction = InputManager.Movement.x * cameraTransform.right + InputManager.Movement.y * cameraForward;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // ───────────────────────── 세이브 ─────────────────────────

        /// <summary>세이브용: 배운 스킬의 에셋 이름 목록 (GetSkillLevelValues와 같은 순서).</summary>
        public List<string> GetUnlockedSkillNames()
        {
            List<string> names = new List<string>(skillLevels.Count);
            foreach (KeyValuePair<SkillData, int> pair in skillLevels)
            {
                names.Add(pair.Key.name);
            }

            return names;
        }

        /// <summary>세이브용: 위 이름 목록과 같은 순서의 레벨 목록.</summary>
        public List<int> GetSkillLevelValues()
        {
            List<int> levels = new List<int>(skillLevels.Count);
            foreach (KeyValuePair<SkillData, int> pair in skillLevels)
            {
                levels.Add(pair.Value);
            }

            return levels;
        }

        /// <summary>
        /// 저장된 이름·레벨 목록으로 배운 스킬을 되살린다.
        /// 가진 스킬 목록(skills)에서만 이름을 찾으므로 따로 조회 표가 필요 없다.
        /// 레벨 목록이 없는 예전 세이브는 배운 스킬을 모두 1레벨로 본다.
        /// </summary>
        public void LoadUnlockedSkills(List<string> savedNames, List<int> savedLevels = null)
        {
            skillLevels.Clear();

            if (savedNames != null)
            {
                foreach (SkillData skill in skills)
                {
                    if (skill == null)
                    {
                        continue;
                    }

                    int index = savedNames.IndexOf(skill.name);
                    if (index < 0)
                    {
                        continue;
                    }

                    int level = savedLevels != null && index < savedLevels.Count ? savedLevels[index] : 1;
                    skillLevels[skill] = Mathf.Clamp(level, 1, skill.maxLevel);
                }
            }

            onSkillUnlockChanged.Invoke();
        }
    }
}
