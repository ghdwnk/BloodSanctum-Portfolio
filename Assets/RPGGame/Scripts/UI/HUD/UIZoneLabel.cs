using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 미니맵 위에 지금 플레이어가 있는 지역 이름을 계속 보여준다.
    /// 지역 판정은 ZoneTracker가 하고, 이 스크립트는 바뀔 때 글자만 갈아 끼운다 (UIZoneTitle과 같은 구독 방식).
    /// 지역 배너(UIZoneTitle)는 들어설 때 잠깐 뜨고 사라지지만, 이쪽은 항상 떠 있어 지금 위치를 알려 준다
    /// </summary>
    public class UIZoneLabel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("지역 밖에 있을 때는 판 전체를 숨긴다. 켜고 끌 대상(보통 판 자신의 자식 루트)")]
        [SerializeField] private GameObject plate;

        private void Start()
        {
            ZoneTracker tracker = FindAnyObjectByType<ZoneTracker>();
            if (tracker != null)
            {
                tracker.SubscribeOnZoneChanged(Refresh);
            }

            // 씬을 시작할 때 이미 지역 안에 있으면 이벤트가 오기 전이라도 바로 보여준다
            Refresh(ZoneTracker.CurrentZone);
        }

        private void Refresh(ZoneArea zone)
        {
            bool hasZone = zone != null && !string.IsNullOrEmpty(zone.ZoneName);

            if (plate != null)
            {
                plate.SetActive(hasZone);
            }

            if (hasZone && label != null)
            {
                label.text = zone.ZoneName;
            }
        }
    }
}
