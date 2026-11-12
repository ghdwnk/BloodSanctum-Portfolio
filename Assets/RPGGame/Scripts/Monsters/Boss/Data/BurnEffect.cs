using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 대상에게 붙어 일정 간격으로 화상 피해를 주는 상태 이상.
    /// 운석을 여러 발 맞아도 겹쳐서 쌓이지 않고, 남은 시간만 새로 채워진다 (한 번에 받는 화상 피해가 폭증하지 않게)
    /// </summary>
    public class BurnEffect : MonoBehaviour
    {
        private static readonly List<BurnEffect> active = new List<BurnEffect>();

        // 불타는 동안 플레이어 닉네임 위에 계속 띄우는 문구
        private const string StatusKey = "burn";
        private const string StatusText = "화상피해!";
        private static readonly Color StatusColor = new Color(1f, 0.25f, 0.2f, 1f);

        private Damageable target;
        private UIPlayerNameTag nameTag;
        private HPController hp;
        private float tickDamage;
        private float tickInterval;
        private float remaining;
        private float tickTimer;
        private GameObject effectInstance;

        /// <summary>대상에게 화상을 건다. 이미 타고 있으면 지속 시간과 피해량을 새 값으로 갱신한다.</summary>
        public static void Apply(Damageable target, float tickDamage, float duration, float tickInterval, GameObject effectPrefab)
        {
            if (target == null || duration <= 0f)
            {
                return;
            }

            BurnEffect burn = target.GetComponent<BurnEffect>();
            if (burn == null)
            {
                burn = target.gameObject.AddComponent<BurnEffect>();
                burn.target = target;
                burn.hp = target.GetComponent<HPController>();
                burn.tickTimer = 0f;

                // 죽으면 화상도 꺼야 한다. 그대로 두면 마을에서 부활한 뒤에도 계속 타게 된다
                if (burn.hp != null)
                {
                    burn.hp.SubscribeOnDead(burn.Extinguish);
                }

                if (effectPrefab != null)
                {
                    burn.effectInstance = Instantiate(effectPrefab, target.transform);
                    burn.effectInstance.transform.localPosition = Vector3.zero;
                }

                // 이름표는 플레이어 루트(PlayerStateManager) 아래에 붙어 있다. 플레이어가 아닌 대상은 이름표가 없어 문구를 띄우지 않는다
                PlayerStateManager player = target.GetComponentInParent<PlayerStateManager>();
                if (player != null)
                {
                    burn.nameTag = player.GetComponentInChildren<UIPlayerNameTag>(true);
                    if (burn.nameTag != null)
                    {
                        burn.nameTag.SetStatus(StatusKey, StatusText, StatusColor);
                    }
                }

                active.Add(burn);
            }

            burn.tickDamage = tickDamage;
            burn.tickInterval = Mathf.Max(0.05f, tickInterval);
            burn.remaining = duration;
        }

        /// <summary>모든 화상을 끈다 (보스 사망·전투 리셋 때).</summary>
        public static void ClearAll()
        {
            for (int ix = active.Count - 1; ix >= 0; --ix)
            {
                if (active[ix] != null)
                {
                    Destroy(active[ix]);
                }
            }

            active.Clear();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;

            while (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                target.ReceiveDamageOverTime(tickDamage);
            }

            if (remaining <= 0f)
            {
                Destroy(this);
            }
        }

        private void Extinguish()
        {
            Destroy(this);
        }

        private void OnDestroy()
        {
            active.Remove(this);

            if (hp != null)
            {
                hp.UnsubscribeOnDead(Extinguish);
            }

            if (effectInstance != null)
            {
                Destroy(effectInstance);
            }

            if (nameTag != null)
            {
                nameTag.ClearStatus(StatusKey);
            }
        }
    }
}
