using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 개발·테스트용 도구. 인스펙터에서 값을 고치고 버튼(커스텀 에디터)을 눌러 쓴다.
    /// 기능별로 파일을 나눈 partial 클래스다.
    ///  - GameTestTools.cs        : 플레이어 체력·무적, 보스전 (이 파일)
    ///  - GameTestTools.Player.cs : 레벨·경험치·포인트·MP·스킬
    ///  - GameTestTools.Items.cs  : 골드·강화석·아이템·장비 지급/착용/강화
    ///  - GameTestTools.Quest.cs  : 퀘스트를 어느 단계로든 클리어/초기화
    ///  - GameTestTools.World.cs  : 이동·몬스터 정리·게임 속도
    /// 화면 구성은 Editor/GameTestToolsEditor.cs가 맡는다.
    /// 에디터나 개발 빌드에서만 동작한다 (릴리스 빌드에서는 아무 일도 하지 않는다)
    /// </summary>
    public partial class GameTestTools : MonoBehaviour
    {
        public enum BossType { DrekMalos, Zyrathon }

        [SerializeField, Min(1f)] private float playerMaxHP = 1000f;
        // 현재 체력 비율. 버튼으로 적용한다
        [SerializeField, Range(0.01f, 1f)] private float playerHPRatio = 1f;
        [Tooltip("켜면 레벨업·장비 교체로 최대 체력이 다시 계산돼도 위의 최대 체력 값으로 되돌려 놓는다")]
        [SerializeField] private bool keepPlayerMaxHP;
        [Tooltip("켜면 체력이 줄어도 매 프레임 최대치로 되돌린다. 한 번에 최대 체력 이상을 맞으면 죽을 수 있으니 최대 체력도 넉넉히 올려 둔다")]
        [SerializeField] private bool godMode;

        [SerializeField] private BossType bossType = BossType.DrekMalos;
        [SerializeField, Range(1, 5)] private int bossLevel = 3;
        // 보스가 이 체력 비율로 시작한다. 50% 이하로 두면 2페이즈부터 바로 확인할 수 있다
        [SerializeField, Range(0.05f, 1f)] private float bossStartHPRatio = 1f;
        // 보스를 기준으로 플레이어를 이 거리만큼 떨어뜨려 놓는다
        [SerializeField, Min(2f)] private float playerStartDistance = 10f;
        [SerializeField] private bool skipEncounterDialogue = true;
        [SerializeField] private bool healPlayerOnStart = true;
        // '보스 체력 깎기' 버튼이 한 번에 깎는 양 (최대 체력 대비 비율). 0.5면 최대 체력의 절반을 깎아
        // 가득 찬 보스는 50%가 되어 2페이즈로 넘어간다. 눌러 둘수록 계속 깎이며 최소 1은 남긴다
        [SerializeField, Range(0.05f, 0.95f)] private float bossHPCutRatio = 0.5f;

        private HPController playerHP;
        private PlayerStateManager playerState;

        private static bool IsAllowed => Application.isEditor || Debug.isDebugBuild;

        private void Update()
        {
            if (!IsAllowed || !EnsurePlayer() || playerState.IsPlayerDead)
            {
                return;
            }

            if (keepPlayerMaxHP && !Mathf.Approximately(playerHP.MaxHP, playerMaxHP))
            {
                playerHP.SetMaxHP(playerMaxHP, false);
            }

            KeepMP();

            if (!godMode)
            {
                return;
            }

            if (playerHP.CurrentHP < playerHP.MaxHP)
            {
                playerHP.SetCurrentHP(playerHP.MaxHP);
            }
        }

        // 최대 체력과 현재 체력 비율을 적용한다.
        // 장비·레벨이 바뀌면 PlayerStats가 최대 체력을 다시 계산하므로, 유지하려면 keepPlayerMaxHP를 켠다
        public void ApplyPlayerHP()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            playerHP.SetMaxHP(playerMaxHP, false);
            playerHP.SetCurrentHP(playerMaxHP * playerHPRatio);
        }

        public void HealPlayerFull()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            playerHP.SetCurrentHP(playerHP.MaxHP);
        }

        // 기존 보스를 치우고 새로 소환한 뒤 플레이어를 보스 앞으로 옮긴다
        public void StartBossFight()
        {
            if (!IsAllowed || !EnsurePlayer())
            {
                return;
            }

            RemoveBoss();

            GameObject boss = MonsterSpawner.SpawnBoss(bossType.ToString(), bossLevel);
            if (boss == null)
            {
                Debug.LogWarning($"보스를 소환하지 못했습니다: {bossType}");
                return;
            }

            BossStateManager bossManager = boss.GetComponent<BossStateManager>();
            if (skipEncounterDialogue)
            {
                bossManager.SkipEncounterDialogue();
            }

            HPController bossHP = boss.GetComponentInChildren<HPController>();
            if (bossHP != null && bossStartHPRatio < 1f)
            {
                bossHP.SetCurrentHP(bossHP.MaxHP * bossStartHPRatio);
            }

            // 아레나 입구(북쪽)에서 들어오는 방향으로, 보스를 바라보는 위치에 세운다.
            // 남쪽에는 봉인 왕좌가 있어 그쪽에 세우면 카메라가 왕좌 모델 안으로 파고든다
            Vector3 position = boss.transform.position + new Vector3(0f, 0.5f, playerStartDistance);
            Util.TeleportCharacter(playerState.transform, position, Quaternion.LookRotation(Vector3.back));
            CameraRig.SnapToTarget();
            // 카메라도 보스 쪽(남쪽)을 보게 한다. 그러지 않으면 보스가 카메라 뒤에 있어 아무것도 안 보인다
            CameraRig.SetYaw(180f);

            if (healPlayerOnStart)
            {
                HealPlayerFull();
            }
        }

        // 보스를 즉시 쓰러뜨린다 (사망 연출·보상·퀘스트 진행 확인용)
        public void KillBoss()
        {
            if (!IsAllowed)
            {
                return;
            }

            foreach (BossStateManager boss in FindObjectsByType<BossStateManager>())
            {
                if (!boss.IsDead)
                {
                    boss.GetComponentInChildren<HPController>()?.Die();
                }
            }
        }

        // 지금 싸우는 보스의 체력을 bossHPCutRatio만큼 깎는다. 다시 소환하지 않고도 전투 도중에 바로 2페이즈를 볼 수 있다.
        // 페이즈 전환은 BossStateManager가 매 프레임 체력 비율을 보고 알아서 처리한다
        public void CutBossHP()
        {
            if (!IsAllowed)
            {
                return;
            }

            bool found = false;

            foreach (BossStateManager boss in FindObjectsByType<BossStateManager>())
            {
                HPController bossHP = boss.IsDead ? null : boss.GetComponentInChildren<HPController>();
                if (bossHP == null)
                {
                    continue;
                }

                // 죽지 않게 최소 1은 남긴다 (0이 되면 사망 연출이 시작된다)
                float target = Mathf.Max(1f, bossHP.CurrentHP - bossHP.MaxHP * bossHPCutRatio);
                bossHP.SetCurrentHP(target);
                found = true;

                Debug.Log($"[GameTestTools] {boss.name} 체력 {bossHP.CurrentHP:0}/{bossHP.MaxHP:0} ({bossHP.HPRatio * 100f:0}%)");
            }

            if (!found)
            {
                Debug.LogWarning("[GameTestTools] 체력을 깎을 보스가 없습니다. 먼저 보스전을 시작하세요.");
            }
        }

        // 필드에 있는 보스와 남은 공격 예고를 모두 지운다
        public void RemoveBoss()
        {
            if (!IsAllowed)
            {
                return;
            }

            foreach (BossStateManager boss in FindObjectsByType<BossStateManager>())
            {
                Destroy(boss.gameObject);
            }

            BossTelegraph.ClearAll();
        }

        private bool EnsurePlayer()
        {
            if (playerHP != null && playerState != null)
            {
                return true;
            }

            playerState = FindAnyObjectByType<PlayerStateManager>();
            playerHP = playerState != null ? playerState.GetComponentInChildren<HPController>() : null;

            return playerHP != null;
        }
    }
}
