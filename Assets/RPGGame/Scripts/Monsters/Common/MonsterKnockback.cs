using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 맞은 몬스터를 공격 반대 방향으로 짧게 밀어내는 컴포넌트.
    /// 상태를 바꾸거나 행동을 막지 않고 위치만 살짝 이동시킨다.
    /// 그래서 맞는 동안에도 추격·공격 로직은 그대로 돌고, 전투 흐름에 지장이 없다.
    /// MonsterStateManager가 Awake에서 모든 일반 몬스터에 코드로 붙이고, Damageable의 넉백 알림을 구독한다.
    /// </summary>
    public class MonsterKnockback : MonoBehaviour
    {
        [Tooltip("밀려나는 데 걸리는 시간(초). 짧게 확 밀리고 멈춰야 찰진 느낌이 난다")]
        [SerializeField] private float duration = 0.14f;
        [Tooltip("공격 모션 중에는 밀리는 거리를 이 비율로 줄인다. 공격 중 크게 밀리면 판정이 닿지 못해 계속 때리기만 하면 몬스터가 영영 공격하지 못한다")]
        [SerializeField, Range(0f, 1f)] private float attackingRatio = 0.3f;
        [Tooltip("한 번에 밀려날 수 있는 최대 거리(m). 연타나 스킬이 겹쳐도 멀리 날아가지 않게 한다")]
        [SerializeField] private float maxDistance = 1.2f;

        private CharacterController characterController;
        private MonsterStateManager manager;
        private Vector3 direction;
        private float distance;
        private float elapsed;
        private bool active;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            manager = GetComponent<MonsterStateManager>();

            // 피격 이벤트는 자식(히트박스)에 있을 수 있어서 자식까지 찾는다
            Damageable damageable = GetComponentInChildren<Damageable>();
            if (damageable != null)
            {
                damageable.SubscribeOnKnockback(OnKnockback);
            }
        }

        private void OnKnockback(Vector3 hitOrigin, float knockbackDistance)
        {
            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            if (manager != null && manager.IsDead)
            {
                return;
            }

            Vector3 away = transform.position - hitOrigin;
            away.y = 0f;

            if (away.sqrMagnitude < 0.0001f)
            {
                away = -transform.forward;
            }

            direction = away.normalized;

            float ratio = manager != null && manager.IsAttacking ? attackingRatio : 1f;
            // 진행 중이던 넉백에 덮어쓴다 (누적하지 않으므로 연타해도 한계 거리를 넘지 않는다)
            distance = Mathf.Min(knockbackDistance * ratio, maxDistance);
            elapsed = 0f;
            active = true;
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            if (!characterController.enabled)
            {
                active = false;
                return;
            }

            float previous = Ease(elapsed / duration);
            elapsed += Time.deltaTime;
            float current = Ease(elapsed / duration);

            characterController.Move(direction * (distance * (current - previous)));

            if (elapsed >= duration)
            {
                active = false;
            }
        }

        // 처음에 빠르게 밀리고 끝으로 갈수록 느려지는 곡선 (ease-out quad)
        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
