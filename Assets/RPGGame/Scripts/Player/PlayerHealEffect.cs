using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 체력이 회복될 때 초록 파티클과 "+회복량" 숫자를 띄우는 연출. 플레이어 루트에 붙어 있다.
    /// HPController의 회복 이벤트(SubscribeOnHealed)를 구독한다 (물약·레벨업·부활 등 모든 회복).
    /// </summary>
    public class PlayerHealEffect : MonoBehaviour
    {
        [Tooltip("회복 이벤트를 받을 HP 컨트롤러. 비워 두면 자식에서 찾는다")]
        [SerializeField] private HPController hpController;
        [Tooltip("플레이어를 따라다니는 회복 파티클")]
        [SerializeField] private GameObject effectPrefab;
        [Tooltip("머리 위에 떠오르는 회복량 숫자")]
        [SerializeField] private UIFloatingText floatingTextPrefab;

        [Header("설정")]
        [Tooltip("파티클 위치 (플레이어 기준)")]
        [SerializeField] private Vector3 effectOffset = Vector3.zero;
        [Tooltip("파티클을 지우기까지의 시간(초)")]
        [SerializeField] private float effectLifeTime = 2f;
        [Tooltip("숫자가 뜨는 높이(m, 머리 위)")]
        [SerializeField] private float textHeight = 2.1f;
        [SerializeField] private Color healColor = new Color(0.44f, 1f, 0.45f);

        private Transform refTransform;

        private void Awake()
        {
            refTransform = transform;

            if (hpController == null)
            {
                hpController = GetComponentInChildren<HPController>();
            }
        }

        private void Start()
        {
            if (hpController != null)
            {
                hpController.SubscribeOnHealed(OnHealed);
            }
        }

        private void OnHealed(float amount)
        {
            SpawnEffect();
            SpawnText(amount);
        }

        // 파티클은 플레이어를 따라다녀야 하므로 자식으로 붙이고, 수명이 지나면 지운다
        private void SpawnEffect()
        {
            if (effectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(effectPrefab, refTransform.position + effectOffset, Quaternion.identity, refTransform);
            Destroy(effect, effectLifeTime);
        }

        // 숫자는 제자리에서 떠올라야 하므로 부모를 두지 않는다 (플레이어가 움직여도 그 자리에 남는다)
        private void SpawnText(float amount)
        {
            if (floatingTextPrefab == null)
            {
                return;
            }

            UIFloatingText floating = Instantiate(floatingTextPrefab, refTransform.position + Vector3.up * textHeight, Quaternion.identity);
            floating.Setup($"+{Mathf.FloorToInt(amount)}", healColor);
        }
    }
}
