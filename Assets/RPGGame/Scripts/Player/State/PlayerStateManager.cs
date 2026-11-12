using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 플레이어 상태(대기·이동·구르기·공격·사망)를 관리하는 중심 컴포넌트. 플레이어 루트(Kachujin)에 붙어 있다.
    ///
    /// 상태마다 PlayerStateBase를 상속한 컴포넌트가 따로 있고(PlayerIdleState 등), 지금 상태의 컴포넌트만 켜 둔다.
    /// 매 프레임 입력을 보고 상태를 고른다: 공격 입력 → 공격(콤보 예약), 이동 입력 → 이동, 없으면 대기.
    /// 스킬 시전·대쉬 중에는 입력을 무시하고, 사망하면 Revive가 불릴 때까지 상태가 바뀌지 않는다.
    ///
    /// 연결:
    ///  - OnStateChanged → PlayerAnimationController.OnStateChanged (인스펙터 연결, 애니메이터 State 값)
    ///  - PlayerLevelController.OnLevelUP → OnPlayerLevelUP (인스펙터 연결)
    ///  - HPController 사망 → OnPlayerDead,  PlayerAttackState 공격 끝 → OnAttackEnd (코드에서 구독)
    /// </summary>
    public class PlayerStateManager : MonoBehaviour
    {
        /// <summary>플레이어 상태. 정수 값이 애니메이터의 State 파라미터와 states 배열 번호로 쓰인다.</summary>
        public enum State { None = -1, Idle, Move, Roll, Attack, Dead, Length }

        /// <summary>
        /// 기본 공격 콤보 단계. 정수 값 + 1이 애니메이터 상태 이름 AttackCombo{N}의 N이다.
        /// 콤보 수를 바꿀 때는 이 enum과 애니메이터의 AttackCombo{N} 상태만 맞추면 된다.
        /// </summary>
        public enum AttackCombo { None = -1, Combo1, Combo2, Combo3, Length }

        // 콤보 단계별 애니메이터 상태 이름 해시 ("AttackCombo1" ...). 클릭마다 문자열을 만들지 않도록 미리 구해 둔다
        private static readonly int[] ComboStateHashes = BuildComboStateHashes();

        [Tooltip("State 순서(Idle, Move, Roll, Attack, Dead)대로 넣은 상태 컴포넌트")]
        [SerializeField] private PlayerStateBase[] states;

        [Tooltip("상태가 바뀔 때마다 발행 (PlayerAnimationController.OnStateChanged가 연결돼 있다)")]
        [SerializeField] private UnityEvent<State> OnStateChanged;

        private State state = State.None;
        private PlayerData data;
        private PlayerAnimationController animationController;
        private WeaponController weaponController;
        // 스킬 시전·대쉬 중에는 이동·공격 입력을 막기 위해 참조한다
        private PlayerSkillController skillController;
        private PlayerDashController dashController;
        private PlayerStats playerStats;

        /// <summary>다음에 이어질 콤보 단계 (공격 애니메이션 이벤트 ComboCheck가 애니메이터에 넘긴다).</summary>
        public AttackCombo NextAttackCombo { get; private set; } = AttackCombo.None;

        /// <summary>현재 레벨의 기본 수치 (Player Data 에셋의 레벨 표 한 줄).</summary>
        public PlayerData.LevelData CurrentLevelData { get; private set; }

        public State CurrentState => state;
        public bool IsPlayerDead => state == State.Dead;

        private void Awake()
        {
            data = DataManager.Instance.playerData;
            CurrentLevelData = data.levels[0];

            animationController = GetComponentInChildren<PlayerAnimationController>();
            weaponController = GetComponentInChildren<WeaponController>();
            skillController = GetComponent<PlayerSkillController>();
            dashController = GetComponent<PlayerDashController>();
            playerStats = GetComponent<PlayerStats>();

            PlayerAttackState attackState = GetComponent<PlayerAttackState>();
            if (attackState != null)
            {
                attackState.SubscribeOnAttackEnd(OnAttackEnd);
            }

            // 상태 컴포넌트에 이동 속도 등 공용 데이터를 넘긴다
            foreach (PlayerStateBase stateComponent in states)
            {
                stateComponent.SetData(data);
            }

            // 최대 HP/MP는 PlayerStats가 최종 스탯을 계산해 넣으므로 여기서는 사망만 구독한다
            HPController hpController = GetComponentInChildren<HPController>();
            if (hpController != null)
            {
                hpController.SubscribeOnDead(OnPlayerDead);
            }
        }

        private void Update()
        {
            // 사망·구르기·스킬 시전·대쉬 중에는 입력으로 상태를 바꾸지 않는다
            // (상태가 None이면 아직 스폰 연출 중이다. 스폰 애니메이션이 끝나면 PlayerSpawnSMB가 Idle로 바꾼다)
            if (IsPlayerDead || state == State.None || state == State.Roll)
            {
                return;
            }

            if ((skillController != null && skillController.IsCasting) ||
                (dashController != null && dashController.IsDashing))
            {
                return;
            }

            if (InputManager.IsAttack && weaponController.IsWeaponAttached)
            {
                HandleAttackInput();
                return;
            }

            // 공격 중에는 애니메이션이 끝날 때(AttackEnd 이벤트)까지 상태를 유지한다
            if (state == State.Attack)
            {
                return;
            }

            if ((state == State.Idle || state == State.Move) && InputManager.IsRoll)
            {
                SetState(State.Roll);
                return;
            }

            SetState(InputManager.Movement == Vector2.zero ? State.Idle : State.Move);
        }

        // 공격 입력: 공격 중이 아니면 1타를 시작하고, 공격 중이면 지금 콤보의 다음 단계를 예약한다
        private void HandleAttackInput()
        {
            if (state != State.Attack)
            {
                NextAttackCombo = AttackCombo.Combo1;
                SetState(State.Attack);
                animationController.SetAttackComboState((int)NextAttackCombo);
                return;
            }

            NextAttackCombo = GetNextAttackCombo(animationController.GetCurrentStateInfo());

            // ComboCheck 애니메이션 이벤트(클립 25% 지점)까지 기다리지 않고 클릭한 프레임에 바로 애니메이터에 넘긴다.
            // 이벤트 때만 넘기면 25% 이후에 누른 클릭은 콤보로 이어지지 않는다
            if (NextAttackCombo != AttackCombo.None)
            {
                animationController.SetAttackComboState((int)NextAttackCombo);
            }
        }

        // 지금 재생 중인 공격 애니메이션을 보고 다음 콤보 단계를 정한다.
        // 마지막 콤보이거나 공격 애니메이션이 아니면 None을 돌려줘 콤보를 끊는다
        private static AttackCombo GetNextAttackCombo(AnimatorStateInfo currentAnimationState)
        {
            // 마지막 단계는 다음이 없으므로 검사에서 뺀다
            for (int ix = 0; ix < ComboStateHashes.Length - 1; ++ix)
            {
                if (currentAnimationState.shortNameHash == ComboStateHashes[ix])
                {
                    return (AttackCombo)(ix + 1);
                }
            }

            return AttackCombo.None;
        }

        private static int[] BuildComboStateHashes()
        {
            int[] hashes = new int[(int)AttackCombo.Length];
            for (int ix = 0; ix < hashes.Length; ++ix)
            {
                hashes[ix] = Animator.StringToHash($"AttackCombo{ix + 1}");
            }

            return hashes;
        }

        // ───────────────────────── 상태 전환 ─────────────────────────

        /// <summary>상태를 바꾼다. 사망 상태에서는 Revive 전까지 바뀌지 않는다.</summary>
        public void SetState(State newState)
        {
            if (state == newState || IsPlayerDead)
            {
                return;
            }

            if (state != State.None)
            {
                states[(int)state].enabled = false;
            }

            states[(int)newState].enabled = true;
            state = newState;
            OnStateChanged?.Invoke(state);
        }

        private void OnAttackEnd()
        {
            NextAttackCombo = AttackCombo.None;
            SetState(State.Idle);
            animationController.SetAttackComboState((int)NextAttackCombo);
        }

        public void OnPlayerDead()
        {
            SetState(State.Dead);
        }

        /// <summary>
        /// 부활시킨다 (PlayerRespawnController가 부른다). SetState는 사망 상태에서 바로 돌아가므로 상태를 직접 되돌린다.
        /// 이 경로가 없으면 HP를 채워도 사망 상태에 갇혀 조작이 돌아오지 않는다.
        /// </summary>
        public void Revive()
        {
            if (state == State.Dead)
            {
                states[(int)State.Dead].enabled = false;
            }

            state = State.None;
            NextAttackCombo = AttackCombo.None;

            SetState(State.Idle);
            animationController.SetAttackComboState((int)AttackCombo.None);
        }

        /// <summary>
        /// 레벨이 올랐을 때 (PlayerLevelController.OnLevelUP에 인스펙터로 연결돼 있다. 이름과 매개변수를 바꾸지 않는다).
        /// 최대 HP/MP/공격력 등은 PlayerStats가 장비·버프까지 합쳐 다시 계산한다.
        /// </summary>
        public void OnPlayerLevelUP(int newLevel)
        {
            int level = Mathf.Clamp(newLevel, 1, data.levels.Count);
            CurrentLevelData = data.levels[level - 1];

            if (playerStats != null)
            {
                playerStats.SetLevel(level);
            }
        }
    }
}
