using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 장비 강화 규칙(재료·단계별 비용과 성공 확률·단계당 스탯 상승률)을 담은 에셋.
    ///
    /// 에셋: Assets/RPGGame/Data/Enhancement/Enhancement Data. 게임 씬의 EnhancementManager가 참조한다.
    /// 밸런스를 바꿀 때 코드를 고치지 않고 이 에셋의 값만 바꾸면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Enhancement Data", menuName = "Scriptable Objects/Enhancement Data")]
    public class EnhancementData : ScriptableObject
    {
        /// <summary>한 단계를 올릴 때 드는 비용과 성공 확률.</summary>
        [System.Serializable]
        public class Step
        {
            [Tooltip("소모하는 재료(강화석) 개수")]
            public int materialCount = 1;
            [Tooltip("소모하는 골드")]
            public int goldCost = 100;
            [Tooltip("성공 확률 (0~1). 실패해도 단계는 내려가지 않고 재료·골드만 소모된다")]
            [Range(0f, 1f)] public float successRate = 1f;
        }

        /// <summary>
        /// 최대 강화 단계. 강화 이펙트 단계(1~3 / 4~6 / 7~9)와 비용 표(9칸)가 이 값에 맞춰져 있어
        /// 인스펙터에서 바꾸면 안 되므로 필드가 아니라 상수로 둔다.
        /// </summary>
        public const int FixedMaxLevel = 9;

        [Header("재료")]
        [Tooltip("강화에 쓰는 재료 아이템 (강화석)")]
        public MaterialItem materialItem;

        [Header("규칙")]
        [Tooltip("한 단계마다 장비 기본 스탯(공격력·방어력·체력)에 더해지는 비율. 0.15면 +1마다 기본치의 15%씩 늘어난다")]
        [Range(0f, 1f)] public float statRatePerLevel = 0.15f;

        [Tooltip("단계별 비용·확률. steps[i]는 +i에서 +(i+1)로 올릴 때의 조건이다 (0번째가 첫 강화)")]
        public List<Step> steps = new();

        /// <summary>
        /// 현재 단계에서 다음 단계로 올릴 때의 조건. 목록이 최대 단계보다 짧으면 마지막 줄을 계속 쓴다
        /// (에셋을 덜 채워도 동작하게). 목록이 비어 있으면 null.
        /// </summary>
        public Step GetStep(int currentLevel)
        {
            if (steps.Count == 0)
            {
                return null;
            }

            return steps[Mathf.Clamp(currentLevel, 0, steps.Count - 1)];
        }

        /// <summary>강화 단계에 따른 스탯 배율. +0이면 1배, +9이면 1 + 9 x statRatePerLevel배.</summary>
        public float GetStatMultiplier(int level)
        {
            return 1f + Mathf.Max(0, level) * statRatePerLevel;
        }
    }
}
