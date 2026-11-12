using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 플레이어의 최종 능력치를 계산하고 실제 컴포넌트에 넣어 주는 곳. 플레이어 루트에 붙어 있다.
    ///
    /// 최종 능력치 = 레벨 기본값(PlayerData 레벨 표) + 스탯 포인트 투자분 + 장비 보정 + 일시 버프.
    /// 값이 바뀔 때마다 HPController(최대 HP)·MPController(최대 MP)·Damageable(방어력)에 다시 넣고,
    /// 스탯창·버프 HUD·애니메이션 속도 등은 SubscribeOnStatsChanged로 변화를 구독한다.
    ///
    /// 값을 넣는 쪽:
    ///  - 레벨: PlayerStateManager.OnPlayerLevelUP → SetLevel
    ///  - 포인트: 레벨업(GrantLevelUpPoints), 스탯창(TryAllocateStatPoint), 스킬창(TryConsumeSkillPoint)
    ///  - 장비: PlayerEquipment → SetEquipmentBonus
    ///  - 버프: 분노의 함성·강철 방어 → ApplyTemporaryBonus
    ///
    /// 실행 순서를 -60으로 앞당겨, 다른 컴포넌트의 Awake·Start보다 먼저 레벨 1 기본값이 들어가 있게 한다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class PlayerStats : MonoBehaviour
    {
        /// <summary>스탯 포인트를 투자할 수 있는 능력치 (allocatedPoints 배열 순서, 세이브에도 이 순서로 저장된다).</summary>
        public enum StatType { Attack, Defense, MaxHP, CritRate, Length }

        /// <summary>스킬 쿨타임 감소율 상한(%). 쿨타임이 0에 가까워지지 않게 한다 (대쉬 쿨타임에도 같은 상한).</summary>
        public const float MaxCooldownReduction = 50f;
        /// <summary>이동속도 증가율 상한(%). +30%면 기본 5m/s가 6.5m/s.</summary>
        public const float MaxMoveSpeedBonus = 30f;
        /// <summary>공격속도 증가율 상한(%). +50%면 같은 콤보를 1.5배 빠르게 끝낸다.</summary>
        public const float MaxAttackSpeedBonus = 50f;

        // ───── 포인트 ─────
        private int availableStatPoints;
        private int availableSkillPoints;
        // 능력치별 투자한 포인트 수 (StatType 순서)
        private readonly int[] allocatedPoints = new int[(int)StatType.Length];

        // ───── 장비 보정 (PlayerEquipment가 세 부위를 합쳐 넘겨준다) ─────
        private float equipmentAttack;
        private float equipmentDefense;
        private float equipmentMaxHP;
        private float equipmentCritRate;
        private float equipmentCritDamage;
        private float equipmentCooldownReduction;
        private float equipmentDashCooldownReduction;
        private float equipmentMoveSpeed;
        private float equipmentAttackSpeed;

        // ───── 일시 버프 ─────
        private float buffAttack;
        private float buffDefense;
        private float buffCritRate;
        // 능력치별로 진행 중인 버프 코루틴 (같은 능력치에 다시 걸리면 새로 시작한다)
        private readonly Coroutine[] buffRoutines = new Coroutine[(int)StatType.Length];
        // 버프 HUD가 남은 시간을 그릴 수 있게 끝나는 시각과 보정량을 같이 기억한다
        private readonly float[] buffEndTimes = new float[(int)StatType.Length];
        private readonly float[] buffAmounts = new float[(int)StatType.Length];

        // 최종 능력치가 바뀔 때마다 발행. 코드에서만 구독하므로 인스펙터에 노출하지 않는다
        private readonly UnityEvent onStatsChanged = new UnityEvent();

        private PlayerData data;
        private PlayerData.LevelData levelData;
        private HPController hpController;
        private MPController mpController;
        private Damageable damageable;

        public int AvailableStatPoints => availableStatPoints;
        public int AvailableSkillPoints => availableSkillPoints;
        /// <summary>능력치별 투자 포인트 (읽기용. 세이브가 그대로 저장한다).</summary>
        public int[] AllocatedPoints => allocatedPoints;

        // ───────────────────────── 최종 능력치 ─────────────────────────

        public float MaxHP => levelData == null
            ? 0f
            : levelData.maxHP + allocatedPoints[(int)StatType.MaxHP] * data.maxHPPerStatPoint + equipmentMaxHP;

        public float MaxMP => levelData == null ? 0f : levelData.maxMP;

        public float Attack => levelData == null
            ? 0f
            : levelData.attack + allocatedPoints[(int)StatType.Attack] * data.attackPerStatPoint
              + equipmentAttack + buffAttack;

        public float Defense => levelData == null
            ? 0f
            : levelData.defense + allocatedPoints[(int)StatType.Defense] * data.defensePerStatPoint
              + equipmentDefense + buffDefense;

        /// <summary>치명타 확률(%). 100%를 넘지 않는다.</summary>
        public float CritRate => levelData == null
            ? 0f
            : Mathf.Clamp(levelData.critRate + allocatedPoints[(int)StatType.CritRate] * data.critRatePerStatPoint
                          + equipmentCritRate + buffCritRate, 0f, 100f);

        /// <summary>치명타 피해 배율(%). 150이면 1.5배.</summary>
        public float CritDamage => data == null ? 100f : data.baseCritDamage + equipmentCritDamage;

        /// <summary>스킬 쿨타임 감소율(%).</summary>
        public float CooldownReduction => Mathf.Clamp(equipmentCooldownReduction, 0f, MaxCooldownReduction);
        /// <summary>스킬 쿨타임에 곱할 배율 (감소 20%면 0.8).</summary>
        public float CooldownMultiplier => 1f - CooldownReduction / 100f;

        /// <summary>대쉬 쿨타임 감소율(%). 스킬 쿨타임과 별개로 합산한다.</summary>
        public float DashCooldownReduction => Mathf.Clamp(equipmentDashCooldownReduction, 0f, MaxCooldownReduction);
        public float DashCooldownMultiplier => 1f - DashCooldownReduction / 100f;

        public float MoveSpeedMultiplier => 1f + Mathf.Clamp(equipmentMoveSpeed, 0f, MaxMoveSpeedBonus) / 100f;
        public float AttackSpeedMultiplier => 1f + Mathf.Clamp(equipmentAttackSpeed, 0f, MaxAttackSpeedBonus) / 100f;

        private void Awake()
        {
            data = DataManager.Instance.playerData;

            hpController = GetComponentInChildren<HPController>();
            mpController = GetComponentInChildren<MPController>();
            damageable = GetComponentInChildren<Damageable>();

            SetLevel(1);
        }

        private void Start()
        {
            // 장비 보정까지 합친 최대치로 채운다.
            // Awake 시점에는 장비 보정이 아직 없어서, 기본 갑옷(최대 HP +30)을 입고 시작하면 200/230으로 시작하게 된다
            RefillToMax();
        }

        public void SubscribeOnStatsChanged(UnityAction listener)
        {
            onStatsChanged.AddListener(listener);
        }

        /// <summary>
        /// 현재 최대치로 HP/MP를 가득 채운다. 게임 시작이나 세이브 불러오기 직후에만 부른다.
        /// PlayerEquipment.Start와 이 Start 중 어느 쪽이 먼저 돌든 마지막에 한 번은 가득 차도록 양쪽에서 부른다.
        /// </summary>
        public void RefillToMax()
        {
            ApplyToControllers(true);
        }

        /// <summary>레벨 기본값을 바꾼다 (레벨업·세이브 불러오기). 레벨이 바뀌면 HP/MP를 가득 채운다.</summary>
        public void SetLevel(int level)
        {
            int index = Mathf.Clamp(level, 1, data.levels.Count) - 1;
            levelData = data.levels[index];

            ApplyToControllers(true);
        }

        // ───────────────────────── 포인트 ─────────────────────────

        /// <summary>레벨업 보상 포인트를 준다 (오른 레벨 수 x 레벨당 스킬·스탯 포인트).</summary>
        public void GrantLevelUpPoints(int levelCount)
        {
            if (levelCount <= 0)
            {
                return;
            }

            availableSkillPoints += data.skillPointPerLevel * levelCount;
            availableStatPoints += data.statPointPerLevel * levelCount;
            onStatsChanged.Invoke();
        }

        /// <summary>스탯 포인트만 준다 (테스트 도구용).</summary>
        public void GrantStatPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            availableStatPoints += amount;
            onStatsChanged.Invoke();
        }

        /// <summary>스킬 포인트만 준다 (튜토리얼 퀘스트 보너스·테스트 도구).</summary>
        public void GrantSkillPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            availableSkillPoints += amount;
            onStatsChanged.Invoke();
        }

        /// <summary>스탯 포인트 1개를 투자한다. 남은 포인트가 없으면 false.</summary>
        public bool TryAllocateStatPoint(StatType statType)
        {
            if (availableStatPoints <= 0 || statType == StatType.Length)
            {
                return false;
            }

            availableStatPoints--;
            allocatedPoints[(int)statType]++;

            // 최대 HP가 올라도 현재 HP는 그대로 둔다 (포인트 분배로 체력을 회복하는 꼼수를 막는다)
            ApplyToControllers(false);
            return true;
        }

        /// <summary>투자한 스탯 포인트를 모두 돌려받는다 (비용 없음).</summary>
        public void ResetStatPoints()
        {
            for (int ix = 0; ix < allocatedPoints.Length; ++ix)
            {
                availableStatPoints += allocatedPoints[ix];
                allocatedPoints[ix] = 0;
            }

            ApplyToControllers(false);
        }

        /// <summary>스킬 포인트를 쓴다 (스킬 습득·레벨업). 모자라면 false.</summary>
        public bool TryConsumeSkillPoint(int amount = 1)
        {
            if (availableSkillPoints < amount)
            {
                return false;
            }

            availableSkillPoints -= amount;
            onStatsChanged.Invoke();
            return true;
        }

        // ───────────────────────── 장비 ─────────────────────────

        /// <summary>
        /// 장착한 장비 세 부위의 보정 합계를 넣는다 (PlayerEquipment가 장비가 바뀔 때마다 부른다).
        /// 스탯 포인트 분배와 달리, 장비는 벗으면 그만큼 아프고 입으면 그만큼 편해지는 게 자연스러우므로
        /// 최대 HP가 변한 만큼 현재 HP도 같이 움직인다.
        /// </summary>
        public void SetEquipmentBonus(float attack, float defense, float maxHP,
            float critRate, float critDamage, float cooldownReduction = 0f, float dashCooldownReduction = 0f,
            float moveSpeed = 0f, float attackSpeed = 0f)
        {
            equipmentAttack = attack;
            equipmentDefense = defense;
            equipmentMaxHP = maxHP;
            equipmentCritRate = critRate;
            equipmentCritDamage = critDamage;
            equipmentCooldownReduction = cooldownReduction;
            equipmentDashCooldownReduction = dashCooldownReduction;
            equipmentMoveSpeed = moveSpeed;
            equipmentAttackSpeed = attackSpeed;

            if (hpController != null)
            {
                hpController.SetMaxHPWithDelta(MaxHP);
            }

            if (mpController != null)
            {
                mpController.SetMaxMP(MaxMP, false);
            }

            if (damageable != null)
            {
                damageable.SetDefense(Defense);
            }

            onStatsChanged.Invoke();
        }

        // ───────────────────────── 버프 ─────────────────────────

        /// <summary>
        /// duration초 동안 능력치를 amount만큼 올린다. 같은 능력치에 다시 걸면 앞의 버프를 지우고 새로 시작한다.
        /// 최대 HP는 일시 버프 대상이 아니다 (Attack·Defense·CritRate만).
        /// </summary>
        public void ApplyTemporaryBonus(StatType statType, float amount, float duration)
        {
            if (statType == StatType.Length)
            {
                return;
            }

            int index = (int)statType;
            if (buffRoutines[index] != null)
            {
                StopCoroutine(buffRoutines[index]);
                SetBuffValue(statType, 0f);
            }

            buffAmounts[index] = amount;
            buffEndTimes[index] = Time.time + duration;
            buffRoutines[index] = StartCoroutine(TemporaryBonusRoutine(statType, amount, duration));
        }

        /// <summary>이 능력치에 일시 버프가 걸려 있으면 보정량과 남은 시간(초)을 돌려준다 (버프 HUD용).</summary>
        public bool TryGetActiveBuff(StatType statType, out float amount, out float remaining)
        {
            amount = 0f;
            remaining = 0f;

            if (statType == StatType.Length || buffRoutines[(int)statType] == null)
            {
                return false;
            }

            amount = buffAmounts[(int)statType];
            remaining = Mathf.Max(0f, buffEndTimes[(int)statType] - Time.time);
            return true;
        }

        private IEnumerator TemporaryBonusRoutine(StatType statType, float amount, float duration)
        {
            SetBuffValue(statType, amount);
            ApplyToControllers(false);

            yield return new WaitForSeconds(duration);

            SetBuffValue(statType, 0f);
            ApplyToControllers(false);
            buffRoutines[(int)statType] = null;
        }

        private void SetBuffValue(StatType statType, float amount)
        {
            switch (statType)
            {
                case StatType.Attack: buffAttack = amount; break;
                case StatType.Defense: buffDefense = amount; break;
                case StatType.CritRate: buffCritRate = amount; break;
            }
        }

        // ───────────────────────── 피해 계산 ─────────────────────────

        /// <summary>
        /// 기본 공격 한 번의 피해. 최종 공격력에 치명타 판정을 적용한다 (Weapon이 부른다). forceCritical이면 확률과 상관없이 치명타다 (3타 찌르기).
        /// 방어력에 따른 감소는 맞는 쪽(Damageable)이 처리한다.
        /// </summary>
        public float CalculateOutgoingDamage(out bool isCritical, bool forceCritical = false)
        {
            float damage = Attack;

            isCritical = forceCritical || Random.Range(0f, 100f) < CritRate;
            if (isCritical)
            {
                damage *= CritDamage / 100f;
            }

            return damage;
        }

        /// <summary>
        /// 이미 계산된 피해(스킬 등)에 치명타 판정을 적용한다. 확률은 CritRate, 배율은 CritDamage로 기본 공격과 같다.
        /// </summary>
        public float ApplyCritical(float damage, out bool isCritical)
        {
            isCritical = Random.Range(0f, 100f) < CritRate;
            return isCritical ? damage * CritDamage / 100f : damage;
        }

        // ───────────────────────── 반영 ─────────────────────────

        // 최종 능력치를 실제 컴포넌트에 넣는다.
        // refill이 true면 HP/MP를 가득 채우고, false면 현재 값은 그대로 둔 채 최대치만 바꾼다
        private void ApplyToControllers(bool refill)
        {
            if (hpController != null)
            {
                hpController.SetMaxHP(MaxHP, refill);
            }

            if (mpController != null)
            {
                mpController.SetMaxMP(MaxMP, refill);
            }

            // 방어력은 Damageable 한 곳에서만 처리한다
            if (damageable != null)
            {
                damageable.SetDefense(Defense);
            }

            onStatsChanged.Invoke();
        }

        // ───────────────────────── 세이브 ─────────────────────────

        /// <summary>세이브에서 포인트 정보를 되살리고 HP/MP를 가득 채운다 (불러오기 전용).</summary>
        public void LoadState(int savedStatPoints, int savedSkillPoints, int[] savedAllocation)
        {
            availableStatPoints = Mathf.Max(0, savedStatPoints);
            availableSkillPoints = Mathf.Max(0, savedSkillPoints);

            for (int ix = 0; ix < allocatedPoints.Length; ++ix)
            {
                allocatedPoints[ix] = savedAllocation != null && ix < savedAllocation.Length
                    ? Mathf.Max(0, savedAllocation[ix])
                    : 0;
            }

            ApplyToControllers(true);
        }
    }
}
