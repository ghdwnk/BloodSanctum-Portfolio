using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 맞았을 때 머리 위에 피해량 숫자를 띄우는 연출. 최대 3줄까지 쌓아서 보여준다.
    ///  - 피해 하나당 0.5초 동안 보이고 (크게 떴다가 작아지며 사라지는 원래 연출 시간), 가장 먼저 받은 피해가 맨 아랫줄, 새 피해는 위에 쌓인다 (선입선출)
    ///  - 3줄이 이미 차 있는데 새 피해가 들어오면 가장 오래된(맨 아랫줄) 피해부터 밀려난다
    ///  - 한 번 맞으면 한 줄만, 그 시간 안에 여러 번 맞으면 최대 3줄이 함께 보인다
    /// Damageable.OnDamageReceived에 OnDamageReceived가 인스펙터로 연결돼 있다 (이름을 바꾸지 않는다).
    /// 몬스터에게 준 피해는 노랑, 치명타는 빨강(더 크게+느낌표), 플레이어가 받은 피해는 흰색.
    /// </summary>
    public class UIDamageText : MonoBehaviour
    {
        // 한 번에 보이는 최대 줄 수
        private const int MaxLines = 3;

        [SerializeField] private TMPro.TextMeshProUGUI damageText;
        [SerializeField] private float scaleMax = 1.5f;
        [SerializeField] private float scaleMin = 0.8f;
        [SerializeField] private float scaleAnimationTime = 0.5f;
        // 피해 숫자 하나가 보이는 시간(초). 에디터에 저장된 옛 값(3초)이 따라오지 않도록 직렬화하지 않고 상수로 고정한다
        private const float lineLifetime = 0.5f;
        // 사라지기 직전 서서히 투명해지는 시간(초). 0이면 투명해지지 않고 작아진 뒤 그대로 사라진다
        private const float fadeOutTime = 0f;
        [SerializeField] private float criticalScaleBonus = 1.35f;
        // 색은 씬/프리팹마다 직렬화된 값이 제각각이라 코드에서 고정한다.
        // 플레이어가 몬스터에게 준 일반 피해=노랑, 치명타=빨강, 플레이어가 받은 피해=흰색
        private static readonly Color damageColor = new Color(1f, 0.92f, 0.1f, 1f);
        private static readonly Color criticalColor = new Color(1f, 0.1f, 0.1f, 1f);
        private static readonly Color receivedColor = new Color(1f, 1f, 1f, 1f);

        // 피해 한 줄의 기록. 줄마다 받은 시각이 달라 크기 연출과 사라지는 시점이 각자 따로 간다
        private struct Entry
        {
            public int damage;
            public bool critical;
            public float spawnTime;
        }

        // 오래된 순서로 들어 있다 (0번이 가장 먼저 받은 피해 = 맨 아랫줄)
        private readonly List<Entry> entries = new List<Entry>(MaxLines);
        private readonly System.Text.StringBuilder builder = new System.Text.StringBuilder(128);
        private bool isPlayerTarget;
        private Damageable damageable;

        private void Awake()
        {
            damageText.text = string.Empty;

            // 줄이 위로 쌓이게: 맨 아래 줄을 기준선으로 두고 줄이 늘면 위로 자라게 한다. 한 줄이 접히지 않도록 줄바꿈도 끈다
            damageText.alignment = TMPro.TextAlignmentOptions.Bottom;
            damageText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            damageText.overflowMode = TMPro.TextOverflowModes.Overflow;
            damageText.richText = true;

            // 치명타 여부를 읽을 피격 대상. 텍스트는 대상의 자식이거나 그 아래에 있다
            damageable = GetComponentInParent<Damageable>();
            if (damageable == null)
            {
                // 데미지 텍스트가 피격 대상의 형제 오브젝트 아래에 있는 경우
                damageable = transform.root.GetComponentInChildren<Damageable>();
            }

            if (damageable == null)
            {
                // 플레이어 UI 캔버스의 데미지 텍스트: 피격 대상(플레이어 Damageable)이 계층 밖에 있어 따로 찾는다
                PlayerStats player = FindFirstObjectByType<PlayerStats>();
                if (player != null)
                {
                    damageable = player.GetComponentInChildren<Damageable>();
                }
            }

            // 피격 대상이 몬스터/보스가 아니면 플레이어(받은 피해). 텍스트 위치와 상관없이 대상 기준으로 판별한다
            isPlayerTarget = damageable != null
                && damageable.GetComponentInParent<MonsterStateManager>() == null
                && damageable.GetComponentInParent<BossStateManager>() == null;
        }

        public void OnDamageReceived(float damage)
        {
            // 피해량은 DamageFormula에서 이미 정수로 내림돼 오므로 정수로 바꿔 소수점 없이 찍는다
            bool isCritical = damageable != null && damageable.LastHitCritical;

            // 3줄이 차 있으면 가장 먼저 받은(맨 아랫줄) 피해부터 밀어낸다
            if (entries.Count >= MaxLines)
            {
                entries.RemoveAt(0);
            }

            entries.Add(new Entry { damage = Mathf.FloorToInt(damage), critical = isCritical, spawnTime = Time.time });
            Rebuild();
        }

        // 크기 연출(처음 scaleAnimationTime 동안)이나 사라지는 연출 중인 줄이 있을 때만 매 프레임 다시 그린다.
        // 연출이 없는 동안에는 줄이 만료될 때까지 아무것도 하지 않는다
        private void Update()
        {
            if (entries.Count == 0)
            {
                return;
            }

            float now = Time.time;
            bool changed = false;

            // 맨 아랫줄(가장 오래된 것)부터 수명이 다하면 지운다
            while (entries.Count > 0 && now - entries[0].spawnTime >= lineLifetime)
            {
                entries.RemoveAt(0);
                changed = true;
            }

            if (entries.Count == 0)
            {
                damageText.text = string.Empty;
                return;
            }

            bool animating = false;
            for (int ix = 0; ix < entries.Count; ++ix)
            {
                float age = now - entries[ix].spawnTime;
                if (age < scaleAnimationTime || age > lineLifetime - fadeOutTime)
                {
                    animating = true;
                    break;
                }
            }

            if (changed || animating)
            {
                Rebuild();
            }
        }

        // 기록을 글자로 만든다. 아래 줄이 가장 오래된 것이므로 최신 줄부터 위에서 아래로 이어 붙인다
        private void Rebuild()
        {
            builder.Clear();
            float now = Time.time;

            for (int ix = entries.Count - 1; ix >= 0; --ix)
            {
                Entry entry = entries[ix];
                float age = now - entry.spawnTime;

                // 처음엔 크게 떴다가 scaleMin까지 작아진다
                float scale = age < scaleAnimationTime
                    ? Mathf.Lerp(scaleMax, scaleMin, age / scaleAnimationTime)
                    : scaleMin;
                if (entry.critical)
                {
                    scale *= criticalScaleBonus;
                }

                Color color = isPlayerTarget ? receivedColor : (entry.critical ? criticalColor : damageColor);
                float remaining = lineLifetime - age;
                color.a = fadeOutTime > 0f ? Mathf.Clamp01(remaining / fadeOutTime) : 1f;

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("<size=").Append(Mathf.RoundToInt(scale * 100f)).Append("%><color=#")
                    .Append(ColorUtility.ToHtmlStringRGBA(color)).Append('>')
                    .Append(entry.damage).Append(entry.critical ? "!" : string.Empty)
                    .Append("</color></size>");
            }

            damageText.text = builder.ToString();
        }

        private void OnDisable()
        {
            // 오브젝트가 꺼지면(풀링·사망 정리) 남은 줄이 다음에 켜졌을 때 되살아나지 않게 비운다
            entries.Clear();
            if (damageText != null)
            {
                damageText.text = string.Empty;
            }
        }
    }
}
