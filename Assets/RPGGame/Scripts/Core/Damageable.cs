using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 피해를 받을 수 있는 대상(플레이어·몬스터·보스)에 붙는 "피해 입구" 컴포넌트.
    ///
    /// 모든 공격은 이 컴포넌트를 거쳐 들어오고, 여기서 다음을 한 번에 처리한다.
    ///  1. 짧은 무적 시간: 한 번의 휘두르기가 여러 콜라이더에 겹쳐 맞아도 피해가 한 번만 들어가게 한다
    ///  2. 방어력 반영: DamageFormula로 최종 피해를 계산한다 (HPController는 계산된 값을 그대로 깎기만 한다)
    ///  3. 결과 알림: OnDamageReceived 이벤트로 HPController, 데미지 숫자, 피격음, 카메라 흔들림 등에 알린다
    ///
    /// 피해 종류별 입구
    ///  - ReceiveDamage / ReceiveHit : 일반 타격. 무적 시간이 적용된다
    ///  - ReceiveDamageOverTime     : 장판·화상 같은 지속 피해. 무적 시간을 보지도 켜지도 않는다
    ///  - ReceiveFixedDamage        : 방어력을 무시하는 고정 피해 (현재 체력 비율 피해 등)
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        [Header("피격 설정")]
        [Tooltip("한 번 맞은 뒤 다음 피해를 받지 않는 시간(초). 한 번의 공격이 여러 번 들어가는 것을 막는다")]
        [SerializeField] private float invulnerableTime = 0.2f;

        [Header("이벤트")]
        [Tooltip("피해가 실제로 들어갔을 때 방어력이 반영된 최종 피해량을 알린다 (HPController.OnDamaged, 데미지 숫자, 피격음 등)")]
        [SerializeField] private UnityEvent<float> OnDamageReceived;

        // 장비·레벨이 정한 방어력. PlayerStats / MonsterStateManager / BossStateManager가 SetDefense로 넣어 준다
        private float defense;

        // 이 시각(Time.time) 전까지는 일반 타격을 무시한다.
        // 예전에는 Update에서 매 프레임 무적 해제 여부를 검사했지만, 맞을 때만 시각을 비교하면
        // 모든 몬스터의 Update를 없앨 수 있다
        private float invulnerableUntil = float.NegativeInfinity;

        // 피해가 실제로 들어갔을 때 (공격 위치, 밀려날 거리)를 알리는 이벤트.
        // 코드에서만 구독하므로(MonsterKnockback) 직렬화하지 않고 처음 구독할 때 만든다
        private UnityEvent<Vector3, float> onKnockback;

        /// <summary>
        /// 가장 최근에 받은 타격이 치명타였는지. OnDamageReceived가 불리는 시점에 이미 정해져 있어
        /// 데미지 숫자 같은 구독자가 이벤트 안에서 읽어 색과 크기를 바꿀 수 있다.
        /// </summary>
        public bool LastHitCritical { get; private set; }

        /// <summary>
        /// 컷씬처럼 어떤 피해도 받으면 안 되는 구간에서 켠다.
        /// 짧은 무적 시간과 달리 직접 끌 때까지 유지된다.
        /// </summary>
        public bool IsImmune { get; set; }

        // 같은 대상의 체력. 사망 처리된 뒤에는 타격을 받지 않기 위해 처음 쓸 때 찾아 둔다
        private HPController hpController;
        private bool hpSearched;

        /// <summary>체력이 0이 되어 이미 사망 처리된 대상인지.</summary>
        public bool IsDead
        {
            get
            {
                if (!hpSearched)
                {
                    hpSearched = true;
                    hpController = GetComponentInParent<HPController>();
                    if (hpController == null)
                    {
                        hpController = transform.root.GetComponentInChildren<HPController>();
                    }
                }

                return hpController != null && hpController.IsDead;
            }
        }

        /// <summary>방어력을 설정한다. 스탯이 바뀔 때마다 호출된다.</summary>
        public void SetDefense(float value)
        {
            defense = value;
        }

        /// <summary>일반 타격을 받는다 (넉백·치명타 없음). 무적 시간이면 무시된다.</summary>
        public void ReceiveDamage(float damageAmount)
        {
            TryReceiveDamage(damageAmount, false);
        }

        /// <summary>
        /// 넉백·치명타 정보를 담은 타격을 받는다.
        /// 피해가 실제로 들어갔는지(무적 시간에 막히지 않았는지)를 돌려주므로,
        /// 호출한 쪽은 그 결과로 히트스톱·효과음·상태 이상(화상·둔화) 같은 추가 효과를 걸지 정한다.
        /// </summary>
        /// <param name="damageAmount">방어력 반영 전 피해량</param>
        /// <param name="hitOrigin">공격이 날아온 위치 (넉백 방향 계산용)</param>
        /// <param name="knockbackDistance">밀려날 거리. 0이면 넉백 없음</param>
        /// <param name="isCritical">치명타 여부 (데미지 숫자 표시용)</param>
        /// <returns>피해가 실제로 들어갔으면 true</returns>
        public bool ReceiveHit(float damageAmount, Vector3 hitOrigin, float knockbackDistance, bool isCritical)
        {
            if (!TryReceiveDamage(damageAmount, isCritical))
            {
                return false;
            }

            // 무적 시간에 막힌 타격은 위에서 이미 돌아갔으므로, 넉백은 실제로 맞은 경우에만 일어난다
            if (knockbackDistance > 0f)
            {
                onKnockback?.Invoke(hitOrigin, knockbackDistance);
            }

            return true;
        }

        /// <summary>
        /// 장판·화상 같은 지속 피해를 받는다. 무적 시간을 보지도, 켜지도 않는다.
        /// 일반 타격 경로를 쓰면 1초마다 들어오는 틱이 직전 타격의 무적 시간에 걸려 사라지거나,
        /// 반대로 틱이 무적을 켜서 곧이어 들어오는 실제 타격을 막아 버리기 때문이다.
        /// </summary>
        public void ReceiveDamageOverTime(float damageAmount)
        {
            if (IsImmune || IsDead)
            {
                return;
            }

            LastHitCritical = false;
            OnDamageReceived?.Invoke(DamageFormula.Apply(damageAmount, defense));
        }

        /// <summary>
        /// 방어력을 무시하고 정해진 양을 그대로 입힌다. "현재 체력의 50%" 같은 비율 피해가
        /// 방어력 때문에 그보다 적게 들어가면 안 되는 경우에 쓴다. 무적 시간은 보지도 켜지도 않는다.
        /// </summary>
        public void ReceiveFixedDamage(float damageAmount)
        {
            if (IsImmune || IsDead || damageAmount <= 0f)
            {
                return;
            }

            LastHitCritical = false;
            OnDamageReceived?.Invoke(damageAmount);
        }

        /// <summary>피해가 실제로 들어갔을 때의 넉백 알림을 코드에서 구독한다.</summary>
        public void SubscribeOnKnockback(UnityAction<Vector3, float> listener)
        {
            onKnockback ??= new UnityEvent<Vector3, float>();
            onKnockback.AddListener(listener);
        }

        /// <summary>
        /// 피격 이벤트를 코드에서 구독한다.
        /// 인스펙터 배선을 늘리지 않고도 스테이트 매니저가 피격을 알 수 있게 한다.
        /// </summary>
        public void SubscribeOnDamageReceived(UnityAction<float> listener)
        {
            OnDamageReceived ??= new UnityEvent<float>();
            OnDamageReceived.AddListener(listener);
        }

        // 일반 타격 공통 처리. 피해가 들어갔으면 true
        private bool TryReceiveDamage(float damageAmount, bool isCritical)
        {
            // 컷씬 면역이거나 직전 타격의 무적 시간 안이면 무시한다
            // 컷씬 면역이거나, 이미 죽었거나, 직전 타격의 무적 시간 안이면 무시한다.
            // 죽은 대상을 계속 때려도 피해·사망 이벤트·피격 연출이 다시 나가지 않는다
            if (IsImmune || IsDead || Time.time < invulnerableUntil)
            {
                return false;
            }

            LastHitCritical = isCritical;
            invulnerableUntil = Time.time + invulnerableTime;

            // 방어력은 여기 한 곳에서만 반영한다. 예전에는 HPController에서 한 번 더 빼는 구조라
            // 데미지 숫자에 뜨는 값과 실제로 깎이는 체력이 서로 달랐다
            OnDamageReceived?.Invoke(DamageFormula.Apply(damageAmount, defense));

            return true;
        }
    }
}
