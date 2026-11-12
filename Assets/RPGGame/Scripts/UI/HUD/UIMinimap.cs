using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 화면 우상단 미니맵. 위에서 내려다보는 전용 카메라가 지형을 그리고,
    /// 몬스터·NPC 위치는 월드 좌표를 미니맵 좌표로 바꿔 점(blip)으로 찍는다.
    /// 점을 월드에 두지 않고 UI로 그리는 이유는 레이어/렌더링 설정을 건드리지 않기 위함이다.
    /// </summary>
    public class UIMinimap : MonoBehaviour
    {
        [SerializeField] private Camera minimapCamera;
        [Tooltip("점이 붙는 영역 (미니맵 이미지와 같은 크기)")]
        [SerializeField] private RectTransform blipRoot;
        [SerializeField] private RectTransform playerBlip;
        [SerializeField] private Image blipPrefab;

        [Header("설정")]
        [Tooltip("미니맵에 담을 반경(월드 단위). 카메라 orthographicSize와 같은 값을 쓴다")]
        [SerializeField] private float worldRange = 60f;
        [SerializeField] private float cameraHeight = 60f;
        // 몬스터와 보스는 같은 빨강을 쓰고, 보스는 크기(1.6배)로만 구분한다
        [SerializeField] private Color monsterColor = new Color(0.9f, 0.2f, 0.18f);
        [SerializeField] private Color bossColor = new Color(0.9f, 0.2f, 0.18f);
        [SerializeField] private Color npcColor = new Color(0.3f, 0.6f, 1f);

        private Transform playerTransform;
        // 점은 매 프레임 만들지 않고 재사용한다
        private readonly List<Image> blips = new();

        private void LateUpdate()
        {
            if (!EnsurePlayer())
            {
                return;
            }

            FollowPlayer();
            UpdateBlips();
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

        // 카메라를 플레이어 위로 옮긴다. 회전은 고정(북쪽 위)이라 방향 감각이 흔들리지 않는다
        private void FollowPlayer()
        {
            if (minimapCamera == null)
            {
                return;
            }

            Vector3 position = playerTransform.position;
            position.y += cameraHeight;
            minimapCamera.transform.position = position;
            minimapCamera.orthographicSize = worldRange;

            // 플레이어 표식은 바라보는 방향을 나타낸다
            if (playerBlip != null)
            {
                playerBlip.localRotation = Quaternion.Euler(0f, 0f, -playerTransform.eulerAngles.y);
            }
        }

        private void UpdateBlips()
        {
            int used = 0;

            for (int ix = 0; ix < MonsterStateManager.AliveMonsterCount; ++ix)
            {
                MonsterStateManager monster = MonsterStateManager.GetAliveMonster(ix);

                if (monster == null || monster.IsDead)
                {
                    continue;
                }

                if (TryPlaceBlip(used, monster.transform, monsterColor))
                {
                    ++used;
                }
            }

            foreach (BossStateManager boss in FindObjectsByType<BossStateManager>())
            {
                if (boss.IsDead)
                {
                    continue;
                }

                if (TryPlaceBlip(used, boss.transform, bossColor, 1.6f))
                {
                    ++used;
                }
            }

            foreach (NPCStateManager npc in FindObjectsByType<NPCStateManager>())
            {
                if (TryPlaceBlip(used, npc.transform, npcColor, 1.3f))
                {
                    ++used;
                }
            }

            // 이번 프레임에 쓰지 않은 점은 꺼둔다 (Destroy하지 않고 재사용)
            for (int ix = used; ix < blips.Count; ++ix)
            {
                blips[ix].gameObject.SetActive(false);
            }
        }

        // 월드 위치를 미니맵 좌표로 바꿔 점을 찍는다. 범위 밖이면 찍지 않는다.
        // 표식이 플레이어와 같은 방향 화살표 아이콘이라 위치뿐 아니라 바라보는 방향도 필요해서
        // 좌표 대신 Transform을 받는다
        private bool TryPlaceBlip(int index, Transform target, Color color, float sizeScale = 1f)
        {
            Vector3 offset = target.position - playerTransform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude > worldRange * worldRange)
            {
                return false;
            }

            Image blip = GetBlip(index);
            if (blip == null)
            {
                return false;
            }

            // 미니맵 반지름을 월드 반경에 대응시킨다
            float radius = blipRoot.rect.width * 0.5f;
            Vector2 local = new Vector2(offset.x, offset.z) / worldRange * radius;

            RectTransform rect = blip.rectTransform;
            rect.anchoredPosition = local;
            rect.localScale = Vector3.one * sizeScale;
            // 미니맵은 북쪽 고정이므로 월드 Y회전을 그대로 UI 회전으로 쓴다 (플레이어 표식과 같은 규칙)
            rect.localRotation = Quaternion.Euler(0f, 0f, -target.eulerAngles.y);
            blip.color = color;
            blip.gameObject.SetActive(true);

            return true;
        }

        private Image GetBlip(int index)
        {
            while (blips.Count <= index)
            {
                if (blipPrefab == null || blipRoot == null)
                {
                    return null;
                }

                Image created = Instantiate(blipPrefab, blipRoot);
                created.gameObject.SetActive(false);
                blips.Add(created);
            }

            return blips[index];
        }
    }
}
