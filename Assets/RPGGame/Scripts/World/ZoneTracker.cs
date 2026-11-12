using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 지금 어느 지역에 있는지 추적한다.
    /// 지역 이름 배너와 안전지대 판정이 모두 이 결과를 쓰기 때문에,
    /// UI가 아니라 별도 컴포넌트에 두고 결과만 나눠 준다.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ZoneTracker : MonoBehaviour
    {
        private static ZoneTracker instance;

        [Tooltip("매 프레임 검사할 필요가 없다. 지역 경계는 수십 미터 단위라 이 정도면 충분하다")]
        [SerializeField] private float checkInterval = 0.3f;
        // 지역이 바뀔 때 발행 (지역 이름 배너가 구독한다)
        [SerializeField] private UnityEvent<ZoneArea> OnZoneChanged = new UnityEvent<ZoneArea>();

        private ZoneArea[] zones;
        private Transform playerTransform;
        private float nextCheckTime;

        public static ZoneArea CurrentZone { get; private set; }
        // 몬스터 상태 머신이 매 프레임 물어보므로 프로퍼티 하나로 싸게 답한다
        public static bool IsPlayerInSafeZone => CurrentZone != null && CurrentZone.IsSafeZone;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            // 씬이 바뀌면 이전 지역이 남아 있으면 안 된다
            CurrentZone = null;
        }

        private void Start()
        {
            // 지역은 씬마다 다르므로 시작할 때 한 번만 모은다
            zones = FindObjectsByType<ZoneArea>();
        }

        private void Update()
        {
            if (Time.time < nextCheckTime || !EnsurePlayer())
            {
                return;
            }

            nextCheckTime = Time.time + checkInterval;

            ZoneArea zone = FindZone(playerTransform.position);
            if (zone == CurrentZone)
            {
                return;
            }

            CurrentZone = zone;
            OnZoneChanged?.Invoke(zone);
        }

        private bool EnsurePlayer()
        {
            if (playerTransform != null)
            {
                return true;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return false;
            }

            playerTransform = player.transform;

            return true;
        }

        // 포함하는 지역 중 우선순위가 가장 높은 것 (넓은 지역 안에 좁은 지역이 겹칠 수 있다)
        private ZoneArea FindZone(Vector3 position)
        {
            ZoneArea best = null;

            foreach (ZoneArea zone in zones)
            {
                if (zone == null || !zone.Contains(position))
                {
                    continue;
                }

                if (best == null || zone.Priority > best.Priority)
                {
                    best = zone;
                }
            }

            return best;
        }

        // 월드 좌표가 속한 지역 이름. 지역 밖이거나 지역 정보가 아직 없으면 빈 문자열
        public static string GetZoneNameAt(Vector3 position)
        {
            if (instance == null || instance.zones == null)
            {
                return string.Empty;
            }

            ZoneArea zone = instance.FindZone(position);
            return zone != null ? zone.ZoneName : string.Empty;
        }

        public void SubscribeOnZoneChanged(UnityAction<ZoneArea> listener)
        {
            OnZoneChanged?.AddListener(listener);
        }
    }
}
