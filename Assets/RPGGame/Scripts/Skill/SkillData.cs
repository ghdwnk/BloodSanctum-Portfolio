using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 스킬 하나가 재생할 이펙트 한 개의 설정.
    /// 스킬마다 이펙트 개수가 달라서(돌진은 잔상+임팩트 2개, 버프는 1개) 배열로 들고 에셋에서 채운다.
    /// </summary>
    [System.Serializable]
    public class SkillVfx
    {
        [Tooltip("생성할 이펙트 프리팹")]
        public GameObject prefab;
        [Tooltip("시전 후 이펙트가 생기기까지의 지연(초). 애니메이션의 타격 시점에 맞춘다")]
        public float delay = 0f;
        [Tooltip("생성 후 방출을 멈추기까지의 시간(초). 0 이하면 이펙트가 스스로 사라질 때까지 둔다")]
        public float lifetime = 2f;
        [Tooltip("플레이어 기준 위치 (z가 앞쪽). 생성 순간의 위치·방향을 기준으로 계산한다")]
        public Vector3 localOffset = Vector3.zero;
        [Tooltip("켜면 플레이어의 자식으로 붙어 함께 움직인다 (버프 오라 등)")]
        public bool followCaster = false;
    }

    /// <summary>
    /// 플레이어 스킬 하나의 데이터(ScriptableObject)와 공통 동작.
    ///
    /// 실제 효과는 자식 클래스가 ApplyEffect에서 구현한다.
    ///  - DashStrikeSkillData   : 돌진 강타 (전방 돌진하며 경로의 적을 베고 밀어냄)
    ///  - LaserRainSkillData    : 비전 폭풍 (전방 장판에 지속 피해, 에셋 이름은 Earthquake Slam Skill)
    ///  - WarCrySkillData       : 분노의 함성 (공격력 버프)
    ///  - IronDefenseSkillData  : 강철 방어 (방어력 버프)
    ///  - UltimateBlastSkillData: 궁극기 화염 폭발 (큰 범위 피해 + 잔불)
    ///
    /// PlayerSkillController가 시전할 때 PlayVfx → PlaySfx → ApplyEffect 순서로 부른다.
    /// 레벨(1~maxLevel)이 오르면 위력은 커지고 마나·쿨타임은 줄어든다 (GetPowerMultiplier / GetMPCost / GetCooldown).
    /// </summary>
    public abstract class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("스킬 창·툴팁에 보이는 이름")]
        public string skillName;
        [Tooltip("툴팁 설명")]
        [TextArea(2, 5)] public string description;
        public Sprite icon;

        [Header("비용 (1레벨 기준)")]
        [Tooltip("마나 소모. 레벨이 오르면 mpCostReductionPerLevel만큼 줄어든다")]
        public float mpCost = 20f;
        [Tooltip("쿨타임(초). 레벨별 쿨타임(아래)을 채우지 않았을 때 이 값에서 시작해 cooldownReductionPerLevel만큼 줄어든다")]
        public float cooldown = 6f;
        [Tooltip("레벨별 쿨타임(초). 1레벨, 2레벨... 순서로 채우면 이 값을 그대로 쓴다 (비어 있거나 0이면 위 쿨타임과 레벨당 감소율로 계산). 레벨보다 짧게 채우면 마지막 값을 쓴다")]
        public float[] cooldownByLevel;

        [Header("스킬 레벨")]
        [Tooltip("이 스킬을 처음 배우는 데 필요한 플레이어 레벨. 1이면 제한 없음 (2레벨 이상 강화에는 레벨 제한이 없다)")]
        public int requiredPlayerLevel = 1;
        [Tooltip("최대 스킬 레벨")]
        public int maxLevel = 4;
        [Tooltip("레벨당 위력 증가율. 0.35면 4레벨에서 1레벨의 2.05배")]
        [Range(0f, 1f)] public float powerGrowthPerLevel = 0.35f;
        [Tooltip("레벨당 마나 소모 감소율. 0.1이면 레벨당 10%p씩 줄어 4레벨에서 70%")]
        [Range(0f, 0.3f)] public float mpCostReductionPerLevel = 0.1f;
        [Tooltip("레벨당 쿨타임 감소율")]
        [Range(0f, 0.3f)] public float cooldownReductionPerLevel = 0.1f;

        [Header("시전")]
        [Tooltip("시전 중 이동·공격·대쉬 입력을 막는 시간(초)")]
        public float castLockDuration = 0.5f;
        [Tooltip("재생할 애니메이터 트리거 이름 (Kachujin Animator Controller의 Skill1~Skill4)")]
        public string animationTrigger = "Skill";
        [Tooltip("켜면 기본 공격 도중에도 쓸 수 있고, 쓰는 순간 기본 공격(콤보·무기 판정)이 끊긴다")]
        public bool cancelsBasicAttack = false;

        [Header("연출")]
        [Tooltip("시전과 함께 재생할 이펙트 목록. 효과 로직과 분리돼 있어 이펙트만 바꿀 때 코드를 건드리지 않는다")]
        public SkillVfx[] vfx;
        [Tooltip("시전할 때 한 번 재생할 효과음")]
        public AudioClip castSound;
        [Tooltip("효과음 음량. 파일마다 원래 크기가 달라 비슷하게 들리도록 스킬별로 맞춘다")]
        [Range(0f, 1f)] public float castSoundVolume = 0.3f;
        [Tooltip("시전 후 소리가 나기까지의 지연(초). 이펙트가 보이는 순간에 맞출 때 쓴다")]
        public float castSoundDelay = 0f;

        /// <summary>
        /// 범위 판정(Physics.Overlap*NonAlloc)에 같이 쓰는 버퍼. 판정마다 배열을 새로 만들지 않도록 재사용한다.
        /// 몬스터 수 상한이 있어 한 번에 이보다 많이 걸리는 일은 없다
        /// </summary>
        protected static readonly Collider[] HitBuffer = new Collider[64];

        // 툴팁처럼 시전 정보가 없는 곳에서 플레이어 공격력을 읽기 위한 캐시. 씬이 바뀌어 사라졌을 때만 다시 찾는다
        private static PlayerStats cachedPlayerStats;

        // ───────────────────────── 자식 클래스가 구현 ─────────────────────────

        /// <summary>
        /// 스킬 효과를 실제로 적용한다. 시전 순간 바로 불리며, 돌진·지연처럼 시간이 걸리는 처리는
        /// context.Caster로 코루틴을 직접 시작한다.
        /// </summary>
        public abstract void ApplyEffect(SkillCastContext context);

        /// <summary>
        /// 툴팁 맨 위에 보여 줄 핵심 수치 한 줄 (예: "데미지 140", "공격력 +90% · 지속 8초").
        /// 스킬마다 수치의 뜻이 달라 자식 클래스가 덮어쓴다. 빈 문자열이면 줄을 표시하지 않는다.
        /// </summary>
        public virtual string GetEffectText(int skillLevel)
        {
            return string.Empty;
        }

        // ───────────────────────── 레벨 계산 ─────────────────────────

        /// <summary>targetLevel로 올리는 데 필요한 플레이어 레벨. 처음 배울 때(1레벨)만 제한이 있다.</summary>
        public int GetRequiredPlayerLevel(int targetLevel)
        {
            return targetLevel <= 1 ? requiredPlayerLevel : 1;
        }

        /// <summary>레벨에 따른 위력 배율 (1레벨 = 1배). 각 스킬이 자기 피해·버프 수치에 곱한다.</summary>
        public float GetPowerMultiplier(int skillLevel)
        {
            return 1f + powerGrowthPerLevel * (ClampLevel(skillLevel) - 1);
        }

        /// <summary>레벨에 따른 마나 소모. 아무리 줄어도 원래의 10%보다 낮아지지 않는다.</summary>
        public float GetMPCost(int skillLevel)
        {
            float reduction = mpCostReductionPerLevel * (ClampLevel(skillLevel) - 1);
            return mpCost * Mathf.Max(0.1f, 1f - reduction);
        }

        /// <summary>레벨에 따른 쿨타임(초). 레벨별 값이 없으면 아무리 줄어도 원래의 10%보다 짧아지지 않는다.</summary>
        public float GetCooldown(int skillLevel)
        {
            // 레벨별 쿨타임이 채워져 있으면 그 값을 그대로 쓴다 (스킬마다 레벨 간 간격을 따로 정하기 위함)
            if (cooldownByLevel != null && cooldownByLevel.Length > 0)
            {
                int index = Mathf.Min(ClampLevel(skillLevel), cooldownByLevel.Length) - 1;
                if (cooldownByLevel[index] > 0f)
                {
                    return cooldownByLevel[index];
                }
            }

            float reduction = cooldownReductionPerLevel * (ClampLevel(skillLevel) - 1);
            return cooldown * Mathf.Max(0.1f, 1f - reduction);
        }

        // 아직 배우지 않은 스킬(0레벨)도 UI에 1레벨 기준값을 보여 줘야 하므로 1 이상으로 자른다
        protected int ClampLevel(int skillLevel)
        {
            return Mathf.Clamp(skillLevel, 1, Mathf.Max(1, maxLevel));
        }

        // ───────────────────────── 피해 계산 ─────────────────────────

        /// <summary>
        /// 스킬 피해 = (고정 피해 + 플레이어 공격력 x 계수) x 레벨 배율.
        /// 공격력(장비·스탯·버프 포함)이 오르면 스킬도 함께 세지되, 고정 피해가 바닥을 받쳐 주므로
        /// 공격력 증가율만큼 그대로 비례해 폭주하지는 않는다.
        /// </summary>
        public float CalculateDamage(float flatDamage, float attackRatio, float playerAttack, int skillLevel)
        {
            return (flatDamage + playerAttack * attackRatio) * GetPowerMultiplier(skillLevel);
        }

        /// <summary>시전 정보가 없는 곳(툴팁)에서 지금 플레이어 공격력을 읽는다.</summary>
        protected static float GetCurrentPlayerAttack()
        {
            if (cachedPlayerStats == null)
            {
                cachedPlayerStats = FindAnyObjectByType<PlayerStats>();
            }

            return cachedPlayerStats != null ? cachedPlayerStats.Attack : 0f;
        }

        /// <summary>시전 정보에서 플레이어 공격력을 읽는다 (스탯이 없으면 0).</summary>
        protected static float GetCasterAttack(SkillCastContext context)
        {
            return context.PlayerStats != null ? context.PlayerStats.Attack : 0f;
        }

        /// <summary>툴팁용 "공격력 N% + M" 표기. 계수가 0이면 고정 피해만 보여 준다.</summary>
        protected static string FormatRatio(float flatDamage, float attackRatio)
        {
            return attackRatio > 0f ? $"공격력 {attackRatio * 100f:0}% + {flatDamage:0}" : $"{flatDamage:0}";
        }

        // ───────────────────────── 공통 연출 ─────────────────────────

        /// <summary>시전 효과음을 재생한다. castSoundDelay가 있으면 그만큼 기다렸다 재생한다.</summary>
        public void PlaySfx(SkillCastContext context)
        {
            if (castSound == null)
            {
                return;
            }

            if (castSoundDelay <= 0f || context == null || context.Caster == null)
            {
                SfxPlayer.Play(castSound, castSoundVolume);
                return;
            }

            context.Caster.StartCoroutine(PlaySfxRoutine());
        }

        private IEnumerator PlaySfxRoutine()
        {
            yield return new WaitForSeconds(castSoundDelay);
            SfxPlayer.Play(castSound, castSoundVolume);
        }

        /// <summary>
        /// 등록된 이펙트를 각자의 지연 시간에 맞춰 생성한다.
        /// 스킬 종류와 상관없는 공통 연출이라 자식 클래스가 아니라 여기서 처리한다.
        /// </summary>
        public void PlayVfx(SkillCastContext context)
        {
            if (vfx == null || context == null || context.Caster == null)
            {
                return;
            }

            foreach (SkillVfx entry in vfx)
            {
                if (entry != null && entry.prefab != null)
                {
                    context.Caster.StartCoroutine(SpawnVfxRoutine(entry, context));
                }
            }
        }

        private static IEnumerator SpawnVfxRoutine(SkillVfx entry, SkillCastContext context)
        {
            if (entry.delay > 0f)
            {
                yield return new WaitForSeconds(entry.delay);
            }

            Transform playerTransform = context.PlayerTransform;
            if (playerTransform == null)
            {
                yield break;
            }

            // 지연이 끝난 시점의 플레이어 위치를 기준으로 잡으므로, 돌진이 끝난 자리에 임팩트가 생긴다
            Vector3 position = playerTransform.TransformPoint(entry.localOffset);
            GameObject instance = Instantiate(entry.prefab, position, playerTransform.rotation);

            if (entry.followCaster)
            {
                instance.transform.SetParent(playerTransform, true);
            }

            if (entry.lifetime <= 0f)
            {
                yield break;
            }

            yield return new WaitForSeconds(entry.lifetime);

            if (instance == null)
            {
                yield break;
            }

            // 오라·실드처럼 계속 재생되는 이펙트를 바로 Destroy하면 파티클이 한 프레임에 사라진다.
            // 방출만 멈추고, 남은 파티클이 수명을 다한 뒤에 오브젝트를 지운다
            float tailTime = 0f;
            foreach (ParticleSystem particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                tailTime = Mathf.Max(tailTime, particle.main.startLifetime.constantMax);
            }

            Destroy(instance, tailTime);
        }

        // ───────────────────────── 자식 클래스용 공용 처리 ─────────────────────────

        /// <summary>대상에게 스킬 피해를 준다 (넉백 없음). 대상마다 따로 치명타를 굴린다.</summary>
        protected static void DamageTarget(Collider targetCollider, float damage, PlayerStats caster)
        {
            Damageable damageable = targetCollider.GetComponent<Damageable>();
            if (damageable != null)
            {
                bool isCritical = false;
                float finalDamage = caster != null ? caster.ApplyCritical(damage, out isCritical) : damage;
                damageable.ReceiveHit(finalDamage, targetCollider.transform.position, 0f, isCritical);
            }
        }

        /// <summary>대상을 origin 반대쪽(수평)으로 force만큼 밀어낸다. CharacterController가 있는 대상만 밀린다.</summary>
        protected static void ApplyKnockback(Collider targetCollider, Vector3 origin, float force)
        {
            CharacterController targetController = targetCollider.GetComponent<CharacterController>();
            if (targetController == null || !targetController.enabled)
            {
                return;
            }

            Vector3 direction = targetCollider.transform.position - origin;
            direction.y = 0f;

            // 정확히 같은 자리에 있으면 방향을 정할 수 없으므로 대상이 보는 방향으로 민다
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = targetCollider.transform.forward;
            }

            targetController.Move(direction.normalized * force);
        }
    }
}
