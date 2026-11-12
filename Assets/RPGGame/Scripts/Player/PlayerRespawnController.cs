using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 죽었을 때 페널티를 적용하고 마을에서 부활시킨다. 플레이어 루트에 붙어 있다.
    ///
    /// 흐름: 사망 → 경험치·골드 일부를 잃음 → respawnDelay 동안 사망 모션 → 마을 부활 지점으로 이동 →
    ///       HP/MP 회복·부활 장비 초기화 → 조작 복구(PlayerStateManager.Revive).
    /// 퀘스트 진행도는 QuestManager가 그대로 들고 있으므로 건드리지 않는다 (죽어도 이어서 진행된다).
    /// 엔딩 뒤 "마을로 이동"처럼 사망과 무관하게 마을로 보낼 때도 쓴다 (ReturnToVillage).
    /// </summary>
    public class PlayerRespawnController : MonoBehaviour
    {
        [Tooltip("마을 부활 지점")]
        [SerializeField] private Transform respawnPoint;
        [Tooltip("사망 모션을 보여 주는 시간(초). 이 시간이 지나면 마을로 옮긴다")]
        [SerializeField] private float respawnDelay = 3f;

        [Header("부활 연출")]
        [Tooltip("마을에서 눈을 뜰 때 그 자리에 한 번 재생할 이펙트 (비우면 연출 없음)")]
        [SerializeField] private GameObject respawnEffectPrefab;
        [Tooltip("이펙트를 지우기까지의 시간(초). 파티클 재생 시간보다 길게 잡는다")]
        [SerializeField] private float respawnEffectLifeTime = 4f;

        [Header("사망 페널티")]
        [Tooltip("현재 레벨에서 쌓은 경험치 중 잃는 비율 (레벨이 내려가지는 않는다)")]
        [SerializeField, Range(0f, 1f)] private float expLossRatio = 0.1f;
        [Tooltip("가진 골드 중 잃는 비율")]
        [SerializeField, Range(0f, 1f)] private float goldLossRatio = 0.1f;

        private PlayerStateManager stateManager;
        private HPController hpController;
        private MPController mpController;
        private PlayerLevelController levelController;
        private PlayerEquipment playerEquipment;
        private Transform refTransform;
        private bool isRespawning;

        private void Awake()
        {
            refTransform = transform;
            stateManager = GetComponent<PlayerStateManager>();
            hpController = GetComponentInChildren<HPController>();
            mpController = GetComponentInChildren<MPController>();
            playerEquipment = GetComponent<PlayerEquipment>();
            levelController = FindAnyObjectByType<PlayerLevelController>();

            if (hpController != null)
            {
                hpController.SubscribeOnDead(OnPlayerDead);
            }
        }

        private void OnPlayerDead()
        {
            // 사망 이벤트가 두 번 들어와도 페널티가 두 번 적용되지 않게 막는다
            if (isRespawning)
            {
                return;
            }

            isRespawning = true;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            ApplyDeathPenalty();

            // 사망 모션을 보여준 뒤 마을로 보낸다
            yield return new WaitForSeconds(respawnDelay);

            MoveToRespawnPoint();
            RestoreState();
            PlayRespawnEffect();

            UISystemMessage.ShowToast("마을에서 눈을 떴다.");

            isRespawning = false;
        }

        // 부활 지점에서 이펙트를 재생한다. 플레이어를 따라다니지 않고 눈을 뜬 자리에 남는다
        private void PlayRespawnEffect()
        {
            if (respawnEffectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(respawnEffectPrefab, refTransform.position, Quaternion.identity);
            Destroy(effect, respawnEffectLifeTime);
        }

        // 경험치와 골드를 잃고, 잃은 양을 안내한다
        private void ApplyDeathPenalty()
        {
            float lostExp = 0f;
            int lostGold = 0;

            if (levelController != null)
            {
                lostExp = levelController.LoseExp(levelController.ExpInCurrentLevel * expLossRatio);
            }

            if (CurrencyManager.Instance != null)
            {
                lostGold = CurrencyManager.Instance.LoseGold(
                    Mathf.RoundToInt(CurrencyManager.Instance.Gold * goldLossRatio));
            }

            // 모달로 띄우면 부활 직후 확인 버튼을 누를 때까지 카메라 회전이 막혀 조작이 잠긴 것처럼 느껴진다
            UISystemMessage.ShowToastWarning($"쓰러졌다... 경험치 {lostExp:0}, 골드 {lostGold}을(를) 잃었다.");
        }

        // 로딩 연출 없이 즉시 마을 부활 지점으로 옮기는 함수 (이어하기에서 저장된 위치를 쓸 수 없을 때 등)
        public void MoveToRespawnPointNow()
        {
            MoveToRespawnPoint();
        }

        private void MoveToRespawnPoint()
        {
            if (respawnPoint == null)
            {
                return;
            }

            // CharacterController를 잠시 끄고 옮기는 절차는 Util에 모여 있다 (텔레포트/보스 리셋과 공용)
            Util.TeleportCharacter(refTransform, respawnPoint.position, respawnPoint.rotation);

            // 카메라가 마을까지 수백 미터를 서서히 따라오지 않도록 즉시 붙인다
            CameraRig.SnapToTarget();
        }

        // 사망과 무관하게 플레이어를 마을 부활 지점으로 보내는 함수 (엔딩 후 "마을로 이동" 등).
        // 이동은 텔레포트 로딩 화면을 거치며, 호출 전에 Time.timeScale이 1이어야 연출이 진행된다
        public void ReturnToVillage()
        {
            if (respawnPoint == null)
            {
                return;
            }

            UITeleportScreen.Teleport(refTransform, respawnPoint.position, respawnPoint.rotation);
        }

        private void RestoreState()
        {
            // 마을에서 되살아났으니 장비의 사망 시 부활 효과도 다시 쓸 수 있다
            if (playerEquipment != null)
            {
                playerEquipment.ResetRevive();
            }

            if (hpController != null)
            {
                hpController.OnHealed(hpController.MaxHP);
            }

            if (mpController != null)
            {
                mpController.RestoreMP(float.MaxValue);
            }

            // 상태 복구는 마지막에 한다. 조작이 돌아오는 시점과 위치 이동이 어긋나지 않게 하기 위함
            if (stateManager != null)
            {
                stateManager.Revive();
            }
        }
    }
}
