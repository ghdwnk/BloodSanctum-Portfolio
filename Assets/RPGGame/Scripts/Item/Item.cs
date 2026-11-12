using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 모든 아이템 에셋(ScriptableObject)의 공통 부모.
    ///
    /// 실제 에셋은 자식 클래스로 만든다.
    ///  - EquipmentItem : 무기·갑옷·투구. 얻을 때마다 EquipmentInstance(개체)로 따로 보관된다
    ///  - HealthItem    : 체력 물약 (소모품)
    ///  - MaterialItem  : 강화석·블러드스톤 조각 같은 재료
    ///
    /// 에셋은 Assets/RPGGame/Data/Item 아래에 있고, 세이브에서 이름으로 다시 찾을 수 있도록
    /// Resources/Data/Item Database에 전부 등록돼 있어야 한다.
    /// 인벤토리는 같은 에셋을 개수로 쌓아 보관한다 (장비만 예외).
    /// </summary>
    public abstract class Item : ScriptableObject
    {
        /// <summary>인벤토리 탭 분류. 정수 값이 에셋에 저장되므로 순서를 바꾸지 않는다.</summary>
        public enum Category { Equipment, Consumable, Etc }

        /// <summary>아이템 등급. 정수 값이 에셋에 저장되므로 값을 바꾸지 말고 끝에만 추가한다.</summary>
        public enum Grade { Common, Rare, Legendary }

        [Header("기본 정보")]
        [Tooltip("인벤토리·툴팁에 보이는 이름")]
        public string itemName;
        [Tooltip("인벤토리에서 어느 탭에 보일지")]
        public Category category;
        [Tooltip("등급. 이름 색과 드랍 외곽선 색이 등급을 따른다")]
        public Grade grade = Grade.Common;
        [Tooltip("인벤토리·퀵슬롯·드랍에 쓰는 아이콘")]
        public Sprite sprite;

        [Header("상점")]
        [Tooltip("상점에 되팔 때 받는 금액. 0이면 상점 목록의 가격 x 판매 비율로 계산하고, 목록에도 없으면 팔 수 없다")]
        public int sellPrice = 0;

        // ───────────────────────── 등급 표시 ─────────────────────────

        /// <summary>등급별 표시 색 (일반=회색 / 희귀=파랑 / 전설=주황).</summary>
        public static Color GetGradeColor(Grade grade)
        {
            switch (grade)
            {
                case Grade.Rare: return new Color(0.38f, 0.63f, 1f);
                case Grade.Legendary: return new Color(1f, 0.55f, 0.15f);
                default: return new Color(0.78f, 0.78f, 0.78f);
            }
        }

        /// <summary>등급의 한국어 이름.</summary>
        public static string GetGradeName(Grade grade)
        {
            switch (grade)
            {
                case Grade.Rare: return "희귀";
                case Grade.Legendary: return "전설";
                default: return "일반";
            }
        }

        public Color GradeColor => GetGradeColor(grade);
        public string GradeName => GetGradeName(grade);

        /// <summary>아이템 이름을 등급 색으로 감싼 리치 텍스트. TextMeshPro에 그대로 넣으면 색이 입혀진다.</summary>
        public string ColoredName => $"<color=#{ColorUtility.ToHtmlStringRGB(GradeColor)}>{itemName}</color>";

        // ───────────────────────── 사용 ─────────────────────────

        /// <summary>
        /// 인벤토리 목록이나 퀵슬롯에서 아이템을 사용했을 때 불린다.
        /// 종류마다 하는 일이 다르다: 물약은 회복 후 1개 소모, 재료는 안내만, 장비는 인벤토리 창이 따로 처리한다.
        /// </summary>
        public abstract void Use();
    }
}
