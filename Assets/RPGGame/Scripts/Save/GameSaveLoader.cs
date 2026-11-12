using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 게임 씬에서 세이브를 적용하고, 플레이 중 자동 저장을 맡는 컴포넌트.
    ///
    /// - 시작할 때: 로비에서 "이어하기"로 넘어왔다면(SaveManager.ShouldLoadOnStart) 세이브를 적용한다.
    ///   이어서 플레이어 머리 위에 닉네임 이름표를 붙인다.
    /// - 플레이 중: autoSaveInterval초마다 자동 저장한다.
    /// - 환경설정의 "로비로 나가기"를 누르면 UIOptionsPanel이 SaveNow를 불러 즉시 저장한다.
    /// </summary>
    public class GameSaveLoader : MonoBehaviour
    {
        private const string NameTagPrefabPath = "UI/Player Name Tag";
        // 이름표를 캐릭터 키보다 이만큼 위에 둔다(m)
        private const float NameTagGap = 0.35f;
        // 저장된 위치와 그 아래 실제 지면 높이의 허용 차이(m). 이보다 크면 맵이 바뀐 뒤의 옛 좌표로 본다
        private const float MaxRestoreHeightGap = 6f;

        [Tooltip("자동 저장 간격(초)")]
        [SerializeField] private float autoSaveInterval = 60f;

        // 플레이어는 게임 씬 내내 같은 오브젝트이므로 시작할 때 한 번 찾아 둔다
        private PlayerStateManager player;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerStateManager>();

            if (SaveManager.ShouldLoadOnStart && SaveManager.HasSaveData())
            {
                ApplySaveData(SaveManager.Load());
            }

            AttachNameTag();
            StartCoroutine(AutoSaveRoutine());
        }

        private IEnumerator AutoSaveRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(autoSaveInterval);

            while (true)
            {
                yield return wait;
                SaveNow();
            }
        }

        // ───────────────────────── 불러오기 ─────────────────────────

        // 플레이어 머리 위에 닉네임 이름표를 붙인다. 이름표 프리팹은 NPC 이름표와 같은 월드 캔버스 구조다
        private void AttachNameTag()
        {
            GameObject prefab = Resources.Load<GameObject>(NameTagPrefabPath);
            if (player == null || prefab == null)
            {
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            float height = controller != null ? controller.height : 1.8f;

            GameObject nameTag = Instantiate(prefab, player.transform);
            nameTag.transform.localPosition = new Vector3(0f, height + NameTagGap, 0f);
        }

        // 세이브를 플레이어·인벤토리·퀘스트·재화에 적용한다. 순서가 중요하다:
        // 레벨 → 스탯 포인트 → 스킬 → 인벤토리 → 장착 → 퀘스트 → 골드 → 체력·MP 가득 채우기
        private void ApplySaveData(SaveData data)
        {
            if (data == null || player == null)
            {
                return;
            }

            PlayerProfile.SetNickname(data.nickname);
            RestorePosition(new Vector3(data.positionX, data.positionY, data.positionZ));

            // 카메라 리그는 Awake에서 씬에 놓인 플레이어 자리에 붙는데, 방금 플레이어를 저장된 위치로 옮겼다.
            // 그대로 두면 첫 화면에서 카메라가 원래 자리에서 플레이어에게 날아오는 모습이 보인다.
            // 이 함수는 첫 프레임이 그려지기 전(Start)에 불리므로, 로딩 화면이 끝나고 처음 보이는 장면부터 카메라가 이미 플레이어에게 붙어 있다
            CameraRig.SnapToTarget();

            // 레벨/경험치를 넣은 뒤 레벨업 처리 함수를 그대로 써서 레벨에 맞는 기본 스탯까지 맞춘다
            PlayerLevelController levelController = player.GetComponentInChildren<PlayerLevelController>();
            if (levelController != null)
            {
                levelController.LoadState(data.level, data.currentExp);
            }

            player.OnPlayerLevelUP(data.level);

            // 스탯 포인트는 레벨을 반영한 뒤에 넣어야 분배 내역까지 합산된 최대 HP/MP가 나온다
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.LoadState(data.statPoints, data.skillPoints,
                    data.allocatedStatPoints != null ? data.allocatedStatPoints.ToArray() : null);
            }

            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();
            if (skillController != null)
            {
                skillController.LoadUnlockedSkills(data.unlockedSkills, data.skillLevels);
            }

            // 인벤토리(장비 개체 포함)를 먼저 되살려야 장착 상태를 개체 ID로 찾을 수 있다
            List<string> equippedIds = RestoreInventory(data);

            PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
            if (equipment != null)
            {
                equipment.LoadState(equippedIds);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.LoadState(data.quests);
            }

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.LoadGold(data.gold);
            }

            // 세이브에 현재 체력을 저장하지 않으므로, 장비까지 반영된 최대치로 가득 채운 채 시작한다
            if (playerStats != null)
            {
                playerStats.RefillToMax();
            }
        }

        // 저장된 위치로 옮긴다. 그 자리가 지금 맵에 없으면(맵 구조가 바뀌기 전 세이브 등)
        // 허공에 떨어지지 않도록 마을 부활 지점에서 시작한다
        private void RestorePosition(Vector3 savedPosition)
        {
            Util.TeleportCharacter(player.transform, savedPosition, player.transform.rotation);

            if (!IsValidRestorePosition(savedPosition))
            {
                Debug.LogWarning($"[GameSaveLoader] 저장된 위치 {savedPosition}에 지면이 없어 마을에서 시작합니다.");
                player.GetComponent<PlayerRespawnController>()?.MoveToRespawnPointNow();
            }
        }

        // 저장된 위치에 서 있을 수 있는지: 값이 깨지지 않았고(NaN·무한대 아님), 아래에 지면이 있으며 높이가 크게 어긋나지 않아야 한다
        private static bool IsValidRestorePosition(Vector3 position)
        {
            bool broken = float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)
                          || float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z);
            if (broken)
            {
                return false;
            }

            return Util.SampleGroundHeight(position, out float groundY) && Mathf.Abs(position.y - groundY) <= MaxRestoreHeightGap;
        }

        // 인벤토리를 되살리고, 장착해야 할 장비 개체의 ID 목록(부위 순서)을 돌려준다.
        // 새 형식은 장비 개체 목록을 그대로 되살리고, 예전 형식은 "같은 장비 n개 + 에셋별 강화 단계"를 개체로 바꾼다
        private static List<string> RestoreInventory(SaveData data)
        {
            InventoryManager inventory = InventoryManager.Instance;
            ItemDatabase database = DataManager.Instance != null ? DataManager.Instance.itemDatabase : null;

            if (inventory == null || database == null)
            {
                return data.equippedIds;
            }

            // 씬에 기본 지급 장비가 이미 들어 있으므로 세이브 내용으로 통째로 바꾼다
            inventory.ClearEquipments();

            foreach (ItemSaveEntry entry in data.items)
            {
                Item item = database.FindByName(entry.itemName);
                if (item != null && !(item is EquipmentItem))
                {
                    inventory.LoadItem(item, entry.count);
                }
            }

            bool isNewFormat = data.equipmentInstances != null && data.equipmentInstances.Count > 0;
            List<string> equippedIds = isNewFormat
                ? RestoreEquipmentInstances(data, database, inventory)
                : ConvertLegacyEquipment(data, database, inventory);

            // 불러오는 동안에는 목록 변경 알림을 하나씩 보내지 않고, 끝난 뒤 한 번만 보낸다
            inventory.NotifyItemListChanged();

            return equippedIds;
        }

        // 새 형식: 저장된 장비 개체를 그대로 되살린다
        private static List<string> RestoreEquipmentInstances(SaveData data, ItemDatabase database, InventoryManager inventory)
        {
            foreach (EquipmentSaveEntry entry in data.equipmentInstances)
            {
                EquipmentItem item = database.FindByName(entry.itemName) as EquipmentItem;
                if (item == null)
                {
                    continue;
                }

                inventory.LoadEquipment(new EquipmentInstance
                {
                    id = string.IsNullOrEmpty(entry.id) ? System.Guid.NewGuid().ToString("N") : entry.id,
                    item = item,
                    // 장비 에셋에서 없앤 스탯(예: 무기의 방어력)이 예전 개체에 남아 있지 않도록,
                    // 에셋 기본값이 0인 스탯은 저장된 값과 상관없이 0으로 둔다
                    attack = item.attackBonus > 0f ? entry.attack : 0f,
                    defense = item.defenseBonus > 0f ? entry.defense : 0f,
                    maxHP = item.maxHPBonus > 0f ? entry.maxHP : 0f,
                    enhanceLevel = Mathf.Clamp(entry.level, 0, EnhancementData.FixedMaxLevel)
                });
            }

            return data.equippedIds;
        }

        // 예전 형식: 같은 장비 n개는 원본 스탯의 개체 n개로 만들고, 에셋별 강화 단계는 그 장비의 첫 개체에 붙인다.
        // 예전 방식에서는 장착·강화가 에셋 하나를 가리켰으므로, 첫 개체를 장착 대상으로 삼으면 기존 상태가 그대로 이어진다
        private static List<string> ConvertLegacyEquipment(SaveData data, ItemDatabase database, InventoryManager inventory)
        {
            Dictionary<string, int> legacyLevels = new Dictionary<string, int>();
            if (data.enhancements != null)
            {
                foreach (ItemSaveEntry entry in data.enhancements)
                {
                    legacyLevels[entry.itemName] = entry.count;
                }
            }

            Dictionary<EquipmentItem, EquipmentInstance> firstOfKind = new Dictionary<EquipmentItem, EquipmentInstance>();

            foreach (ItemSaveEntry entry in data.items)
            {
                EquipmentItem item = database.FindByName(entry.itemName) as EquipmentItem;
                if (item == null)
                {
                    continue;
                }

                for (int ix = 0; ix < Mathf.Max(1, entry.count); ++ix)
                {
                    EquipmentInstance instance = EquipmentInstance.Create(item, false);

                    if (ix == 0)
                    {
                        firstOfKind[item] = instance;

                        if (legacyLevels.TryGetValue(item.name, out int level))
                        {
                            instance.enhanceLevel = Mathf.Clamp(level, 0, EnhancementData.FixedMaxLevel);
                        }
                    }

                    inventory.LoadEquipment(instance);
                }
            }

            List<string> equippedIds = new List<string>();
            if (data.equippedItems != null)
            {
                foreach (string assetName in data.equippedItems)
                {
                    EquipmentItem item = string.IsNullOrEmpty(assetName) ? null : database.FindByName(assetName) as EquipmentItem;
                    equippedIds.Add(item != null && firstOfKind.TryGetValue(item, out EquipmentInstance first) ? first.id : string.Empty);
                }
            }

            return equippedIds;
        }

        // ───────────────────────── 저장 ─────────────────────────

        /// <summary>
        /// 지금 상태를 바로 저장한다 (자동 저장, 환경설정의 "로비로 나가기").
        /// </summary>
        public void SaveNow()
        {
            if (player == null)
            {
                return;
            }

            PlayerLevelController levelController = player.GetComponentInChildren<PlayerLevelController>();
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();
            PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
            Vector3 position = player.transform.position;

            SaveData data = new SaveData
            {
                nickname = PlayerProfile.Nickname,
                level = levelController != null ? levelController.Level : 1,
                currentExp = levelController != null ? levelController.CurrentExp : 0f,
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                statPoints = playerStats != null ? playerStats.AvailableStatPoints : 0,
                skillPoints = playerStats != null ? playerStats.AvailableSkillPoints : 0
            };

            if (playerStats != null)
            {
                data.allocatedStatPoints = new List<int>(playerStats.AllocatedPoints);
            }

            if (skillController != null)
            {
                data.unlockedSkills = skillController.GetUnlockedSkillNames();
                data.skillLevels = skillController.GetSkillLevelValues();
            }

            if (equipment != null)
            {
                data.equippedIds = equipment.GetEquippedIds();
            }

            if (QuestManager.Instance != null)
            {
                data.quests = QuestManager.Instance.GetSaveEntries();
            }

            if (CurrencyManager.Instance != null)
            {
                data.gold = CurrencyManager.Instance.Gold;
            }

            if (InventoryManager.Instance != null)
            {
                foreach (ItemSlot slot in InventoryManager.Instance.GetItems())
                {
                    data.items.Add(new ItemSaveEntry { itemName = slot.item.name, count = slot.count });
                }

                foreach (EquipmentInstance instance in InventoryManager.Instance.GetEquipments())
                {
                    data.equipmentInstances.Add(new EquipmentSaveEntry
                    {
                        id = instance.id,
                        itemName = instance.item.name,
                        attack = instance.attack,
                        defense = instance.defense,
                        maxHP = instance.maxHP,
                        level = instance.enhanceLevel
                    });
                }
            }

            SaveManager.Save(data);
        }
    }
}
