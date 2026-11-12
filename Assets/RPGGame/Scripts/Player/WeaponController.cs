using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 플레이어가 손에 든 무기를 관리하는 컴포넌트. 플레이어 아래 "Weapon Controller" 오브젝트에 붙어 있다.
    ///
    /// 하는 일:
    ///  - 게임 시작 때 기본 무기 프리팹(Weapon Sword14)을 손 뼈(weaponHolder)에 만들어 붙인다
    ///  - 장착한 무기 장비가 바뀌거나 강화되면 손에 보이는 모델과 강화 빛(WeaponEnhanceGlow)을 다시 맞춘다
    ///  - 공격 콤보마다 칼 궤적 이펙트와 휘두르는 소리를 낸다 (PlayerAttackComboSMB가 부른다)
    ///  - 공격 상태(PlayerAttackState)의 판정 시작/끝 알림을 무기(Weapon)에 연결한다
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Tooltip("무기를 붙일 손 뼈")]
        [SerializeField] private Transform weaponHolder;
        [Tooltip("게임 시작 때 손에 만들 무기 프리팹")]
        [SerializeField] private Weapon defaultWeaponPrefab;
        [Tooltip("콤보 단계별 칼 궤적 이펙트 (PlayerAttackComboSMB의 effectIndex 순서)")]
        [SerializeField] private PlayerAttackEffect[] attackComboEffects;
        [Tooltip("휘두르는 소리. 비워 두면 자식에서 찾는다")]
        [SerializeField] private AudioPlayer swingSound;

        private Weapon weapon;
        private PlayerEquipment playerEquipment;
        private WeaponEnhanceGlow enhanceGlow;

        public bool IsWeaponAttached { get; private set; }
        public Weapon EquippedWeapon => weapon;

        private void Start()
        {
            EquipDefaultWeapon();

            // 장착한 무기 장비에 따라 손에 든 모델을 바꾼다.
            // PlayerEquipment의 실행 순서가 더 빨라(-55) 이 시점에는 장착 상태가 이미 정해져 있다
            playerEquipment = transform.root.GetComponentInChildren<PlayerEquipment>();
            if (playerEquipment != null)
            {
                playerEquipment.SubscribeOnEquipmentChanged(ApplyEquippedWeaponModel);
            }

            // 강화 단계가 바뀔 때도 강화 빛을 다시 맞춘다
            EnhancementManager.Instance?.SubscribeOnEnhancementChanged(ApplyEquippedWeaponModel);

            ApplyEquippedWeaponModel();
        }

        // 기본 무기를 손에 만들어 붙인다. 이미 들고 있거나 프리팹이 없으면 아무것도 하지 않는다
        private void EquipDefaultWeapon()
        {
            if (IsWeaponAttached || defaultWeaponPrefab == null)
            {
                return;
            }

            // 부모를 지정해 만들어, 씬 루트에 잠깐 생겼다가 옮겨지는 비용을 없앤다
            Weapon spawnedWeapon = Instantiate(defaultWeaponPrefab, weaponHolder);
            AttachWeapon(spawnedWeapon);
        }

        private void AttachWeapon(Weapon newWeapon)
        {
            newWeapon.Attach(weaponHolder);
            weapon = newWeapon;
            IsWeaponAttached = true;

            if (swingSound == null)
            {
                swingSound = GetComponentInChildren<AudioPlayer>();
            }

            PlayerAttackState playerAttackState = transform.root.GetComponentInChildren<PlayerAttackState>();
            if (playerAttackState != null)
            {
                playerAttackState.SubscribeOnAttackBegin(newWeapon.OnAttackBegin);
                playerAttackState.SubscribeOnAttackCheckEnd(newWeapon.OnAttackEnd);
            }
        }

        // 장착 중인 무기 장비의 외형을 손에 든 무기에 입히고(없으면 기본 모델), 강화 단계에 맞는 빛을 입힌다
        private void ApplyEquippedWeaponModel()
        {
            if (weapon == null || playerEquipment == null)
            {
                return;
            }

            EquipmentInstance equipped = playerEquipment.GetEquipped(EquipmentItem.EquipType.Weapon);
            weapon.SetModel(equipped != null ? equipped.item.weaponModelPrefab : null);

            if (enhanceGlow == null)
            {
                enhanceGlow = weapon.GetComponent<WeaponEnhanceGlow>();
                if (enhanceGlow == null)
                {
                    enhanceGlow = weapon.gameObject.AddComponent<WeaponEnhanceGlow>();
                }
            }

            int enhanceLevel = equipped != null ? equipped.enhanceLevel : 0;
            enhanceGlow.Refresh(weapon.CurrentModel, enhanceLevel);
        }

        // ───────────────────────── 공격 이펙트 ─────────────────────────

        /// <summary>콤보 단계의 칼 궤적과 휘두르는 소리를 낸다 (PlayerAttackComboSMB가 부른다).</summary>
        public void PlayAttackComboEffect(int comboIndex, float soundDelay = 0f, float animationSpeed = 1f)
        {
            if (comboIndex < 0 || comboIndex >= attackComboEffects.Length)
            {
                return;
            }

            // 애니메이션이 빨라진 만큼 이펙트·소리 지연도 줄여야 베는 순간과 어긋나지 않는다
            float timeScale = Mathf.Max(0.1f, animationSpeed);
            attackComboEffects[comboIndex].Activate(timeScale);

            if (swingSound != null)
            {
                swingSound.Play(soundDelay / timeScale);
            }
        }

        /// <summary>재생 중인 칼 궤적을 모두 끈다 (대쉬·스킬로 공격이 끊길 때).</summary>
        public void CancelAttackEffects()
        {
            foreach (PlayerAttackEffect effect in attackComboEffects)
            {
                if (effect != null && effect.gameObject.activeSelf)
                {
                    effect.Cancel();
                }
            }
        }
    }
}
