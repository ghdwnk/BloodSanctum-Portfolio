using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 플레이어의 MP를 들고 있는 컴포넌트. 스킬 사용 시 소모하고, 기본 공격 적중과 자연 회복으로 채워진다.
    /// 최대 MP는 PlayerStats가 레벨에 맞춰 SetMaxMP로 넣어 준다.
    /// </summary>
    public class MPController : MonoBehaviour
    {
        [Header("자연 회복")]
        [Tooltip("초당 자연 회복되는 MP. 0이면 자연 회복을 쓰지 않는다. MP는 주로 기본 공격 적중으로 채우므로 작게 둔다")]
        [SerializeField] private float regenPerSecond = 0.2f;

        [Header("이벤트")]
        [Tooltip("MP가 바뀔 때 (현재 MP, 최대 MP)를 알린다 (MP 구슬 갱신)")]
        [SerializeField] private UnityEvent<float, float> OnMPChanged = new UnityEvent<float, float>();

        private float currentMP;
        private float maxMP;

        private void Update()
        {
            if (regenPerSecond <= 0f || currentMP >= maxMP)
            {
                return;
            }

            RestoreMP(regenPerSecond * Time.deltaTime);
        }

        /// <summary>
        /// 최대 MP를 정한다. refill이 false면 현재 MP는 그대로 두고 새 최대치 안으로만 맞춘다
        /// (스탯 변경처럼 전투 도중 최대 MP가 바뀌는 경우).
        /// </summary>
        public void SetMaxMP(float value, bool refill)
        {
            maxMP = value;
            currentMP = refill ? maxMP : Mathf.Clamp(currentMP, 0f, maxMP);

            OnMPChanged.Invoke(currentMP, maxMP);
        }

        /// <summary>MP를 회복한다 (자연 회복, 기본 공격 적중, 부활 등). 최대치를 넘지 않는다.</summary>
        public void RestoreMP(float mpAmount)
        {
            currentMP = Mathf.Clamp(currentMP + mpAmount, 0f, maxMP);

            OnMPChanged.Invoke(currentMP, maxMP);
        }

        /// <summary>MP를 소모한다. 모자라면 아무것도 하지 않고 false를 돌려준다.</summary>
        public bool TryConsumeMP(float mpAmount)
        {
            if (currentMP < mpAmount)
            {
                return false;
            }

            currentMP -= mpAmount;
            OnMPChanged.Invoke(currentMP, maxMP);

            return true;
        }
    }
}
