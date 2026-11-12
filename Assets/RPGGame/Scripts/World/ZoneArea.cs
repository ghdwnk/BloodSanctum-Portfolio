using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 지역(마을/평원/던전 등) 하나의 이름과 범위를 나타내는 컴포넌트.
    /// 플레이어가 이 범위에 들어오면 화면에 지역 이름 연출이 뜬다.
    /// 맵을 추가할 때 코드를 고치지 않고 빈 오브젝트에 이 컴포넌트만 붙이면 된다.
    /// </summary>
    public class ZoneArea : MonoBehaviour
    {
        [SerializeField] private string zoneName = "이름 없는 지역";
        // 이 오브젝트를 중심으로 한 상자 크기(로컬 기준). 회전도 그대로 반영된다
        [SerializeField] private Vector3 size = new Vector3(60f, 40f, 60f);
        [Tooltip("영역이 겹칠 때 큰 값이 이긴다. 넓은 평원 안에 마을이 들어 있는 배치를 표현하려면 마을 쪽 값을 크게 준다")]
        [SerializeField] private int priority;
        [Header("성격")]
        [Tooltip("켜면 이 지역 안에서는 몬스터가 플레이어를 쫓지 않는다 (마을 안전지대)")]
        [SerializeField] private bool isSafeZone;
        [Tooltip("사냥터/던전 표시. 배너 문구를 다르게 보여준다")]
        [SerializeField] private bool isDungeon;
        [Tooltip("켜면 이 지역에 있는 동안 BGM이 전투 음악으로 흐른다 (보스 던전/아레나용)")]
        [SerializeField] private bool useBattleMusic;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0.3f, 1f);

        public string ZoneName => zoneName;
        public int Priority => priority;
        public bool IsSafeZone => isSafeZone;
        public bool IsDungeon => isDungeon;
        public bool UseBattleMusic => useBattleMusic;

        // 월드 좌표가 이 지역 안에 있는지 검사한다.
        // 물리 트리거 대신 좌표 계산을 쓰는 이유는, 큰 트리거 콜라이더를 씬에 깔면
        // 다른 트리거 기반 로직(아이템 획득 등)과 얽히고 시작 시점에 진입 이벤트가 발생하지 않기 때문
        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            Vector3 half = size * 0.5f;

            return Mathf.Abs(local.x) <= half.x
                   && Mathf.Abs(local.y) <= half.y
                   && Mathf.Abs(local.z) <= half.z;
        }

#if UNITY_EDITOR
        // 씬 뷰에서 범위를 눈으로 확인하고 조정할 수 있게 한다
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.12f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
        }
#endif
    }
}
