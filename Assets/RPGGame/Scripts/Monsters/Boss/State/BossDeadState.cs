using System.Collections;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 사망 상태. 진행 중인 패턴을 멈추고, 사망 모션을 보여준 뒤 시체를 땅속으로 가라앉혀 제거한다
    /// </summary>
    public class BossDeadState : BossStateBase
    {
        [Header("시체 제거")]
        // 사망 모션을 보여줄 시간. 이 시간이 지나면 가라앉기 시작한다
        [SerializeField] private float corpseStayTime = 5f;
        // 땅속으로 가라앉는 데 걸리는 시간
        [SerializeField] private float sinkDuration = 2.5f;
        // 가라앉는 깊이. 보스 모델이 커서 일반 몬스터보다 깊게 내린다
        [SerializeField] private float sinkDepth = 4f;
        // 비명이 난 뒤 몸이 무너지는 소리까지의 지연 (사망 모션의 쓰러지는 순간에 맞춘다)
        [SerializeField] private float bodyFallSoundDelay = 1.0f;

        protected override void OnEnable()
        {
            base.OnEnable();

            // 사망 시점에 진행 중이던 패턴이 계속 피해를 주지 않도록 중단한다
            manager.StopAllCoroutines();

            // 비명(BossStateManager.OnDead)보다 조금 늦게, 몸이 바닥에 무너지는 소리를 낸다.
            // manager의 코루틴은 위에서 멈췄으므로 이 컴포넌트에서 돌린다
            StartCoroutine(PlayBodyFall());

            // 더 이상 밀려나지 않도록 이동 처리를 끈다
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // 시체 위에 0/최대 체력바가 떠 있지 않게 머리 위 체력바를 감춘다.
            // HPBar가 붙은 오브젝트 자체를 끄면 같은 오브젝트의 UIDamageText도 함께 꺼져서
            // 사망 직전 마지막 데미지 텍스트가 뜨다 말고 코루틴 에러가 나므로, 체력바 시각 요소만 감춘다
            HPBar hpBar = GetComponentInChildren<HPBar>();
            if (hpBar != null)
            {
                hpBar.HideBar();
            }

            // 보스를 처치해도 음악은 끊지 않고 그대로 이어 간다 (자라톤은 봉인 상호작용 때 엔딩 곡으로 바뀐다).
            // 보스전 표시만 풀어 두어, 이후 마을 등 다른 지역으로 가면 그 지역의 곡으로 바뀌게 한다.
            // 지금 지역이 전투 곡 지역이라 같은 곡이 재생 중이면 다시 시작하지 않는다
            BackgroundMusicPlayer musicPlayer = FindAnyObjectByType<BackgroundMusicPlayer>();
            if (musicPlayer != null)
            {
                musicPlayer.PlayNormalMusic();
            }

            // 일반 몬스터는 MonsterDeathEffectPlayer가 셰이더 _Cutoff로 녹여 없애지만,
            // 보스 머티리얼은 Standard(불투명)라 _Cutoff가 먹지 않는다. 셰이더에 의존하지 않는 가라앉기 연출을 쓴다.
            // 코루틴은 manager가 아니라 이 컴포넌트에서 돌린다 - manager의 코루틴은 위에서 전부 멈췄다
            StartCoroutine(RemoveCorpse());
        }

        private IEnumerator PlayBodyFall()
        {
            yield return new WaitForSeconds(bodyFallSoundDelay);

            BossSfx.Play(manager.Data.deathBodySounds, refTransform.position, manager.Data.soundVolume * manager.Data.deathVolume);
        }

        private IEnumerator RemoveCorpse()
        {
            yield return new WaitForSeconds(corpseStayTime);

            Vector3 start = refTransform.position;
            Vector3 end = start + Vector3.down * sinkDepth;
            float elapsed = 0f;

            while (elapsed < sinkDuration)
            {
                elapsed += Time.deltaTime;
                // CharacterController는 이미 꺼져 있으므로 위치를 직접 대입해도 무시되지 않는다
                refTransform.position = Vector3.Lerp(start, end, elapsed / sinkDuration);
                yield return null;
            }

            Destroy(manager.gameObject);
        }

        protected override void Update()
        {
            // 사망 후에는 중력 처리도 하지 않는다
        }
    }
}
