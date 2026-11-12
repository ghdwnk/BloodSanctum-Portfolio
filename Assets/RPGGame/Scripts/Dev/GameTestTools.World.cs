using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// GameTestTools - 이동·몬스터 정리·게임 속도
    /// </summary>
    public partial class GameTestTools
    {
        [Tooltip("게임 진행 속도 배율. 1이 보통이다. 바꾸면 곧바로 적용된다")]
        [SerializeField, Range(0.1f, 5f)] private float gameSpeed = 1f;

        // 이 도구로 속도를 바꿨는지. 바꾼 적이 있을 때만 꺼질 때 1배속으로 되돌린다 (다른 곳의 일시정지를 건드리지 않기 위해)
        private bool gameSpeedChanged;

        private void OnDisable()
        {
            if (gameSpeedChanged)
            {
                Time.timeScale = 1f;
                gameSpeedChanged = false;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && IsAllowed && gameSpeedChanged)
            {
                Time.timeScale = gameSpeed;
            }
        }

        public void ApplyGameSpeed()
        {
            if (!IsAllowed)
            {
                return;
            }

            Time.timeScale = gameSpeed;
            gameSpeedChanged = true;
        }

        public void ResetGameSpeed()
        {
            gameSpeed = 1f;
            Time.timeScale = 1f;
            gameSpeedChanged = false;
        }

        // 마을 부활 지점으로 즉시 이동한다
        public void TeleportToVillage()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            playerState.GetComponent<PlayerRespawnController>()?.MoveToRespawnPointNow();
        }

        // 씬에 있는 지역(ZoneArea)의 중심으로 이동한다. 중심 아래의 지면 높이를 찾아 그 위에 세운다
        public void TeleportToZone(ZoneArea zone)
        {
            if (!IsAllowed || zone == null || !EnsurePlayer())
            {
                return;
            }

            Vector3 position = zone.transform.position;
            if (Util.SampleGroundHeight(position, out float groundY))
            {
                position.y = groundY + 0.5f;
            }

            Util.TeleportCharacter(playerState.transform, position, playerState.transform.rotation);
            CameraRig.SnapToTarget();
        }

        // 보스를 제외한 필드의 모든 몬스터를 즉시 쓰러뜨린다 (드랍·퀘스트 처치 진행 확인용)
        public void KillAllMonsters()
        {
            if (!IsAllowed)
            {
                return;
            }

            int count = 0;
            foreach (MonsterStateManager monster in FindObjectsByType<MonsterStateManager>())
            {
                if (monster.IsDead)
                {
                    continue;
                }

                HPController hp = monster.GetComponentInChildren<HPController>();
                if (hp != null)
                {
                    hp.Die();
                    ++count;
                }
            }

            Debug.Log($"[GameTestTools] 몬스터 {count}마리를 처치했습니다.");
        }
    }
}
