using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 기본 수치: 레벨별 능력치·필요 경험치 표와, 레벨과 무관한 이동·치명타·포인트 규칙.
    ///
    /// 에셋: Resources/Data/Player Data. DataManager가 불러 두고 PlayerStateManager·PlayerStats·PlayerLevelController가 읽는다.
    /// 최종 능력치는 PlayerStats가 "레벨 기본값 + 스탯 포인트 + 장비 + 버프"로 계산한다.
    /// </summary>
    public class PlayerData : ScriptableObject
    {
        /// <summary>레벨 한 단계의 기본 능력치.</summary>
        [System.Serializable]
        public class LevelData
        {
            public int level = 1;
            public float maxHP;
            public float maxMP;
            public float attack;
            public float defense;
            [Tooltip("치명타 확률(%)")]
            public float critRate;
            [Tooltip("이 레벨에 도달하는 데 필요한 누적 경험치")]
            public float requiredExp;
        }

        [Tooltip("레벨 1부터 순서대로 (목록 번호 = 레벨 - 1)")]
        public List<LevelData> levels;

        [Header("이동")]
        [Tooltip("달리기 속도(m/s). 달리기 클립에 루트 모션 이동이 없어 코드로 움직인다")]
        public float moveSpeed = 5f;
        [Tooltip("이동 방향으로 도는 속도(도/초)")]
        public float rotationSpeed = 1000f;

        [Header("치명타")]
        [Tooltip("치명타가 터졌을 때의 피해 배율(%). 레벨과 무관한 기본값")]
        public float baseCritDamage = 150f;

        [Header("레벨업 포인트")]
        [Tooltip("레벨업 1회당 받는 스킬 포인트")]
        public int skillPointPerLevel = 1;
        [Tooltip("레벨업 1회당 받는 스탯 포인트")]
        public int statPointPerLevel = 2;

        [Header("스탯 포인트 1개당 증가량")]
        public float attackPerStatPoint = 2f;
        public float defensePerStatPoint = 2f;
        public float maxHPPerStatPoint = 20f;
        public float critRatePerStatPoint = 1f;
    }
}
