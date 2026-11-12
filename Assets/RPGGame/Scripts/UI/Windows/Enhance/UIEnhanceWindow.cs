using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 장비 강화 창 (기획서 14-6절).
    /// 왼쪽에서 강화할 장비를 고르면 가운데에 비용/성공확률이 뜨고, 결과는 팝업으로 알려준다
    /// </summary>
    public class UIEnhanceWindow : MonoBehaviour
    {
        private static UIEnhanceWindow instance;

        [SerializeField] private GameObject window;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("장비 목록")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UIEnhanceListItem listItemPrefab;
        [SerializeField] private GameObject emptyListHint;

        [Header("강화 패널")]
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private GameObject selectHint;
        [SerializeField] private Image targetIcon;
        [SerializeField] private TextMeshProUGUI targetNameText;
        [SerializeField] private TextMeshProUGUI statPreviewText;
        [SerializeField] private Image materialIcon;
        [SerializeField] private TextMeshProUGUI materialText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI successRateText;
        [SerializeField] private Button enhanceButton;
        [SerializeField] private TextMeshProUGUI enhanceButtonLabel;

        private static readonly Color SuccessColor = new Color(1f, 0.82f, 0.29f);
        private static readonly Color FailColor = new Color(0.85f, 0.42f, 0.42f);

        // 목록 줄은 매번 만들지 않고 재사용한다 (상점 창과 같은 방식)
        private readonly List<UIEnhanceListItem> rows = new();
        // 문자열 조립을 반복하므로 버퍼를 하나만 두고 지워 쓴다
        private readonly StringBuilder builder = new StringBuilder(160);

        private EquipmentInstance selected;
        private bool isSubscribed;

        public static bool IsOn => instance != null && instance.window != null && instance.window.activeSelf;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (window != null)
            {
                window.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static void Show()
        {
            if (instance == null)
            {
                return;
            }

            UISound.SetActive(instance.window, true);
            instance.Refresh();
        }

        public static void Close()
        {
            if (instance != null)
            {
                UISound.SetActive(instance.window, false);
                UIItemTooltip.Hide();
            }
        }

        // 닫기 버튼에서 호출
        public void OnClickClose()
        {
            Close();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            EnhancementManager manager = EnhancementManager.Instance;

            if (manager == null || manager.Data == null)
            {
                return;
            }

            if (!isSubscribed)
            {
                // 강화 결과와 인벤토리 변화 모두 목록/비용 표시에 영향을 준다 (한 번만 구독)
                manager.SubscribeOnEnhancementChanged(Refresh);
                InventoryManager.Instance?.SubscribeOnItemListChanged(Refresh);
                isSubscribed = true;
            }

            if (titleText != null)
            {
                titleText.text = "장비 강화";
            }

            RefreshList(manager);
            RefreshDetail(manager);
        }

        // 인벤토리에 있는 장비만 모아 목록을 만든다
        private void RefreshList(EnhancementManager manager)
        {
            // 같은 종류라도 개체마다 스탯과 강화 단계가 다르므로 한 줄에 개체 하나씩 보여준다
            List<EquipmentInstance> equipments = new List<EquipmentInstance>();
            if (InventoryManager.Instance != null)
            {
                equipments.AddRange(InventoryManager.Instance.GetEquipments());
            }

            PlayerEquipment playerEquipment = FindAnyObjectByType<PlayerEquipment>();

            // 선택해 둔 장비를 팔았거나 잃었으면 선택을 해제한다
            if (selected != null && !equipments.Contains(selected))
            {
                selected = null;
            }

            // 첫 표시에서 빈 화면이 되지 않도록 착용 중인 무기를 기본 선택으로 잡는다
            if (selected == null && equipments.Count > 0)
            {
                selected = equipments[0];

                foreach (EquipmentInstance equipment in equipments)
                {
                    if (playerEquipment != null && playerEquipment.IsEquipped(equipment))
                    {
                        selected = equipment;
                        break;
                    }
                }
            }

            EnsureRowCount(equipments.Count);

            for (int ix = 0; ix < equipments.Count; ++ix)
            {
                EquipmentInstance equipment = equipments[ix];
                bool isEquipped = playerEquipment != null && playerEquipment.IsEquipped(equipment);

                rows[ix].Set(equipment, manager.GetLevel(equipment), isEquipped, equipment == selected,
                    () => Select(equipment));
            }

            if (emptyListHint != null)
            {
                emptyListHint.SetActive(equipments.Count == 0);
            }
        }

        private void Select(EquipmentInstance item)
        {
            selected = item;
            Refresh();
        }

        private void EnsureRowCount(int count)
        {
            while (rows.Count < count)
            {
                rows.Add(Instantiate(listItemPrefab, listContent));
            }

            for (int ix = 0; ix < rows.Count; ++ix)
            {
                rows[ix].gameObject.SetActive(ix < count);
            }
        }

        // 가운데 패널(선택한 장비의 비용/확률/스탯 변화)을 갱신한다
        private void RefreshDetail(EnhancementManager manager)
        {
            bool hasTarget = selected != null;

            if (detailRoot != null)
            {
                detailRoot.SetActive(hasTarget);
            }

            if (selectHint != null)
            {
                selectHint.SetActive(!hasTarget);
            }

            if (!hasTarget)
            {
                return;
            }

            int level = manager.GetLevel(selected);
            EnhancementData data = manager.Data;
            EnhancementData.Step step = manager.GetNextStep(selected);
            bool isMax = manager.IsMaxLevel(selected);

            if (targetIcon != null)
            {
                targetIcon.sprite = selected.item.sprite;
                targetIcon.enabled = selected.item.sprite != null;
            }

            if (targetNameText != null)
            {
                targetNameText.text = isMax
                    ? $"{selected.item.ColoredName} <color=#FFD24A>+{level}</color>  (최대)"
                    : $"{selected.item.ColoredName} <color=#FFD24A>+{level}</color>  →  <color=#FFD24A>+{level + 1}</color>";
            }

            if (statPreviewText != null)
            {
                statPreviewText.text = BuildStatPreview(data, level, isMax);
            }

            if (materialIcon != null)
            {
                materialIcon.sprite = data.materialItem != null ? data.materialItem.sprite : null;
                materialIcon.enabled = data.materialItem != null && data.materialItem.sprite != null;
            }

            int owned = data.materialItem != null && InventoryManager.Instance != null
                ? InventoryManager.Instance.GetItemCount(data.materialItem)
                : 0;

            if (materialText != null)
            {
                if (isMax || step == null)
                {
                    materialText.text = "-";
                }
                else
                {
                    // 모자란 재료는 붉게 표시해서 버튼을 눌러보지 않아도 알 수 있게 한다
                    string color = owned >= step.materialCount ? "#DCDCDC" : "#E06B6B";
                    materialText.text = $"<color={color}>{step.materialCount}개 필요</color>  (보유 {owned})";
                }
            }

            if (goldText != null)
            {
                int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gold : 0;

                if (isMax || step == null)
                {
                    goldText.text = "-";
                }
                else
                {
                    string color = gold >= step.goldCost ? "#DCDCDC" : "#E06B6B";
                    goldText.text = $"<color={color}>{step.goldCost:N0} G</color>";
                }
            }

            if (successRateText != null)
            {
                successRateText.text = isMax || step == null
                    ? "최대 단계"
                    : $"성공확률 {step.successRate * 100f:0}%";
            }

            bool canEnhance = manager.CanEnhance(selected, out string blockedReason);

            if (enhanceButton != null)
            {
                enhanceButton.interactable = canEnhance;

                // 비활성 버튼을 눌렀을 때 이유(재화/재료/레벨 부족 등)를 작은 안내로 띄운다
                UIBlockedButtonNotice.Apply(enhanceButton, canEnhance, blockedReason);
            }

            if (enhanceButtonLabel != null)
            {
                enhanceButtonLabel.text = isMax ? "최대 단계" : "강화하기";
            }
        }

        // 지금 스탯과 다음 단계 스탯을 나란히 보여주는 문자열을 만든다
        private string BuildStatPreview(EnhancementData data, int level, bool isMax)
        {
            builder.Clear();

            float current = data.GetStatMultiplier(level);
            float next = data.GetStatMultiplier(level + 1);

            // 이 개체가 굴려 가진 스탯 기준으로 지금 값과 다음 단계 값을 보여준다
            AppendStatLine("공격력", selected.attack, current, next, isMax);
            AppendStatLine("방어력", selected.defense, current, next, isMax);
            AppendStatLine("체력", selected.maxHP, current, next, isMax);

            return builder.ToString();
        }

        private void AppendStatLine(string label, float baseValue, float current, float next, bool isMax)
        {
            if (Mathf.Approximately(baseValue, 0f))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(label).Append(' ').Append((baseValue * current).ToString("0"));

            if (!isMax)
            {
                builder.Append("  <color=#8FD46A>→ ").Append((baseValue * next).ToString("0")).Append("</color>");
            }
        }

        // 강화하기 버튼
        public void OnClickEnhance()
        {
            EnhancementManager manager = EnhancementManager.Instance;

            if (manager == null || selected == null)
            {
                return;
            }

            if (!manager.TryEnhance(selected, out bool success, out string message))
            {
                // 재료/골드가 모자란 경우. 반드시 읽어야 하는 안내라 확인 버튼이 달린 창으로 띄운다
                UISystemMessage.Show(message);
                return;
            }

            ShowResult(success, message);
        }

        // 강화 결과도 공용 시스템 창으로 띄운다.
        // 창마다 따로 팝업을 두면 모양이 조금씩 달라져서 하나로 모았다
        private void ShowResult(bool success, string message)
        {
            UISystemMessage.Show(
                success ? "강화 성공!" : "강화 실패...",
                success ? message : $"{message}\n재료와 골드는 소모되었습니다.",
                success ? SuccessColor : FailColor);
        }
    }
}
