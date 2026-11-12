using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스 공격 패턴 하나를 나타내는 데이터.
    /// SkillData와 같은 방식으로, 실제 효과는 하위 클래스가 Execute에서 구현한다.
    /// 새 패턴은 이 클래스를 상속한 스크립트 + 애셋을 추가하면 되고 보스 코드는 건드리지 않는다
    /// </summary>
    public abstract class BossPatternData : ScriptableObject
    {
        public string patternName;
        [TextArea(2, 4)] public string description;

        [Header("사용 조건")]
        [Tooltip("이 패턴을 쓸 수 있는 최소/최대 거리")]
        public float minRange = 0f;
        public float maxRange = 6f;
        [Tooltip("같은 패턴을 다시 쓰기까지의 대기 시간")]
        public float cooldown = 6f;

        [Header("연출")]
        [Tooltip("재생할 애니메이터 트리거 이름")]
        public string animationTrigger = "Pattern";
        [Tooltip("예고 표시가 뜬 뒤 이 시간이 지나서야 공격 모션을 시작한다. 예고를 길게 늘려도 타격 순간이 모션의 타격 프레임과 맞도록, (예고 시간 - 모션이 타격에 닿는 시간)으로 정한다")]
        public float animationDelay = 0f;
        [Tooltip("패턴이 끝날 때까지 다른 행동을 막는 시간")]
        public float duration = 1.5f;
        [Tooltip("패턴 시작 시 생성할 이펙트")]
        public GameObject effectPrefab;

        [Header("사운드")]
        [Tooltip("스킬이 실제로 나가는 순간(예고·준비 동작이 끝난 시점)에 재생할 소리. 여러 개면 무작위 하나, 비우면 소리가 없다. 준비 중에는 재생하지 않으므로 시점은 각 패턴의 Execute가 PlaySound를 부르는 위치로 정해진다")]
        public AudioClip[] sounds;
        // 이 패턴 소리의 음량 보정 (BossData.soundVolume에 곱해진다). 파일마다 원래 크기가 달라서 패턴별로 따로 둔다
        [Range(0f, 2f)] public float soundVolume = 1f;

        [Header("피해")]
        [Tooltip("보스 공격력에 곱해지는 배율")]
        public float damageMultiplier = 1f;

        // 바닥에 넓은 예고·장판을 깔아 두는 패턴인지. 이런 패턴이 연달아 나오면 예고 판이 겹쳐 어디가 위험한지
        // 읽을 수 없으므로, 보스는 이런 패턴 바로 다음에는 같은 종류를 고르지 않는다 (BossStateManager.PickPattern)
        public virtual bool IsGroundZone => false;

        // 현재 거리에서 이 패턴을 사용할 수 있는지 확인하는 함수.
        // 순간이동처럼 멀리서도 써야 하는 패턴이 사거리 조건을 바꿀 수 있게 virtual로 둔다
        public virtual bool IsUsableAt(float distance)
        {
            return distance >= minRange && distance <= maxRange;
        }

        // 이 패턴을 쓸 수 있는 가장 먼 거리. 보스가 추격을 포기하는 거리를 정할 때 쓴다
        public virtual float MaxUsableRange => maxRange;

        // 패턴의 실제 효과를 적용하는 함수. 시전 시작 시점에 호출된다
        public abstract void Execute(BossPatternContext context);

        // 범위 안의 대상에게 피해를 주는 공용 헬퍼
        protected static void DamageInSphere(Vector3 center, float radius, LayerMask targetLayer, float damage)
        {
            Collider[] colliders = Physics.OverlapSphere(center, radius, targetLayer);

            foreach (Collider collider in colliders)
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable != null)
                {
                    damageable.ReceiveDamage(damage);
                }
            }
        }

        // 패턴 소리를 position에서 재생하는 공용 헬퍼. 스킬이 실제로 나가는 시점에 하위 클래스가 부른다.
        // 최종 음량 = BossData.soundVolume x 이 패턴의 soundVolume (BossSfx가 1을 넘지 않게 자른다)
        protected void PlaySound(BossPatternContext context, Vector3 position)
        {
            if (sounds == null || sounds.Length == 0)
            {
                return;
            }

            BossStateManager boss = context.Caster as BossStateManager;
            float bossVolume = boss != null && boss.Data != null ? boss.Data.soundVolume : 1f;

            BossSfx.Play(sounds, position, bossVolume * soundVolume);
        }

        // 이펙트를 생성하는 공용 헬퍼. 일정 시간 뒤 자동으로 사라진다.
        // 베기/투사체처럼 방향이 있는 연출은 보스가 바라보는 쪽을 향해야 하므로 회전을 함께 받는다
        protected static void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifeTime = 5f)
        {
            if (prefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(prefab, position, rotation);
            Destroy(effect, lifeTime);
        }
    }
}
