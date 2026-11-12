using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 일반 몬스터 한 마리의 중심 컴포넌트. 몬스터 프리팹 루트(Vampire·Nightshade·Skeletonzombie 등)에 붙어 있다.
    ///
    /// 하는 일:
    ///  - 상태 전환: 대기 → 배회 → 추격 → 공격 → 사망. 지금 상태의 컴포넌트(MonsterXxxState)만 켜 둔다
    ///  - 감지: 플레이어가 시야에 들어오면 추격, 시야를 일정 시간 벗어나면 대기로 돌아간다. 맞으면 바로 알아챈다
    ///  - 레벨 적용: EnemyData의 레벨 표에서 최대 HP·방어력·공격력·이름표를 정한다 (스포너가 SetLevel로 부른다)
    ///  - 사망 처리: 처치 수 집계, 경험치 지급, 전리품 드랍, 스포너에 사망 알림
    ///  - 전투 조율: 동시에 플레이어를 때리는 몬스터 수를 제한하고(공격 슬롯), 가까운 몬스터끼리 겹치지 않게 한다
    ///
    /// 플레이어가 죽었거나 마을 안전지대에 있으면 추격·공격 대상으로 삼지 않는다 (IsPlayerUntargetable).
    /// </summary>
    public class MonsterStateManager : MonoBehaviour
    {
        /// <summary>몬스터 상태. 정수 값이 states 배열 번호이자 애니메이터 파라미터로 쓰인다.</summary>
        public enum State { None = -1, Idle, Patrol, Chase, Attack, Dead, Length }

        // 텔레포트로 도착한 직후 이 시간(초) 동안은 몬스터가 플레이어를 노리지 않는다.
        // 도착 지점 근처를 돌던 몬스터가 로딩 직후 바로 달려들어 상황을 파악할 틈이 없는 것을 막는다
        private const float ArrivalGraceTime = 3f;

        // 살아 있는(활성화된) 일반 몬스터 목록. 몬스터끼리 간격을 유지할 때 주변 몬스터를 찾는 데 쓴다.
        // 개체 수가 12~20 수준이라 직접 관리하며 전수 검사하는 편이 물리 질의(Physics.OverlapSphere)를
        // 매 프레임 부르는 것보다 싸고, 레이어 설정에도 기대지 않는다
        private static readonly List<MonsterStateManager> aliveMonsters = new();

        // 지금 플레이어를 때리고 있는(공격 슬롯을 가진) 몬스터. 제한이 없으면 몰려든 몬스터가 모두
        // 공격 사거리 안으로 파고들어 한 덩어리로 겹친다
        private static readonly HashSet<MonsterStateManager> attackers = new();

        /// <summary>동시에 플레이어를 때릴 수 있는 몬스터 수 (MonsterSpawner가 시작할 때 정한다).</summary>
        public static int MaxSimultaneousAttackers = 4;

        /// <summary>일반 몬스터가 죽을 때마다 발행한다. 스포너가 사망 수를 세어 리스폰 시점을 정한다.</summary>
        public static event Action OnAnyMonsterDead;

        [Tooltip("이 몬스터의 종류 데이터 (이름·레벨별 스탯·감지 범위·드랍). 스포너가 종류를 구분하는 데도 쓴다")]
        [SerializeField] private EnemyData data;

        [Tooltip("시작 레벨. 스포너가 생성 직후 SetLevel로 덮어쓴다")]
        [SerializeField] private int level = 1;

        private State state = State.None;
        // State 값을 번호로 쓰는 상태 컴포넌트 표 (Idle, Patrol, Chase, Attack, Dead)
        private MonsterStateBase[] states;
        private Transform refTransform;
        private PlayerStateManager targetPlayerStateManager;
        private PlayerLevelController playerLevelController;
        private UIMonsterNameTag nameTag;
        private HPController hpController;
        private Damageable damageable;
        private MonsterAttackController attackController;
        private Animator refAnimator;

        // 보스 연출(조우·페이즈 대사) 때문에 멈춰 있는지. 소환수처럼 연출 도중에도 살아 있는 몬스터가
        // 플레이어가 조작하지 못하는 동안 계속 때리는 것을 막는다
        private bool isFrozen;

        // 상태가 바뀔 때마다 발행 (MonsterAnimationController가 애니메이터에 전달한다). 코드에서만 구독한다
        private readonly UnityEvent<State> onStateChanged = new();

        // 추격 중 시야를 벗어나 있던 누적 시간. loseSightDuration 이상 연속으로 벗어나야 추격을 푼다
        private float loseSightTimer;
        // 공격받아 얻은 어그로가 끝나는 시각. 이 시각 전에는 시야 판정으로 추격이 풀리지 않는다
        private float aggroEndTime;

        // 자리를 못 잡고 사거리 안까지 들어온 몬스터는 더 밀고 들어가지 않는다.
        // true면 MonsterStateBase가 루트 모션 전진을 적용하지 않는다
        private bool shouldHoldPosition;

        /// <summary>현재 레벨의 능력치 한 줄 (EnemyData 레벨 표).</summary>
        public EnemyData.LevelData CurrentLevelData { get; private set; }

        /// <summary>이 몬스터의 종류 데이터. 스포너가 프리팹 상태에서 몬스터 종류를 알아낼 때 쓴다.</summary>
        public EnemyData EnemyData => data;

        public Transform PlayerTransform { get; private set; }

        /// <summary>
        /// 배회의 중심이 되는 스폰 지점. 현재 위치를 기준으로 목적지를 뽑으면 이동할 때마다 중심이 밀려
        /// 몬스터가 맵 밖까지 흘러가므로 스폰 지점에 고정한다.
        /// </summary>
        public Vector3 HomePosition { get; private set; }

        public bool IsPlayerDead => targetPlayerStateManager.IsPlayerDead;

        /// <summary>플레이어가 죽었거나, 마을 안전지대에 있거나, 막 텔레포트해 도착했으면 추격·공격 대상이 아니다.</summary>
        public bool IsPlayerUntargetable => IsPlayerDead || ZoneTracker.IsPlayerInSafeZone
            || Time.time < UITeleportScreen.LastTeleportEndTime + ArrivalGraceTime;

        /// <summary>죽었는지. 죽은 몬스터는 씬에 그대로 남으므로 스포너가 개체 수를 셀 때 제외하는 데 쓴다.</summary>
        public bool IsDead => state == State.Dead;

        /// <summary>공격 중인지. 피격 모션이 공격 모션을 끊지 않도록 애니메이션 컨트롤러가 참조한다.</summary>
        public bool IsAttacking => state == State.Attack;

        public static int AliveMonsterCount => aliveMonsters.Count;

        public static MonsterStateManager GetAliveMonster(int index)
        {
            return aliveMonsters[index];
        }

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 플레이의 몬스터가 정적 목록에 남지 않게 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            aliveMonsters.Clear();
            attackers.Clear();
            OnAnyMonsterDead = null;
        }

        private void Awake()
        {
            refTransform = transform;

            if (data == null)
            {
                Debug.LogError($"[MonsterStateManager] {name}에 EnemyData가 지정되지 않았습니다.", this);
                enabled = false;
                return;
            }

            CurrentLevelData = data.GetLevelData(level);

            hpController = GetComponentInChildren<HPController>();
            damageable = GetComponentInChildren<Damageable>();
            attackController = GetComponentInChildren<MonsterAttackController>();
            refAnimator = GetComponent<Animator>();
            nameTag = GetComponentInChildren<UIMonsterNameTag>(true);

            // 상태 컴포넌트를 모두 꺼 둔 채 시작하고, OnEnable에서 배회 상태만 켠다
            states = new MonsterStateBase[]
            {
                GetComponent<MonsterIdleState>(),
                GetComponent<MonsterPatrolState>(),
                GetComponent<MonsterChaseState>(),
                GetComponent<MonsterAttackState>(),
                GetComponent<MonsterDeadState>(),
            };

            foreach (MonsterStateBase stateComponent in states)
            {
                stateComponent.enabled = false;
                stateComponent.SetData(data);
            }

            // 스폰 지점을 배회 중심으로 기억한다. 지형보다 높은 곳에서 생성돼 떨어지더라도
            // 목적지는 지면에 맞춰지므로 여기서는 XZ만 의미가 있다
            HomePosition = refTransform.position;

            PlayerTransform = GameObject.FindGameObjectWithTag("Player").transform.root;
            targetPlayerStateManager = PlayerTransform.GetComponent<PlayerStateManager>();

            if (hpController != null)
            {
                hpController.SubscribeOnDead(OnMonsterDead);
            }

            // 시야 밖에서 공격당해도 바로 반응하도록 피격 이벤트를 구독한다.
            // 인스펙터에 연결하지 않고 코드로 붙여, 새 몬스터 프리팹에서도 자동으로 동작하게 한다
            if (damageable != null)
            {
                damageable.SubscribeOnDamageReceived(OnAttacked);
            }

            // 넉백·피격 반짝임도 프리팹을 일일이 고치지 않아도 모든 일반 몬스터에 적용되도록 코드로 붙인다.
            // 보스는 이 컴포넌트를 쓰지 않으므로 자연히 대상에서 빠진다
            if (GetComponent<MonsterKnockback>() == null)
            {
                gameObject.AddComponent<MonsterKnockback>();
            }

            if (GetComponent<MonsterHitFlash>() == null)
            {
                gameObject.AddComponent<MonsterHitFlash>();
            }

            ApplyLevelData();
        }

        private void OnEnable()
        {
            if (states == null)
            {
                return;
            }

            if (!aliveMonsters.Contains(this))
            {
                aliveMonsters.Add(this);
            }

            // 기본 동작을 제자리 대기가 아니라 배회로 둔다. 생성 직후부터 돌아다니다가
            // 플레이어를 발견하면 추격으로 넘어간다
            SetState(State.Patrol);
        }

        private void OnDisable()
        {
            // 멈춘 채 꺼지면(풀링·리셋) 다음에 켜졌을 때 애니메이터가 멈춰 있지 않게 되돌려 둔다
            if (isFrozen)
            {
                isFrozen = false;

                if (refAnimator != null)
                {
                    refAnimator.speed = 1f;
                }
            }

            aliveMonsters.Remove(this);
            // 슬롯을 쥔 채 사라지면 그 자리가 영영 비지 않는다
            ReleaseAttackSlot();
        }

        // ───────────────────────── 레벨 ─────────────────────────

        /// <summary>
        /// 레벨을 바꾸고 능력치를 다시 적용한다. 스포너가 Instantiate 직후(Awake 이후)에 부르므로,
        /// Awake에서만 적용하면 레벨을 올려도 능력치가 1레벨 그대로 남는다.
        /// </summary>
        public void SetLevel(int newLevel)
        {
            level = newLevel;
            CurrentLevelData = data.GetLevelData(level);

            ApplyLevelData();
        }

        // 현재 레벨 데이터를 HP·방어력·공격력·이름표에 넣는다
        private void ApplyLevelData()
        {
            if (CurrentLevelData == null)
            {
                return;
            }

            if (hpController != null)
            {
                hpController.SetMaxHP(CurrentLevelData.maxHP);
            }

            // 방어력은 피해 계산을 맡는 Damageable에 넣는다.
            // 그래야 데미지 텍스트에 뜨는 값이 실제로 깎이는 체력과 같아진다
            if (damageable != null)
            {
                damageable.SetDefense(CurrentLevelData.defense);
            }

            if (attackController != null)
            {
                attackController.SetAttack(CurrentLevelData.attack);
            }

            if (nameTag != null)
            {
                nameTag.SetInfo(data.enemyName, level);
            }
        }

        // ───────────────────────── 감지 ─────────────────────────

        private void Update()
        {
            if (state == State.Dead)
            {
                return;
            }

            // 보스가 대사를 하는 동안(플레이어가 조작할 수 없는 구간)에는 소환수를 포함한 모든 몬스터가 멈춘다
            bool shouldFreeze = BossStateManager.IsBlockingPlayerInput;
            if (shouldFreeze != isFrozen)
            {
                SetFrozen(shouldFreeze);
            }

            if (isFrozen)
            {
                return;
            }

            // 안전지대로 들어간 플레이어는 놓아주고 원래 자리로 돌아간다.
            // 여기서 그냥 돌아가면 추격 상태로 굳어 마을 입구까지 따라와 멈춰 서 있게 된다
            if (IsPlayerUntargetable)
            {
                if (state is State.Chase or State.Attack)
                {
                    ReleaseAttackSlot();
                    SetState(State.Patrol);
                }

                return;
            }

            if (state is State.Idle or State.Patrol)
            {
                if (Util.IsInSight(refTransform, PlayerTransform, data.detectionAngle, data.detectionRange))
                {
                    SetState(State.Chase);
                }

                return;
            }

            // 추격·공격 중에는 플레이어가 시야에서 벗어났는지 확인한다.
            // 공격받은 직후(어그로 시간 안)에는 판정하지 않는다. 등 뒤에서 맞으면 시야각 밖이라 곧바로 추격이 풀려 버린다
            if (state is State.Chase or State.Attack && Time.time >= aggroEndTime)
            {
                UpdateLoseSight();
            }
        }

        // 몬스터를 멈추거나 되살린다. 지금 상태 컴포넌트를 꺼서 이동·공격 판단을 끊고,
        // 애니메이터를 멈춰 모션도 얼린다. 이미 켜진 타격 판정은 닫아 멈춘 채로 맞히지 않게 한다
        private void SetFrozen(bool frozen)
        {
            isFrozen = frozen;

            if (state != State.None && state != State.Dead)
            {
                states[(int)state].enabled = !frozen;
            }

            if (refAnimator != null)
            {
                refAnimator.speed = frozen ? 0f : 1f;
            }

            if (frozen && attackController != null)
            {
                attackController.OnAttackEnd();
            }
        }

        // 추격 중 몸이 플레이어를 향해 서서히 돌기 때문에(RotateTowards), 시야를 벗어나자마자 포기하지 않고
        // loseSightDuration 이상 연속으로 벗어나 있을 때만 대기 상태로 돌아간다
        private void UpdateLoseSight()
        {
            if (Util.IsInSight(refTransform, PlayerTransform, data.detectionAngle, data.detectionRange))
            {
                loseSightTimer = 0f;
                return;
            }

            loseSightTimer += Time.deltaTime;

            if (loseSightTimer >= data.loseSightDuration)
            {
                loseSightTimer = 0f;
                SetState(State.Idle);
            }
        }

        // 공격받았을 때. 플레이어를 감지하지 못한 상태(등을 돌렸거나 시야 밖)에서 맞아도
        // 바로 플레이어를 바라보고 전투 태세로 들어간다
        private void OnAttacked(float damage)
        {
            if (state == State.Dead || PlayerTransform == null)
            {
                return;
            }

            // 어그로 시간을 갱신한다. 이 시간 동안은 시야 판정으로 추격이 풀리지 않는다
            aggroEndTime = Time.time + data.aggroDuration;
            loseSightTimer = 0f;

            // 서서히가 아니라 즉시 돌아본다. 등 뒤에서 맞았을 때 천천히 돌면 그동안 시야 판정이 계속 실패하고,
            // 무엇보다 "맞자마자 돌아본다"는 연출 자체가 목적이기 때문이다
            Vector3 direction = PlayerTransform.position - refTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                refTransform.rotation = Quaternion.LookRotation(direction);
            }

            // 이미 추격·공격 중이면 어그로 갱신만 하고 상태는 건드리지 않는다
            if (state is State.Idle or State.Patrol)
            {
                SetState(State.Chase);
            }
        }

        /// <summary>생성되자마자 플레이어를 알아채고 달려들게 한다 (보스 소환수용).</summary>
        public void AlertToPlayer()
        {
            OnAttacked(0f);
        }

        // ───────────────────────── 상태 전환 ─────────────────────────

        /// <summary>상태를 바꾼다. 사망 후에는 바뀌지 않는다.</summary>
        public void SetState(State newState)
        {
            if (state == newState || state == State.Dead)
            {
                return;
            }

            // 공격 상태를 벗어나면 슬롯을 돌려줘 다음 몬스터가 들어올 수 있게 한다
            if (state == State.Attack && newState != State.Attack)
            {
                ReleaseAttackSlot();
            }

            if (newState != State.Chase)
            {
                ShouldHoldPosition = false;
            }

            if (state != State.None)
            {
                states[(int)state].enabled = false;
            }

            // 멈춰 있는 동안 상태가 바뀌어도(맞아서 추격 등) 새 상태는 풀릴 때까지 꺼 둔다. 사망은 항상 진행한다
            if (newState == State.Dead && isFrozen)
            {
                isFrozen = false;

                if (refAnimator != null)
                {
                    refAnimator.speed = 1f;
                }
            }

            states[(int)newState].enabled = !isFrozen;
            state = newState;

            // 새로 추격을 시작하면 시야 이탈 시간을 처음부터 센다
            if (newState == State.Chase)
            {
                loseSightTimer = 0f;
            }

            onStateChanged.Invoke(state);
        }

        public void SubscribeOnStateChanged(UnityAction<State> listener)
        {
            onStateChanged.AddListener(listener);
        }

        // ───────────────────────── 공격 슬롯 ─────────────────────────

        /// <summary>플레이어를 때릴 자리를 잡는다. 이미 잡고 있으면 계속 유지하고, 자리가 없으면 false.</summary>
        public bool TryClaimAttackSlot()
        {
            if (attackers.Contains(this))
            {
                return true;
            }

            if (attackers.Count >= MaxSimultaneousAttackers)
            {
                return false;
            }

            attackers.Add(this);
            return true;
        }

        public void ReleaseAttackSlot()
        {
            attackers.Remove(this);
        }

        /// <summary>
        /// 자리를 못 잡고 사거리까지 들어온 몬스터가 더 밀고 들어가지 않게 한다.
        /// 달리기 모션이 도는 채로 멈춰 있으면 제자리 뛰기로 보이므로, 로직 상태는 추격 그대로 두고
        /// 애니메이터에만 대기 상태를 알린다.
        /// </summary>
        public bool ShouldHoldPosition
        {
            get => shouldHoldPosition;
            set
            {
                if (shouldHoldPosition == value)
                {
                    return;
                }

                shouldHoldPosition = value;
                onStateChanged.Invoke(value ? State.Idle : state);
            }
        }

        // ───────────────────────── 사망 ─────────────────────────

        /// <summary>
        /// 체력이 0이 됐을 때 (HPController 사망 이벤트를 코드로 구독한다).
        /// 처치 수 집계, 경험치 지급, 전리품 드랍을 하고 스포너에 알린다.
        /// </summary>
        public void OnMonsterDead()
        {
            SetState(State.Dead);

            GameStats.AddMonsterKill();
            OnAnyMonsterDead?.Invoke();

            if (playerLevelController == null)
            {
                playerLevelController = FindAnyObjectByType<PlayerLevelController>();
            }

            if (playerLevelController != null)
            {
                playerLevelController.GainExp(CurrentLevelData.gainExp);
            }

            LootDropper.Instance?.DropFrom(data, refTransform.position);
        }
    }
}
