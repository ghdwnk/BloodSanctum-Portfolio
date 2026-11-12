using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 골드/재료/물약/장비를 얻었을 때 경험치바 위에 "+25G", "강화석 1개"처럼 짧게 띄우는 알림.
    /// 대화창이나 모달을 쓰지 않아 전투 중에도 시야를 가리지 않는다.
    /// 미리 만들어 둔 텍스트 몇 개를 돌려 쓰므로(풀링) 획득할 때마다 오브젝트를 만들지 않는다.
    /// 같은 종류를 연달아 얻으면 새 줄을 만들지 않고 수량을 합쳐서 보여준다 (골드 +25G, +30G -> +55G)
    /// </summary>
    public class UIPickupFeed : MonoBehaviour
    {
        private static UIPickupFeed instance;

        private class Entry
        {
            public string key;
            public Item item;     // message도 null이면 골드
            public string message; // 있으면 수량 없이 이 문구만 보여준다 (장비 착용/해제 안내 등)
            public int amount;
            public float age;
            public TextMeshProUGUI text;
        }

        [Tooltip("돌려 쓸 텍스트 풀. 개수가 곧 동시에 보일 수 있는 줄 수다")]
        [SerializeField] private TextMeshProUGUI[] slots;
        [Tooltip("새로 뜬 줄이 투명에서 서서히 진해지는 시간")]
        [SerializeField] private float fadeInTime = 0.2f;
        [SerializeField] private float holdTime = 1.6f;
        [SerializeField] private float fadeTime = 0.8f;
        [SerializeField] private float lineHeight = 30f;
        [Tooltip("골드 줄 색 (인벤토리의 골드 표시와 같은 톤)")]
        [SerializeField] private string goldColorHex = "#FFD95C";

        // 최신 줄이 맨 앞. 맨 아래에 그리고 오래된 줄은 위로 밀어 올린다
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Stack<TextMeshProUGUI> freeSlots = new Stack<TextMeshProUGUI>();

        private void Awake()
        {
            instance = this;

            foreach (TextMeshProUGUI slot in slots)
            {
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    freeSlots.Push(slot);
                }
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static void ShowGold(int amount)
        {
            if (instance != null && amount > 0)
            {
                instance.Push("gold", null, amount);
            }
        }

        public static void ShowItem(Item item, int count)
        {
            if (instance != null && item != null && count > 0)
            {
                instance.Push("item:" + item.name, item, count);
            }
        }

        // 획득과 함께 쌓이는 한 줄 안내 (장비 착용/해제 등). 수량이 없는 문구라 같은 문구가 연달아 와도 줄을 합치지 않고 시간만 되돌린다
        public static void ShowMessage(string message)
        {
            if (instance != null && !string.IsNullOrEmpty(message))
            {
                instance.Push("message:" + message, null, 1, message);
            }
        }

        private void Push(string key, Item item, int amount, string message = null)
        {
            // 방금 같은 종류를 얻었으면 그 줄에 수량을 합치고 표시 시간을 되돌린다
            foreach (Entry existing in entries)
            {
                if (existing.key == key && existing.age < holdTime)
                {
                    existing.amount += amount;
                    // 이미 보이는 줄이므로 페이드 인은 건너뛰고 표시 시간만 되돌린다
                    existing.age = fadeInTime;
                    existing.text.text = BuildText(existing);
                    return;
                }
            }

            TextMeshProUGUI slot;
            if (freeSlots.Count > 0)
            {
                slot = freeSlots.Pop();
            }
            else
            {
                // 줄이 꽉 찼으면 가장 오래된 줄을 밀어낸다
                Entry oldest = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                slot = oldest.text;
            }

            Entry entry = new Entry { key = key, item = item, message = message, amount = amount, age = 0f, text = slot };
            slot.text = BuildText(entry);
            slot.alpha = 0f;
            slot.gameObject.SetActive(true);
            entries.Insert(0, entry);
        }

        private string BuildText(Entry entry)
        {
            if (entry.message != null)
            {
                return entry.message;
            }

            if (entry.item == null)
            {
                return $"<color={goldColorHex}>+{entry.amount:N0}G</color>";
            }

            return $"{entry.item.ColoredName} {entry.amount}개";
        }

        private void Update()
        {
            if (entries.Count == 0)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;

            for (int ix = entries.Count - 1; ix >= 0; --ix)
            {
                Entry entry = entries[ix];
                entry.age += dt;

                if (entry.age >= holdTime + fadeTime)
                {
                    entry.text.gameObject.SetActive(false);
                    freeSlots.Push(entry.text);
                    entries.RemoveAt(ix);
                }
            }

            for (int ix = 0; ix < entries.Count; ++ix)
            {
                Entry entry = entries[ix];
                float alpha;
                if (entry.age < fadeInTime)
                {
                    alpha = entry.age / fadeInTime;
                }
                else
                {
                    alpha = entry.age < holdTime ? 1f : 1f - (entry.age - holdTime) / fadeTime;
                }

                entry.text.alpha = Mathf.Clamp01(alpha);
                // 오래된 줄일수록 위에 놓는다. 사라지는 줄은 조금 더 떠오르게 해서 흘러가는 느낌을 준다
                float rise = entry.age < holdTime ? 0f : (entry.age - holdTime) / fadeTime * 10f;
                entry.text.rectTransform.anchoredPosition = new Vector2(0f, ix * lineHeight + rise);
            }
        }
    }
}
