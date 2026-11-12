using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // 로딩 팁 텍스트 표시용

namespace RPGGame
{
    /// <summary>
    /// 로비에서 게임 씬으로 넘어가는 로딩 화면. 켜지면 다음 씬을 백그라운드로 불러 두고, 진행 막대를 loadingTime 동안 채운 뒤 씬을 연다.
    /// 진행 막대는 실제 로딩률이 아니라 정해진 시간으로 채우는 연출이다. 로딩 팁은 한 번에 하나씩 순서대로 보여 준다.
    /// </summary>
    public class UISceneLoader : MonoBehaviour
    {
        [SerializeField] private string mainSceneName;
        [SerializeField] private Image progressBar;
        [SerializeField] private float loadingTime = 4f;

        [Tooltip("로딩 팁 표시용 - 로딩 1회당 하나만 보여주고, 처음부터 순서대로 보여준 뒤 마지막 다음엔 다시 처음으로")]
        [SerializeField] private LoadingTipCollection tipCollection;
        [SerializeField] private TMP_Text tipText;

        private void OnEnable()
        {
            // 로딩 화면이 뜨는 동안은 배경음악(타이틀 음악)을 멈춘다. 다음 씬의 음악 플레이어가 켜질 때 상태가 초기화된다
            BackgroundMusicPlayer.SetLoadingScreen(true);

            ShowNextTip();
            StartCoroutine(LoadScene());
        }

        // 팁 목록을 처음부터 순서대로 보여주고, 마지막 항목 다음엔 다시 처음으로 돌아간다.
        // 순환 상태는 팁 애셋이 들고 있어 텔레포트 로딩 화면(UITeleportScreen)과도 순서가 이어진다
        private void ShowNextTip()
        {
            if (tipText == null || tipCollection == null)
            {
                return;
            }

            string tip = tipCollection.NextTip();
            if (tip != null)
            {
                tipText.text = tip;
            }
        }

        private IEnumerator LoadScene()
        {
            // 비동기 방식으로 씬 로드
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(mainSceneName);

            // 씬이 모두 로드되더라도 바로 이동하지 않도록 설정
            if (asyncOperation != null)
            {
                asyncOperation.allowSceneActivation = false;

                // 씬 로드가 완료될 때까지 대기
                yield return asyncOperation.isDone;

                float elapsedTime = 0f;

                // 가짜 진행률이지만 씬이 로드된 것처럼 표시
                while (true)
                {
                    yield return null;

                    elapsedTime += Time.deltaTime;

                    // 진행률 계산
                    float progress = elapsedTime / loadingTime;
                    // 진행률 표시
                    progressBar.fillAmount = progress;

                    // 99퍼센트 이상 진행되면, 완료로 표시하고 씬 이동
                    if (progress >= 0.99f)
                    {
                        progressBar.fillAmount = 1f;
                        break;
                    }
                }

                yield return new WaitForSeconds(0.5f);

                // 씬 이동 허용
                asyncOperation.allowSceneActivation = true;
            }
        }
    }
}
