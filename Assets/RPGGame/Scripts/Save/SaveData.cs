using System.Collections.Generic;

namespace RPGGame
{
    /// <summary>
    /// 세이브 파일(save.json) 한 개에 담기는 전체 데이터. JsonUtility로 그대로 직렬화된다.
    ///
    /// 필드를 추가할 때는 기본값을 꼭 넣는다. 예전 세이브에는 그 필드가 없어서 기본값으로 읽히기 때문이다.
    /// 필드 이름을 바꾸면 기존 세이브의 값이 사라지므로 이름은 바꾸지 않는다.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        // ── 플레이어 ──

        /// <summary>로비에서 정한 닉네임. 예전 세이브에는 없으므로 비어 있으면 기본 이름을 쓴다.</summary>
        public string nickname = "";
        public int level = 1;
        public float currentExp;
        public float positionX;
        public float positionY;
        public float positionZ;

        // ── 성장 ──

        /// <summary>아직 쓰지 않은 스탯 포인트.</summary>
        public int statPoints;
        /// <summary>아직 쓰지 않은 스킬 포인트.</summary>
        public int skillPoints;
        /// <summary>스탯별로 투자한 포인트 (PlayerStats.StatType 순서).</summary>
        public List<int> allocatedStatPoints = new List<int>();
        /// <summary>배운 스킬의 에셋 이름 목록.</summary>
        public List<string> unlockedSkills = new List<string>();
        /// <summary>unlockedSkills와 같은 순서의 스킬 레벨. 예전 세이브에는 없으므로 비어 있으면 모두 1레벨로 본다.</summary>
        public List<int> skillLevels = new List<int>();

        // ── 소지품 ──

        public int gold;
        /// <summary>소모품·재료 아이템과 개수 (장비 제외).</summary>
        public List<ItemSaveEntry> items = new List<ItemSaveEntry>();
        /// <summary>인벤토리의 장비 개체 전부 (장착 중인 것 포함). 비어 있으면 예전 형식으로 보고 변환한다.</summary>
        public List<EquipmentSaveEntry> equipmentInstances = new List<EquipmentSaveEntry>();
        /// <summary>부위 순서(무기·갑옷·투구)대로 장착 중인 장비 개체의 ID. 빈 부위는 빈 문자열.</summary>
        public List<string> equippedIds = new List<string>();

        // ── 진행 ──

        public List<QuestSaveEntry> quests = new List<QuestSaveEntry>();

        // ── 예전 형식 (읽기 전용) ──
        // 장비가 개체가 아니라 에셋 하나로 다뤄지던 시절의 데이터. 새로 저장할 때는 채우지 않고,
        // 예전 세이브를 불러올 때 GameSaveLoader가 개체 형식으로 변환하는 데만 쓴다

        /// <summary>[예전 형식] 부위별로 장착 중인 장비의 에셋 이름.</summary>
        public List<string> equippedItems = new List<string>();
        /// <summary>[예전 형식] 장비 에셋별 강화 단계. itemName에 장비 에셋 이름, count에 강화 단계를 담는다.</summary>
        public List<ItemSaveEntry> enhancements = new List<ItemSaveEntry>();
    }

    /// <summary>소모품·재료 아이템 한 종류의 저장 데이터.</summary>
    [System.Serializable]
    public class ItemSaveEntry
    {
        /// <summary>아이템 에셋 이름 (ItemDatabase.FindByName으로 다시 찾는다).</summary>
        public string itemName;
        public int count;
    }

    /// <summary>
    /// 장비 개체 하나의 저장 데이터. 같은 장비라도 얻을 때 굴린 스탯과 강화 단계가 개체마다 다르다.
    /// </summary>
    [System.Serializable]
    public class EquipmentSaveEntry
    {
        /// <summary>개체 ID. 장착 상태(equippedIds)를 복원할 때 이 값으로 찾는다.</summary>
        public string id;
        public string itemName;
        public float attack;
        public float defense;
        public float maxHP;
        /// <summary>강화 단계(+N).</summary>
        public int level;
    }

    /// <summary>퀘스트 하나의 저장 데이터. 상태는 QuestManager.QuestProgressState를 정수로 담는다.</summary>
    [System.Serializable]
    public class QuestSaveEntry
    {
        public int questID;
        public int state;
        public int progressCount;
    }
}
