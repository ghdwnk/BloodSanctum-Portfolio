using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RPGGame
{
    /// <summary>
    /// 보스 한 마리의 중심 컴포넌트. 보스 프리팹(DrekMalos·Zyrathon) 루트에 붙어 있다.
    ///
    /// 상태: 대기(플레이어 감지·조우 대사) → 추격 → 공격(패턴 실행) → 페이즈 전환 / 긴급 회복 → 사망.
    ///       지금 상태의 컴포넌트(BossXxxState)만 켜 둔다.
    /// 패턴·페이즈: BossData 에셋에서 읽는다. 체력 비율이 페이즈 기준 아래로 내려가면 페이즈가 바뀌고,
    ///       페이즈마다 쓸 수 있는 패턴 목록과 공격 간격이 다르다. 보스가 늘어도 이 스크립트는 그대로 두고
    ///       BossData와 패턴 에셋만 새로 만들면 된다.
    /// 전투 흐름: 플레이어가 죽으면 처음 상태로 되돌리고(소환수 정리·체력 회복), 쓰러지면 보상·전투 음악 종료를 처리한다.
    /// 조우 대사와 페이즈 전환 연출 동안에는 InputManager가 플레이어 조작을 막는다 (IsBlockingPlayerInput).
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class BossStateManager : MonoBehaviour
    {
        /// <summary>보스 상태. Heal은 Dead 뒤에 둔다 (예전에 저장된 정수 값이 프리팹과 어긋나지 않게 하기 위해서다).</summary>
        public enum State { None = -1, Idle, Chase, Attack, PhaseTransition, Dead, Heal, Length }

        [Tooltip("이 보스의 데이터 (이름·레벨별 능력치·페이즈·패턴·대사·사운드)")]
        [SerializeField] private BossData data;
        [Tooltip("시작 레벨. 스포너가 생성 직후 SetLevel로 덮어쓴다")]
        [SerializeField] private int level = 1;
        [Tooltip("보스 공격이 맞을 수 있는 레이어 (플레이어)")]
        [SerializeField] private LayerMask attackTargetLayer;

        [Header("이벤트")]
        [Tooltip("상태가 바뀔 때마다 발행 (BossAnimationTrigger가 코드로 구독한다)")]
        [SerializeField] private UnityEvent<State> OnStateChanged = new UnityEvent<State>();
        [Tooltip("페이즈가 바뀔 때 발행 (보스 체력바의 페이즈 표시)")]
        [SerializeField] private UnityEvent<int> OnPhaseChanged = new UnityEvent<int>();

        private State state = State.None;
        // State 값을 번호로 쓰는 상태 컴포넌트 표
        private BossStateBase[] states;

        private Transform refTransform;
        private HPController hpController;
        private BossAnimationTrigger animationTrigger;
        private PlayerStateManager targetPlayerStateManager;

        // 패턴별 다음 사용 가능 시각. 패턴 애셋을 키로 써서 쿨타임을 개별 관리한다
        private readonly Dictionary<BossPatternData, float> patternCooldowns = new();
        // 마지막 공격 시각 (페이즈의 attackInterval 판정용)
        private float lastAttackTime = -999f;
        // 기본 공격은 특수 패턴과 따로 간격을 센다 (섞으면 기본 공격이 특수 패턴을 막거나 그 반대가 된다)
        private float lastBasicAttackTime = -999f;
        // 직전에 쓴 특수 패턴. 후보가 여럿이면 같은 패턴을 연속으로 쓰지 않게 한다
        private BossPatternData lastSpecialPattern;
        // 장판 계열 패턴 다음에 장판 계열을 막아 두는 최대 시간(초). 이 시간이 지나면 다른 패턴이 없어도 다시 쓸 수 있다
        private const float GroundZoneLockTime = 12f;
        // 긴급 회복을 이미 썼는지. 한 전투에 한 번만 쓰고, 전투가 리셋되면 다시 쓸 수 있다
        private bool hasUsedEmergencyHeal;

        // 플레이어가 죽었을 때 되돌아갈 처음 위치. 생성 직후(Start) 위치를 기억한다
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        // 플레이어 사망 이벤트 구독 대상. 보스가 먼저 파괴될 때 구독을 풀기 위해 들고 있는다
        private HPController playerHPController;
        // 전투 음악을 이미 틀었는지. Idle에서 벗어날 때 한 번만 전환하고, 리셋되면 다시 틀 수 있게 한다
        private bool isBattleMusicPlaying;
        // 조우 대사를 이미 보여줬는지. 리셋되면 다시 보여준다
        private bool hasPlayedEncounter;
        // 페이즈/사망 대사처럼 전투를 멈추지 않는 대사가 떠 있는 시간
        private const float CombatLineShowTime = 4f;

        // 이 보스가 소환한 몬스터. 보스가 쓰러지거나 전투가 리셋되면 함께 정리한다
        private readonly List<MonsterStateManager> minions = new();
        // 패턴을 고를 때마다 새 리스트를 만들지 않도록 재사용하는 후보 목록
        private readonly List<BossPatternData> usablePatterns = new();

        public BossData Data => data;
        public EnemyData.LevelData CurrentLevelData { get; private set; }
        public Transform PlayerTransform { get; private set; }
        public LayerMask AttackTargetLayer => attackTargetLayer;
        public int CurrentPhaseIndex { get; private set; }
        public BossData.Phase CurrentPhase => data != null ? data.GetPhase(CurrentPhaseIndex) : null;
        // 지금 실행 중인(또는 실행할) 패턴. BossAttackState가 사용한다
        public BossPatternData PendingPattern { get; private set; }

        public bool IsDead => state == State.Dead;
        // 패턴 실행 중이거나 페이즈 전환 연출 중. 이 동안은 피격 모션이 끼어들지 않는다
        public bool IsPerformingAction => state is State.Attack or State.PhaseTransition or State.Heal;
        public bool IsPlayerDead => targetPlayerStateManager != null && targetPlayerStateManager.IsPlayerDead;
        // 일반 몬스터와 같은 이유로 안전지대도 함께 본다
        public bool IsPlayerUntargetable => IsPlayerDead || ZoneTracker.IsPlayerInSafeZone;
        // 조우 대사가 떠 있는 동안 true. BossIdleState가 이 동안은 공격을 시작하지 않는다
        public bool IsInEncounterDialogue { get; private set; }
        // 조우 대사를 아직 보여주지 않았고 데이터에 대사가 있으면 true
        public bool ShouldPlayEncounter => !hasPlayedEncounter && !string.IsNullOrEmpty(data.encounterDialogue);

        // 살아 있는 소환수 수. 죽거나 사라진 항목은 세면서 목록에서 뺀다
        public int AliveMinionCount
        {
            get
            {
                minions.RemoveAll(minion => minion == null || minion.IsDead);
                return minions.Count;
            }
        }

        public void RegisterMinion(MonsterStateManager minion)
        {
            minions.Add(minion);
        }

        // 남아 있는 소환수를 모두 제거하는 함수. 보스가 쓰러지거나 플레이어가 죽어 전투가 처음으로 돌아갈 때 부른다.
        // 몬스터 프리팹은 루트가 본체라 transform.root를 지운다
        private void ClearMinions()
        {
            foreach (MonsterStateManager minion in minions)
            {
                if (minion != null && !minion.IsDead)
                {
                    Destroy(minion.transform.root.gameObject);
                }
            }

            minions.Clear();
        }

        private void Awake()
        {
            refTransform = transform;

            if (data == null)
            {
                Debug.LogError($"{name}: BossData가 지정되지 않았습니다.");
                enabled = false;
                return;
            }

            // 상태 컴포넌트를 State 순서대로 담고 모두 꺼 둔다 (OnEnable에서 대기 상태만 켠다)
            states = new BossStateBase[]
            {
                GetComponent<BossIdleState>(),
                GetComponent<BossChaseState>(),
                GetComponent<BossAttackState>(),
                GetComponent<BossPhaseTransitionState>(),
                GetComponent<BossDeadState>(),
                GetComponent<BossHealState>(),
            };

            foreach (BossStateBase stateComponent in states)
            {
                if (stateComponent != null)
                {
                    stateComponent.enabled = false;
                }
            }

            targetPlayerStateManager = FindAnyObjectByType<PlayerStateManager>();
            if (targetPlayerStateManager != null)
            {
                PlayerTransform = targetPlayerStateManager.transform;

                // 플레이어가 죽으면 보스전을 처음 상태로 되돌린다.
                // IsPlayerDead를 매 프레임 폴링하면 "죽어 있는 동안" 계속 리셋이 불리므로 이벤트로 한 번만 받는다
                playerHPController = targetPlayerStateManager.GetComponentInChildren<HPController>();
                if (playerHPController != null)
                {
                    playerHPController.SubscribeOnDead(OnPlayerDead);
                }
            }

            hpController = GetComponentInChildren<HPController>();
            animationTrigger = GetComponentInChildren<BossAnimationTrigger>();

            if (hpController != null)
            {
                hpController.SubscribeOnDead(OnDead);
            }

            // 조우 대사 도중에 공격받으면 대사를 끊고 바로 전투로 들어가기 위해 피격을 듣는다
            Damageable damageable = GetComponentInChildren<Damageable>();
            if (damageable != null)
            {
                damageable.SubscribeOnDamageReceived(OnAttacked);
            }

            ApplyLevel();

            CurrentPhaseIndex = 0;
        }

        // 살아 있는 보스 목록. 플레이어 입력 차단 여부를 InputManager가 매 프레임 물어보므로 직접 관리한다
        private static readonly List<BossStateManager> activeBosses = new();

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 플레이의 보스가 목록에 남지 않게 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeBosses.Clear();
        }

        // 보스가 말을 거는 연출 중이라 플레이어가 조작하면 안 되는 상태인지.
        // 조우 대사가 떠 있는 동안과, 페이즈 전환 연출(보스가 외치며 멈춰 있는 구간, 페이즈 대사가 떠 있는 동안 포함)이다
        public static bool IsBlockingPlayerInput
        {
            get
            {
                for (int ix = 0; ix < activeBosses.Count; ++ix)
                {
                    BossStateManager boss = activeBosses[ix];
                    if (boss != null && !boss.IsDead && (boss.IsInEncounterDialogue || boss.state == State.PhaseTransition))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void OnEnable()
        {
            if (!activeBosses.Contains(this))
            {
                activeBosses.Add(this);
            }

            SetState(State.Idle);
        }

        private void OnDisable()
        {
            activeBosses.Remove(this);
        }

        private void Start()
        {
            // 스포너가 Instantiate 직후 위치를 잡아 주므로 Awake가 아니라 Start에서 기억한다
            initialPosition = refTransform.position;
            initialRotation = refTransform.rotation;
        }

        private void OnDestroy()
        {
            // 시체가 사라진 뒤에도 플레이어의 사망 이벤트가 파괴된 보스를 부르지 않게 구독을 푼다
            if (playerHPController != null)
            {
                playerHPController.UnsubscribeOnDead(OnPlayerDead);
            }

            // 대사 도중 파괴되면 카메라가 줌인된 채 남지 않게 한다
            if (IsInEncounterDialogue)
            {
                EndEncounterDialogue();
            }
        }

        // 보스 대사 페이지마다 화자 이름을 [이름] 형태로 붙이는 함수.
        // 대화창이 이 표기를 읽어 이름표와 화자 일러스트로 보여준다 (누구 말인지 전투 중에도 알 수 있다)
        private string FormatSpeech(string text)
        {
            string[] pages = text.Split('|');
            for (int ix = 0; ix < pages.Length; ++ix)
            {
                pages[ix] = $"[{data.enemyName}] {pages[ix].Trim()}";
            }

            return string.Join("|", pages);
        }

        // 조우 대사를 시작하는 함수 (BossIdleState에서 플레이어가 감지 범위에 처음 들어왔을 때 호출).
        // 대사가 끝날 때까지 보스는 공격하지 않고, 카메라는 보스를 비춘다
        public void BeginEncounterDialogue()
        {
            if (!ShouldPlayEncounter)
            {
                return;
            }

            hasPlayedEncounter = true;
            IsInEncounterDialogue = true;

            // 대사가 떠 있는 동안은 전투가 시작되지 않는다. 날아오던 공격이나 지속 피해에 맞아도 체력이 깎이지 않게 한다
            SetBossImmune(true);

            BossSfx.Play(data.encounterSounds, refTransform.position, data.soundVolume * data.encounterVolume);

            Dialogue.ShowDialogueText(FormatSpeech(data.encounterDialogue));

            // NPC 대화용 줌인은 키 1.8m 기준이라 덩치 큰 보스는 얼굴이 잘린다. 캐릭터 높이 비율로 거리를 벌린다
            CharacterController controller = GetComponent<CharacterController>();
            float scale = controller != null ? Mathf.Max(1f, controller.height / 1.8f) : 1f;
            DialogueCameraController.Focus(refTransform, scale);
        }

        // 조우 대사를 끝내고 전투로 넘어갈 수 있게 하는 함수.
        // 플레이어가 창을 닫았거나(BossIdleState가 감지), 대사 도중 보스를 공격했을 때 호출된다
        public void EndEncounterDialogue()
        {
            if (!IsInEncounterDialogue)
            {
                return;
            }

            IsInEncounterDialogue = false;
            SetBossImmune(false);

            if (Dialogue.IsOpen)
            {
                Dialogue.CloseDialogue();
            }

            DialogueCameraController.Release();
        }

        // 조우 대사가 떠 있는 동안 보스가 피해를 받지 않게 켜고 끈다 (Damageable.IsImmune)
        private void SetBossImmune(bool immune)
        {
            Damageable damageable = GetComponentInChildren<Damageable>();
            if (damageable != null)
            {
                damageable.IsImmune = immune;
            }
        }

        private void OnAttacked(float damage)
        {
            // 일반 몬스터의 Hit Audio Player와 같은 소리·피치 변화(±0.2)로, 맞을 때마다 재생한다. 음량은 보스 데이터의 hitVolume을 쓴다
            BossSfx.Play(data.hitSounds, refTransform.position, data.hitVolume, 0.2f);
        }

        private void Update()
        {
            if (IsDead || state is State.PhaseTransition or State.Heal)
            {
                return;
            }

            CheckPhaseTransition();
            CheckEmergencyHeal();
        }

        // 전투 중 체력이 기준 아래로 떨어지면 긴급 회복을 한 번 발동한다.
        // 페이즈 전환 연출 중에는 Update가 일찍 반환하므로 겹치지 않고, 연출이 끝난 뒤 다음 프레임에 판정된다
        private void CheckEmergencyHeal()
        {
            BossData.EmergencyHeal heal = data.emergencyHeal;

            if (hasUsedEmergencyHeal || heal == null || !heal.enabled || hpController == null)
            {
                return;
            }

            // 교전 중일 때만 쓴다 (조우 전에 테스트 도구로 체력을 깎아 둔 경우 등은 Chase로 넘어온 뒤에 판정된다)
            if (state is not (State.Chase or State.Attack))
            {
                return;
            }

            if (hpController.CurrentHP <= 0f || hpController.HPRatio > heal.triggerHPRatio)
            {
                return;
            }

            hasUsedEmergencyHeal = true;
            SetState(State.Heal);
        }

        // 전투를 멈추지 않는 보스 대사를 띄운다 (긴급 회복 등 상태 클래스에서 사용)
        public void ShowCombatLine(string text)
        {
            Dialogue.ShowDialogueTextTemporarily(FormatSpeech(text), CombatLineShowTime);
        }

        // 레벨에 따른 체력/방어력을 적용하는 함수
        private void ApplyLevel()
        {
            CurrentLevelData = data.GetLevelData(level);

            if (hpController != null)
            {
                hpController.SetMaxHP(CurrentLevelData.maxHP);
            }

            // 일반 몬스터와 같은 이유로 방어력은 Damageable이 들고 있는다
            Damageable damageController = GetComponentInChildren<Damageable>();
            if (damageController != null)
            {
                damageController.SetDefense(CurrentLevelData.defense);
            }
        }

        public void SetLevel(int newLevel)
        {
            level = newLevel;
            ApplyLevel();
        }

        // 남은 체력이 다음 페이즈 조건에 도달했는지 확인하고, 도달했으면 전환 스테이트로 넘긴다
        private void CheckPhaseTransition()
        {
            if (hpController == null)
            {
                return;
            }

            int targetPhase = data.GetPhaseIndex(hpController.HPRatio);

            if (targetPhase == CurrentPhaseIndex)
            {
                return;
            }

            CurrentPhaseIndex = targetPhase;
            // 페이즈가 바뀌면 이전 페이즈에서 쓰던 쿨타임은 의미가 없으므로 초기화한다
            patternCooldowns.Clear();

            OnPhaseChanged?.Invoke(CurrentPhaseIndex);

            SetState(State.PhaseTransition);
        }

        public void SetState(int newState)
        {
            SetState((State)newState);
        }

        public void SetState(State newState)
        {
            if (state == newState || state == State.Dead)
            {
                return;
            }

            if (state != State.None && states[(int)state] != null)
            {
                states[(int)state].enabled = false;
            }

            if (states[(int)newState] != null)
            {
                states[(int)newState].enabled = true;
            }

            state = newState;
            OnStateChanged?.Invoke(state);

            // 전투 음악은 보스가 생성될 때가 아니라 실제로 플레이어를 인지해 움직이기 시작할 때 튼다.
            // 퀘스트를 수락하는 순간(보스는 아레나에 있고 플레이어는 마을)부터 전투 음악이 나오는 것을 막고,
            // 리셋 후 다시 교전할 때도 같은 경로로 다시 틀 수 있다
            if (!isBattleMusicPlaying && state is State.Chase or State.Attack)
            {
                isBattleMusicPlaying = true;
                FindAnyObjectByType<BackgroundMusicPlayer>()?.PlayBattleMusic();
            }
        }

        // 현재 거리에서 사용할 수 있는 패턴을 고르는 함수.
        // 쿨타임이 지났고 사거리 조건을 만족하는 패턴 중 하나를 무작위로 선택한다
        public BossPatternData PickPattern()
        {
            BossData.Phase phase = CurrentPhase;
            if (phase == null || phase.patterns.Count == 0 || PlayerTransform == null)
            {
                return null;
            }

            float distance = Vector3.Distance(refTransform.position, PlayerTransform.position);

            // 특수 패턴은 페이즈가 정한 공격 간격을 지켜야 쓸 수 있다
            if (Time.time >= lastAttackTime + phase.attackInterval)
            {
                List<BossPatternData> usable = usablePatterns;
                usable.Clear();
                foreach (BossPatternData pattern in phase.patterns)
                {
                    if (pattern == null || !pattern.IsUsableAt(distance))
                    {
                        continue;
                    }

                    if (patternCooldowns.TryGetValue(pattern, out float readyTime) && Time.time < readyTime)
                    {
                        continue;
                    }

                    usable.Add(pattern);
                }

                // 방금 쓴 특수 패턴이 장판 계열이면 장판 계열은 모두 제외한다 (예고 판이 겹쳐 보이는 것을 막는다).
                // 다른 패턴이 하나도 없어 보스가 멈춰 서는 일이 없도록, 일정 시간이 지나면 다시 허용한다
                if (lastSpecialPattern != null && lastSpecialPattern.IsGroundZone
                    && Time.time < lastAttackTime + GroundZoneLockTime)
                {
                    usable.RemoveAll(pattern => pattern.IsGroundZone);
                }

                // 후보가 둘 이상이면 방금 쓴 패턴은 제외한다 (같은 패턴이 연달아 나오는 것을 막는다)
                if (usable.Count > 1)
                {
                    usable.Remove(lastSpecialPattern);
                }

                if (usable.Count > 0)
                {
                    return usable[UnityEngine.Random.Range(0, usable.Count)];
                }
            }

            // 쓸 특수 패턴이 없으면 가까울 때 기본 공격을 한다
            BossPatternData basic = data.basicAttack;
            if (basic != null && distance <= data.basicAttackRange
                && Time.time >= lastBasicAttackTime + data.basicAttackInterval)
            {
                return basic;
            }

            return null;
        }

        // 패턴을 실행하는 함수. BossAttackState가 스테이트에 진입할 때 호출한다
        public void ExecutePattern(BossPatternData pattern)
        {
            if (pattern == null)
            {
                return;
            }

            BossData.Phase phase = CurrentPhase;
            float damageMultiplier = phase != null ? phase.damageMultiplier : 1f;

            BossPatternContext context = new BossPatternContext
            {
                BossTransform = refTransform,
                BossController = GetComponent<CharacterController>(),
                PlayerTransform = PlayerTransform,
                TargetLayer = attackTargetLayer,
                AttackPower = CurrentLevelData.attack * damageMultiplier,
                Caster = this
            };

            PlayPatternAnimation(pattern.animationTrigger, pattern.animationDelay);

            pattern.Execute(context);

            if (pattern == data.basicAttack)
            {
                lastBasicAttackTime = Time.time;
            }
            else
            {
                patternCooldowns[pattern] = Time.time + pattern.cooldown;
                lastAttackTime = Time.time;
                lastSpecialPattern = pattern;
            }
        }

        // 연계 공격처럼 패턴 도중에 다른 동작을 재생해야 할 때 쓰는 애니메이션 트리거.
        // delay가 있으면 그만큼 기다렸다가 재생한다 (예고가 길 때 타격 순간과 모션을 맞추기 위함)
        public void PlayPatternAnimation(string triggerName, float delay = 0f)
        {
            if (animationTrigger == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            if (delay <= 0f)
            {
                animationTrigger.PlayTrigger(triggerName);
                return;
            }

            StartCoroutine(PlayAnimationAfter(triggerName, delay));
        }

        private System.Collections.IEnumerator PlayAnimationAfter(string triggerName, float delay)
        {
            yield return new WaitForSeconds(delay);
            animationTrigger.PlayTrigger(triggerName);
        }

        // 공격할 패턴을 예약하고 공격 스테이트로 전환하는 함수
        public bool TryStartAttack()
        {
            if (IsPlayerUntargetable)
            {
                return false;
            }

            BossPatternData pattern = PickPattern();
            if (pattern == null)
            {
                return false;
            }

            PendingPattern = pattern;
            SetState(State.Attack);

            return true;
        }

        // 페이즈 전환 연출 트리거를 재생하는 함수 (BossPhaseTransitionState에서 호출).
        // 페이즈 대사를 대화창에 띄웠으면 true. 이때는 전환 상태가 플레이어가 창을 닫을 때까지 전투를 재개하지 않는다
        public bool PlayPhaseTransitionEffect()
        {
            BossData.Phase phase = CurrentPhase;
            if (phase == null)
            {
                return false;
            }

            if (animationTrigger != null && !string.IsNullOrEmpty(phase.transitionTrigger))
            {
                animationTrigger.PlayTrigger(phase.transitionTrigger);
            }

            BossSfx.Play(data.phaseSounds, refTransform.position, data.soundVolume * data.phaseVolume);

            if (phase.transitionEffectPrefab != null)
            {
                GameObject effect = Instantiate(phase.transitionEffectPrefab, refTransform.position, Quaternion.identity);
                Destroy(effect, 5f);
            }

            // 페이즈 대사는 조우 대사처럼 플레이어가 직접 넘기고 닫는다. 창이 떠 있는 동안 보스는 가만히 있고,
            // 닫으면 전투를 재개한다 (BossPhaseTransitionState가 창이 닫혔는지 지켜본다)
            if (!string.IsNullOrEmpty(phase.transitionDialogue))
            {
                Dialogue.ShowDialogueText(FormatSpeech(phase.transitionDialogue));
                return true;
            }

            return false;
        }

        // 플레이어를 바라보도록 회전하는 함수
        public void RotateToPlayer(float rotateSpeed)
        {
            if (PlayerTransform == null)
            {
                return;
            }

            Vector3 direction = PlayerTransform.position - refTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            refTransform.rotation = Quaternion.RotateTowards(refTransform.rotation,
                Quaternion.LookRotation(direction), rotateSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 추격 중 플레이어가 이 거리보다 멀어지면 보스가 추격을 포기하고 대기로 돌아간다.
        /// 기본은 감지 범위지만, 현재 페이즈에 먼 거리에서도 쓸 수 있는 패턴(순간이동)이 있으면 그 거리까지 넓힌다.
        /// 그렇지 않으면 플레이어가 감지 범위 밖으로 벗어나는 순간 보스가 멈춰 서 버린다
        /// </summary>
        public float ChaseGiveUpRange
        {
            get
            {
                float range = data.detectionRange;
                BossData.Phase phase = CurrentPhase;
                if (phase == null)
                {
                    return range;
                }

                foreach (BossPatternData pattern in phase.patterns)
                {
                    if (pattern != null && pattern.MaxUsableRange > range)
                    {
                        range = pattern.MaxUsableRange;
                    }
                }

                return range;
            }
        }

        public float DistanceToPlayer =>
            PlayerTransform != null ? Vector3.Distance(refTransform.position, PlayerTransform.position) : float.MaxValue;

        public void OnDead()
        {
            // 조우 대사 도중 죽는 경우(대사 중 공격 허용)에도 카메라를 되돌린다
            EndEncounterDialogue();

            SetState(State.Dead);

            BossSfx.Play(data.deathSounds, refTransform.position, data.soundVolume * data.deathVolume);

            // 쓰러진 뒤에는 남은 공격 예고가 바닥에 떠 있지 않게 지운다
            BossTelegraph.ClearAll();
            ClearMinions();

            if (!string.IsNullOrEmpty(data.deathDialogue))
            {
                Dialogue.ShowDialogueTextTemporarily(FormatSpeech(data.deathDialogue), CombatLineShowTime);
            }

            // 일반 몬스터(MonsterStateManager.OnMonsterDead)와 동일하게 경험치와 퀘스트 진행도를 처리한다.
            // 프리팹의 이벤트 배선에 의존하지 않고 코드에서 직접 호출해 누락을 막는다
            PlayerLevelController playerLevelController = FindAnyObjectByType<PlayerLevelController>();
            if (playerLevelController != null && CurrentLevelData != null)
            {
                playerLevelController.GainExp(CurrentLevelData.gainExp);
            }

            QuestItem questItem = GetComponentInChildren<QuestItem>();
            if (questItem != null)
            {
                questItem.OnCompleted();
            }

            // 보스도 일반 몬스터와 같은 드랍 경로를 탄다 (BossData가 EnemyData를 상속한다)
            LootDropper.Instance?.DropFrom(data, refTransform.position);
        }

        private void OnPlayerDead()
        {
            // 이미 죽은 보스는 되살리지 않는다 (시체 연출 중일 수 있다)
            if (IsDead)
            {
                return;
            }

            ResetEncounter();
        }

        // 보스전을 처음 상태로 되돌리는 함수.
        // 플레이어가 죽어 마을에서 부활하면, 보스는 깎인 체력 그대로 아레나에 서 있는 대신
        // 체력·페이즈·쿨타임·위치를 전부 생성 직후로 되돌린다
        public void ResetEncounter()
        {
            // 진행 중이던 패턴(돌진/장판 등의 코루틴)이 계속 피해를 주지 않도록 먼저 끊는다
            StopAllCoroutines();

            // 페이즈 전환 대사가 떠 있었다면 함께 닫는다 (조우 대사는 아래 EndEncounterDialogue가 닫는다)
            if (state == State.PhaseTransition && Dialogue.IsOpen)
            {
                Dialogue.CloseDialogue();
            }

            // 처음 상태로 돌아가므로 조우 대사도 다시 보여준다 (스페이스로 바로 넘길 수 있다)
            EndEncounterDialogue();
            hasPlayedEncounter = false;

            ClearMinions();
            PendingPattern = null;
            patternCooldowns.Clear();
            lastAttackTime = -999f;
            lastBasicAttackTime = -999f;
            lastSpecialPattern = null;
            hasUsedEmergencyHeal = false;
            BossTelegraph.ClearAll();

            // 페이즈를 되돌리고 UI에도 알린다 (보스 체력바의 페이즈 표시)
            if (CurrentPhaseIndex != 0)
            {
                CurrentPhaseIndex = 0;
                OnPhaseChanged?.Invoke(CurrentPhaseIndex);
            }

            if (hpController != null && CurrentLevelData != null)
            {
                hpController.SetMaxHP(CurrentLevelData.maxHP);
            }

            // 공격 모션 도중이었다면 남은 트리거를 지워 Idle로 돌아가게 한다
            if (animationTrigger != null)
            {
                animationTrigger.ResetAllTriggers();
            }

            Util.TeleportCharacter(refTransform, initialPosition, initialRotation);

            SetState(State.Idle);

            if (isBattleMusicPlaying)
            {
                isBattleMusicPlaying = false;
                FindAnyObjectByType<BackgroundMusicPlayer>()?.PlayNormalMusic();
            }
        }

        // 조우 대사를 이미 본 것으로 처리한다 (테스트 도구에서 보스전을 바로 시작할 때 쓴다)
        public void SkipEncounterDialogue()
        {
            hasPlayedEncounter = true;
        }

        public void SubscribeOnStateChanged(UnityAction<State> listener)
        {
            OnStateChanged?.AddListener(listener);
        }

        public void SubscribeOnPhaseChanged(UnityAction<int> listener)
        {
            OnPhaseChanged?.AddListener(listener);
        }
    }
}
