using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 구역 밖으로 떨어졌을 때를 대비한 마지막 안전장치. 플레이어 루트에 붙어 있다.
    ///
    /// 구역은 테두리 언덕과 투명 벽으로 막혀 있지만, 만약 뚫리더라도 끝없이 떨어지지 않도록
    /// "마지막으로 땅을 밟았던 위치"를 기억해 두었다가 그보다 많이 아래로 내려가면 그 자리로 되돌린다.
    /// 구역마다 바닥 높이가 달라도 동작하도록 절대 높이가 아니라 마지막 안전 위치와의 차이로 판단한다.
    /// </summary>
    public class PlayerFallGuard : MonoBehaviour
    {
        [Tooltip("마지막 안전 위치보다 이만큼(m) 아래로 내려가면 떨어진 것으로 본다")]
        [SerializeField] private float fallDistance = 40f;

        private CharacterController characterController;
        private Vector3 lastSafePosition;
        private bool hasSafePosition;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            // 텔레포트 중에는 위치가 크게 바뀌므로 기록만 갱신하고 판정하지 않는다
            if (UITeleportScreen.IsOn)
            {
                RememberSafePosition();
                return;
            }

            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            if (characterController.isGrounded || !hasSafePosition)
            {
                RememberSafePosition();
                return;
            }

            if (transform.position.y < lastSafePosition.y - fallDistance)
            {
                Util.TeleportCharacter(transform, lastSafePosition + Vector3.up * 0.5f, transform.rotation);
                Debug.LogWarning("[PlayerFallGuard] 구역 밖으로 떨어져 마지막 안전 위치로 되돌렸습니다.");
            }
        }

        private void RememberSafePosition()
        {
            lastSafePosition = transform.position;
            hasSafePosition = true;
        }
    }
}
