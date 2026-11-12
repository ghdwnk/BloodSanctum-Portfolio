using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 몬스터가 죽은 뒤 몸이 서서히 녹아 사라지는(디졸브) 연출. 몬스터 프리팹에 붙어 있다.
    /// HPController.OnDead에 인스펙터로 Play가 연결돼 있다.
    /// 사망 애니메이션을 보여 준 뒤(startTime) 셰이더의 _Cutoff 값을 0→1로 올려 몸을 지우고,
    /// 끝나면 몬스터 오브젝트를 파괴한다.
    /// </summary>
    public class MonsterDeathEffectPlayer : MonoBehaviour
    {
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        [Tooltip("죽은 뒤 사라지기 시작하기까지의 시간(초). 사망 애니메이션을 보여 주는 시간")]
        [SerializeField] private float startTime = 1.2f;
        [Tooltip("사라지는 데 걸리는 시간(초)")]
        [SerializeField] private float playTime = 2.0f;

        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private float elapsedTime;
        private bool isPlaying;

        private void OnEnable()
        {
            // 모델링에서 사용하는 Renderer 컴포넌트를 모두 검색해 배열에 저장
            renderers = transform.root.GetComponentsInChildren<Renderer>();
            // 파라미터 블록 변수 생성
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            // 시각 효과를 재생하지 않을 때는 함수 종료
            if (!isPlaying)
            {
                return;
            }
            
            // Renderer 컴포넌트 배열을 순회하면서 머티리얼 파라미터에 값 설정
            foreach (Renderer renderer in renderers)
            {
                // Renderer에서 머티리얼 프로퍼티 블록의 현재 값 가져오기
                renderer.GetPropertyBlock(propertyBlock);
                
                // 머티리얼 프로퍼티 블록의 _Cutoff 파라미터에 값을 설정
                propertyBlock.SetFloat(CutoffId, elapsedTime / playTime);
                
                // _Cutoff 파라미터를 설정한 머티리얼 프로퍼티 블록을 다시 Renderer에 적용
                renderer.SetPropertyBlock(propertyBlock);
            }
            
            // 경과 시간 업데이트
            elapsedTime += Time.deltaTime;
            
            // 재생 시간이 모두 지났는지 확인
            if (elapsedTime > playTime)
            {
                isPlaying = false;
                Destroy(transform.root.gameObject);
            }
        }
        
        /// <summary>사라지는 연출을 예약한다 (HPController.OnDead에 인스펙터로 연결돼 있다. 이름을 바꾸지 않는다).</summary>
        public void Play()
        {
            // startTime에 설정된 시간만큼 기다렸다가 playDeathEffect함수 실행
            Invoke(nameof(PlayDeathEffect), startTime);
        }
        
        // 몬스터 죽음 시각 효과를 재생하는 함수
        private void PlayDeathEffect()
        {
            elapsedTime = 0f;
            isPlaying = true;
        }
    }
}
