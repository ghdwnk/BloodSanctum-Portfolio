using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스 전용 데이터. EnemyData(감지/공격/레벨 스탯)를 그대로 물려받고 페이즈 정보를 더한다.
    /// 드렉 말로스처럼 페이즈가 하나뿐인 보스도 phases에 1개만 넣으면 된다 (기획서 13절)
    /// </summary>
    [CreateAssetMenu(fileName = "New Boss Data", menuName = "Scriptable Objects/Boss Data")]
    public class BossData : EnemyData
    {
        // 페이즈 하나의 구성. 진입 체력 비율과 그때 사용할 패턴 목록을 갖는다
        [System.Serializable]
        public class Phase
        {
            public string phaseName = "Phase 1";
            // 이 페이즈로 진입하는 체력 비율(0~1). 1이면 전투 시작 시점부터
            [Range(0f, 1f)] public float enterHPRatio = 1f;
            // 이 페이즈에서 사용할 공격 패턴 목록
            public List<BossPatternData> patterns = new();
            // 공격과 공격 사이의 간격
            public float attackInterval = 3f;
            // 이 페이즈에서 적용할 공격력 배율 (HP 50% 이하 강화 등)
            public float damageMultiplier = 1f;
            // 페이즈 진입 시 재생할 애니메이터 트리거 (비우면 연출 없이 즉시 전환)
            public string transitionTrigger = string.Empty;
            // 페이즈 진입 연출 길이. 이 시간 동안 보스는 행동하지 않는다
            public float transitionDuration = 1.5f;
            // 페이즈 진입 시 생성할 연출 이펙트
            public GameObject transitionEffectPrefab;
            // 페이즈 진입 시 보스가 외치는 대사 (비우면 표시하지 않는다). 전투를 멈추지 않고 잠깐 떠 있다 사라진다
            [TextArea(2, 3)] public string transitionDialogue = string.Empty;
        }

        // 체력이 바닥나기 직전에 한 번만 쓰는 회복. 패턴과 달리 거리·쿨타임이 아니라 체력 비율로 발동한다
        [System.Serializable]
        public class EmergencyHeal
        {
            public bool enabled = false;
            // 체력이 이 비율 이하로 떨어지면 발동한다
            [Range(0f, 1f)] public float triggerHPRatio = 0.2f;
            // 이 비율까지 회복한다. 다음 페이즈 경계(enterHPRatio)보다 낮게 두면 회복 뒤에 페이즈가 다시 바뀌지 않는다
            [Range(0f, 1f)] public float healToHPRatio = 0.49f;
            // 회복에 걸리는 시간. 이 동안 보스는 움직이지 않고 체력이 차오른다 (공격은 계속 받는다)
            public float castDuration = 3f;
            // 시전하는 동안 보스에 붙일 이펙트
            public GameObject effectPrefab;
            // 시전 모션 트리거 (비우면 모션 없음)
            public string animationTrigger = string.Empty;
            // 시전할 때 보스가 외치는 대사 (비우면 표시하지 않는다)
            [TextArea(2, 3)] public string dialogue = string.Empty;
            // 회복을 시작할 때 재생할 소리(여러 개면 무작위 하나)와 음량 보정 (BossData.soundVolume에 곱해진다)
            public AudioClip[] sounds;
            [Range(0f, 2f)] public float volume = 1f;
        }

        [Header("표시")]
        [Tooltip("보스 체력바에서 이름 앞에 붙는 수식어 (예: 마계의 군주). 비우면 이름만 표시한다. enemyName은 대사 화자 표기 등에도 쓰이므로 수식어는 따로 둔다")]
        public string bossTitle = string.Empty;

        [Header("페이즈")]
        // 체력이 높은 페이즈부터 순서대로 넣는다 (예: 1.0 -> 0.5)
        public List<Phase> phases = new();

        [Header("긴급 회복")]
        public EmergencyHeal emergencyHeal = new();

        [Header("기본 공격")]
        [Tooltip("특수 패턴이 쓸 수 없을 때 가까이 있으면 짧은 간격으로 계속 쓰는 공격. 비워두면 기본 공격이 없다")]
        public BossPatternData basicAttack;
        [Tooltip("기본 공격을 쓸 수 있는 최대 거리. 보스는 이 거리까지 접근한다")]
        public float basicAttackRange = 4f;
        [Tooltip("기본 공격 사이의 간격 (특수 패턴의 attackInterval과 별개로 흐른다)")]
        public float basicAttackInterval = 1.6f;

        [Header("사운드")]
        [Tooltip("각 칸이 비어 있으면 그 상황에서는 소리가 나지 않는다. 여러 개를 넣으면 무작위로 하나를 재생한다 조우 대사가 시작될 때(등장 포효)")]
        public AudioClip[] encounterSounds;
        [Tooltip("페이즈가 바뀔 때(광폭화 포효)")]
        public AudioClip[] phaseSounds;
        [Tooltip("맞을 때 나는 타격음 (일반 몬스터와 같은 소리를 넣는다)")]
        public AudioClip[] hitSounds;
        [Tooltip("쓰러질 때 비명")]
        public AudioClip[] deathSounds;
        [Tooltip("쓰러진 뒤 잠시 후 몸이 무너지는 소리")]
        public AudioClip[] deathBodySounds;
        [Range(0f, 1f)] public float soundVolume = 0.8f;
        // 조우/페이즈 소리별 음량 보정 (soundVolume에 곱해진다). 소리 파일마다 원래 크기가 달라서 소리별로 따로 둔다.
        // 곱한 최종 값은 1을 넘지 않는다
        [Range(0f, 2f)] public float encounterVolume = 1f;
        [Range(0f, 2f)] public float phaseVolume = 1f;
        // 쓰러질 때 비명·몸이 무너지는 소리의 음량 보정 (soundVolume에 곱해진다). 사망음만 따로 줄이거나 키울 때 쓴다
        [Range(0f, 2f)] public float deathVolume = 1f;
        // 맞을 때 나는 타격음의 음량 (0~1). 일반 몬스터와 같은 타격음이지만 보스 소리는 14m 안에서 감쇠가 없어 유독 크게 들려서 약간 낮춰 둔다
        [Range(0f, 1f)] public float hitVolume = 0.65f;

        [Header("대사")]
        // 플레이어가 감지 범위에 처음 들어왔을 때 보스가 하는 대사. '|'로 페이지를 나눈다.
        // 이 대사가 끝날 때까지 보스는 공격하지 않는다 (퀘스트 NPC 대사와 분리하기 위해 보스 데이터에 둔다)
        [TextArea(2, 4)] public string encounterDialogue = string.Empty;
        // 쓰러질 때 남기는 대사 (비우면 표시하지 않는다)
        [TextArea(2, 3)] public string deathDialogue = string.Empty;

        // 추격할 때 멈춰 서는 거리. 기본 공격이 있으면 그 사거리까지 다가가야 때릴 수 있다
        public float ChaseStopDistance => basicAttack != null ? Mathf.Min(attackRange, basicAttackRange) : attackRange;

        // 현재 체력 비율에 해당하는 페이즈 번호를 구하는 함수.
        // enterHPRatio가 현재 비율 이상인 페이즈 중 가장 마지막(가장 낮은 체력 조건)을 고른다
        public int GetPhaseIndex(float hpRatio)
        {
            int result = 0;

            for (int ix = 0; ix < phases.Count; ++ix)
            {
                if (hpRatio <= phases[ix].enterHPRatio)
                {
                    result = ix;
                }
            }

            return result;
        }

        public Phase GetPhase(int index)
        {
            if (phases == null || phases.Count == 0)
            {
                return null;
            }

            return phases[Mathf.Clamp(index, 0, phases.Count - 1)];
        }
    }
}
