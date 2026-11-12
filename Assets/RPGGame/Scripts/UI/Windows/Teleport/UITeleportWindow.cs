using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 텔레포트 이동 목록 창. 포털(TeleportPortal)에서 F키로 열리고, 가고 싶은 곳을 마우스 왼쪽 클릭하면 이동한다.
    ///
    /// 목록은 TeleportNetwork의 모든 장소에서 "포털이 있는 장소"를 뺀 것이다.
    /// 마을 포털이면 던전 5곳, 던전 포털이면 마을과 나머지 던전 4곳이 뜬다.
    /// 실제 이동과 로딩 화면은 UITeleportScreen이 맡는다
    /// </summary>
    public class UITeleportWindow : MonoBehaviour
    {
        private static UITeleportWindow instance;

        [SerializeField] private GameObject window;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UITeleportListItem listItemPrefab;

        // 목록 줄은 열 때마다 만들지 않고 재사용한다 (상점·강화 창과 같은 방식)
        private readonly List<UITeleportListItem> rows = new();

        private Transform player;

        public static bool IsOn => instance != null && instance.window != null && instance.window.activeSelf;

        /// <summary>지금 창을 연 포털. 창이 닫혀 있으면 null. 포털이 플레이어가 멀어졌을 때 자기 창만 닫는 데 쓴다.</summary>
        public static TeleportPortal OpenedBy { get; private set; }

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
                OpenedBy = null;
            }
        }

        /// <summary>포털에서 목록 창을 연다. player는 이동시킬 대상이다.</summary>
        public static void Show(TeleportPortal portal, Transform player)
        {
            if (instance == null || portal == null || TeleportNetwork.Instance == null)
            {
                return;
            }

            instance.player = player;
            OpenedBy = portal;
            instance.Refresh(portal.LocationId);
            UISound.SetActive(instance.window, true);
        }

        public static void Close()
        {
            OpenedBy = null;

            if (instance != null)
            {
                UISound.SetActive(instance.window, false);
            }
        }

        // 닫기 버튼에서 호출
        public void OnClickClose()
        {
            Close();
        }

        // 현재 장소를 뺀 목록을 줄로 채운다
        private void Refresh(string currentLocationId)
        {
            if (titleText != null)
            {
                titleText.text = "텔레포트";
            }

            if (hintText != null)
            {
                hintText.text = "이동할 곳을 선택하세요.";
            }

            IReadOnlyList<TeleportNetwork.Destination> destinations = TeleportNetwork.Instance.Destinations;
            int used = 0;

            for (int ix = 0; ix < destinations.Count; ++ix)
            {
                TeleportNetwork.Destination destination = destinations[ix];
                if (destination == null || destination.id == currentLocationId)
                {
                    continue;
                }

                UITeleportListItem row = GetRow(used++);
                bool locked = !TeleportNetwork.IsUnlocked(destination);

                // 람다가 반복 변수를 잡지 않도록 지역 변수에 복사한다
                TeleportNetwork.Destination target = destination;
                row.Set(destination.displayName, destination.description, locked, () => OnSelect(target),
                    TeleportNetwork.GetQuestMarker(destination));
                row.gameObject.SetActive(true);
            }

            // 남는 줄은 숨긴다
            for (int ix = used; ix < rows.Count; ++ix)
            {
                rows[ix].gameObject.SetActive(false);
            }
        }

        private UITeleportListItem GetRow(int index)
        {
            while (rows.Count <= index)
            {
                rows.Add(Instantiate(listItemPrefab, listContent));
            }

            return rows[index];
        }

        // 줄을 클릭했을 때. 잠긴 곳은 이유만 안내하고, 갈 수 있는 곳은 창을 닫고 이동한다
        private void OnSelect(TeleportNetwork.Destination destination)
        {
            if (!TeleportNetwork.IsUnlocked(destination))
            {
                UISystemMessage.ShowToastWarning(destination.lockedMessage);
                return;
            }

            if (destination.arrival == null || player == null)
            {
                return;
            }

            Transform movingPlayer = player;
            Close();

            UITeleportScreen.Teleport(movingPlayer, destination.arrival.position, destination.arrival.rotation);
        }
    }
}
