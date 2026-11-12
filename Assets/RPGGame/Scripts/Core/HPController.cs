using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 체력(HP)을 들고 있고, 피해·회복·사망을 이벤트로 알리는 컴포넌트.
    /// 플레이어·몬스터·보스가 같은 컴포넌트를 쓴다.
    ///
    /// 체력 수치는 인스펙터에서 정하지 않는다. 플레이어는 PlayerStats, 몬스터와 보스는 레벨 데이터가
    /// SetMaxHP로 넣어 준다. 피해량은 Damageable이 방어력까지 반영해 넘겨주므로 여기서는 그대로 깎기만 한다.
    /// </summary>
    public class HPController : MonoBehaviour
    {
        [Header("이벤트")]
        [Tooltip("체력이 바뀔 때 (현재 체력, 최대 체력)을 알린다 (체력바 갱신)")]
        [SerializeField] private UnityEvent<float, float> OnHPChanged = new UnityEvent<float, float>();

        [Tooltip("체력이 0이 되어 죽었을 때 알린다 (사망 연출, 콜라이더 끄기, 퀘스트 처치 집계 등)")]
        [SerializeField] private UnityEvent OnDead = new UnityEvent();

        [Tooltip("실제로 회복된 양을 알린다 (회복 연출). 체력 변화 이벤트로는 피해와 회복을 구분할 수 없어 따로 둔다")]
        [SerializeField] private UnityEvent<float> OnHealedEvent = new UnityEvent<float>();

        private float currentHP;
        private float maxHP;

        /// <summary>
        /// 체력이 0이 되는 순간 사망 대신 다른 처리를 끼워 넣는 훅 (장비의 "사망 시 1회 부활" 등).
        /// true를 돌려주면 사망을 취소한 것으로 보고 OnDead를 발행하지 않는다. 그 전에 체력을 채워 둬야 한다.
        /// </summary>
        public System.Func<bool> DeathInterceptor;

        // 이미 사망 처리된 상태. 시체를 계속 때려도 사망 이벤트(대사·사망음·처치 집계)가 다시 나가지 않게 막는다.
        // 체력이 다시 0보다 커지면(부활·리셋·회복) 자동으로 풀린다
        private bool isDead;

        /// <summary>체력이 0이 되어 사망 처리된 상태인지. Damageable이 이 값을 보고 더 이상 타격을 받지 않는다.</summary>
        public bool IsDead => isDead;

        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;

        /// <summary>남은 체력 비율(0~1). 보스 페이즈 전환·체력바 표시에 쓴다.</summary>
        public float HPRatio => maxHP > 0f ? currentHP / maxHP : 0f;

        /// <summary>최대 체력을 정하고 체력을 가득 채운다 (몬스터 생성, 보스 리셋 등).</summary>
        public void SetMaxHP(float value)
        {
            SetMaxHP(value, true);
        }

        /// <summary>
        /// 최대 체력을 정한다. refill이 false면 현재 체력은 그대로 두고 새 최대치 안으로만 맞춘다.
        /// 스탯 포인트 분배처럼 전투 도중 최대 체력이 오르는 경우 체력을 가득 채우면 안 되기 때문이다.
        /// </summary>
        public void SetMaxHP(float value, bool refill)
        {
            maxHP = value;
            currentHP = refill ? maxHP : Mathf.Clamp(currentHP, 0f, maxHP);
            RefreshDeadFlag();

            OnHPChanged.Invoke(currentHP, maxHP);
        }

        /// <summary>
        /// 장비 착용/해제처럼 최대 체력이 바뀐 만큼 현재 체력도 함께 움직여야 하는 경우에 쓴다.
        /// SetMaxHP(값, false)는 범위 안으로 자르기만 해서, 최대치를 낮췄다가 다시 올려도 현재 체력이 돌아오지 않는다.
        /// 장비는 벗으면 그만큼 체력이 줄고, 입으면 그만큼 늘어나야 자연스럽다.
        /// </summary>
        public void SetMaxHPWithDelta(float newMaxHP)
        {
            float delta = newMaxHP - maxHP;
            maxHP = newMaxHP;
            currentHP = Mathf.Clamp(currentHP + delta, 0f, maxHP);
            RefreshDeadFlag();

            OnHPChanged.Invoke(currentHP, maxHP);
        }

        /// <summary>
        /// 현재 체력을 지정한 값으로 직접 맞춘다 (테스트 도구, 보스 시작 체력 조절용).
        /// 피해·회복 연출 없이 값만 바꾸며, 0으로 만들면 사망 이벤트까지 발행한다.
        /// </summary>
        public void SetCurrentHP(float value)
        {
            currentHP = Mathf.Clamp(value, 0f, maxHP);
            RefreshDeadFlag();

            OnHPChanged.Invoke(currentHP, maxHP);

            if (currentHP <= 0f && !isDead)
            {
                isDead = true;
                OnDead.Invoke();
            }
        }

        /// <summary>
        /// 체력을 회복한다 (물약, 보스 긴급 회복, 부활 등).
        /// 최대 체력에 막혀 실제로는 덜 회복될 수 있으므로 회복 전후 차이만큼만 회복 이벤트를 보낸다.
        /// 가득 찬 상태에서 물약을 써도 "+0"이 뜨지 않게 하기 위함이다.
        /// </summary>
        public void OnHealed(float hpAmount)
        {
            float before = currentHP;
            currentHP = Mathf.Clamp(currentHP + hpAmount, 0f, maxHP);
            RefreshDeadFlag();

            OnHPChanged.Invoke(currentHP, maxHP);

            float healed = currentHP - before;
            if (healed > 0f)
            {
                OnHealedEvent.Invoke(healed);
            }
        }

        /// <summary>
        /// 피해를 받는다. Damageable.OnDamageReceived에 인스펙터로 연결돼 있으므로 이름과 매개변수를 바꾸지 않는다.
        /// 넘어오는 값은 Damageable에서 방어력까지 반영된 최종 피해량이다.
        /// </summary>
        public void OnDamaged(float damage)
        {
            // 이미 죽은 대상은 더 이상 피해도 사망 이벤트도 받지 않는다
            if (isDead)
            {
                return;
            }

            currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damage));
            OnHPChanged.Invoke(currentHP, maxHP);

            if (currentHP > 0f)
            {
                return;
            }

            // 부활 장비처럼 사망을 가로채는 처리가 있으면 그쪽에 먼저 기회를 준다
            if (DeathInterceptor != null && DeathInterceptor())
            {
                return;
            }

            isDead = true;
            OnDead.Invoke();
        }

        // 체력이 남아 있으면 사망 상태를 푼다 (부활·보스 리셋·회복)
        private void RefreshDeadFlag()
        {
            if (currentHP > 0f)
            {
                isDead = false;
            }
        }

        /// <summary>즉시 사망시킨다 (테스트 도구의 처치 버튼 등).</summary>
        public void Die()
        {
            if (isDead)
            {
                return;
            }

            currentHP = 0f;
            isDead = true;
            OnHPChanged.Invoke(currentHP, maxHP);
            OnDead.Invoke();
        }

        /// <summary>회복 이벤트를 코드에서 구독한다 (회복 연출).</summary>
        public void SubscribeOnHealed(UnityAction<float> listener)
        {
            OnHealedEvent.AddListener(listener);
        }

        /// <summary>사망 이벤트를 코드에서 구독한다.</summary>
        public void SubscribeOnDead(UnityAction listener)
        {
            OnDead.AddListener(listener);
        }

        /// <summary>
        /// 사망 이벤트 구독을 해제한다. 코드로 붙인 리스너는 구독한 쪽이 먼저 파괴돼도 자동으로 빠지지 않으므로
        /// (플레이어 사망을 듣는 보스, 화상 효과 등) 파괴될 때 직접 해제해야 한다.
        /// </summary>
        public void UnsubscribeOnDead(UnityAction listener)
        {
            OnDead.RemoveListener(listener);
        }
    }
}
