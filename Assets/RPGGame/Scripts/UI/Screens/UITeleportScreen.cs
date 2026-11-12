using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 텔레포트 중에 화면을 덮는 로딩 화면.
    /// 로비의 씬 로딩 화면(UISceneLoader)과 같은 외형(진행 바 + 팁)을 쓰되,
    /// 씬을 바꾸는 대신 같은 씬 안에서 플레이어를 옮긴다.
    /// 실제 이동은 첫 프레임에 끝나고, 나머지는 위치 이동을 감추는 연출 시간이다
    /// </summary>
    public class UITeleportScreen : MonoBehaviour
    {
        private static UITeleportScreen instance;

        [SerializeField] private GameObject panel;
        [SerializeField] private Image progressBar;
        [Tooltip("진행 바가 차오르는 시간. 로비 로딩과 같은 체감을 주도록 비슷하게 둔다")]
        [SerializeField] private float loadingTime = 2.5f;
        [SerializeField] private LoadingTipCollection tipCollection;
        [SerializeField] private TMP_Text tipText;

        // 텔레포트 도중에 다른 게이트가 반응하지 않도록 외부에 알린다
        public static bool IsOn { get; private set; }
        // 마지막 텔레포트가 끝난 시각. 도착 지점 옆의 게이트가 곧바로 되받아 보내지 않도록 게이트가 참조한다
        public static float LastTeleportEndTime { get; private set; } = -999f;

        /// <summary>텔레포트가 끝나 로딩 화면이 걷힌 직후 한 번 알린다 (엔딩 후 마을 도착 대사처럼 도착 뒤에 할 일에 쓴다).</summary>
        public static event System.Action TeleportFinished;

        private void Awake()
        {
            instance = this;

            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                IsOn = false;
            }
        }

        // 플레이어를 목적지로 옮기면서 로딩 화면을 보여주는 함수.
        // 화면이 없는 씬에서도 게이트가 동작하도록, 인스턴스가 없으면 연출 없이 바로 옮긴다
        public static void Teleport(Transform player, Vector3 position, Quaternion rotation)
        {
            if (instance == null)
            {
                Util.TeleportCharacter(player, position, rotation);
                CameraRig.SetYaw(rotation.eulerAngles.y);
                CameraRig.SnapToTarget();
                LastTeleportEndTime = Time.time;
                return;
            }

            if (IsOn)
            {
                return;
            }

            instance.StartCoroutine(instance.TeleportRoutine(player, position, rotation));
        }

        private IEnumerator TeleportRoutine(Transform player, Vector3 position, Quaternion rotation)
        {
            IsOn = true;

            // 로딩 화면이 떠 있는 동안은 배경음악을 멈춘다 (끝나면 도착한 지역의 음악이 나온다)
            BackgroundMusicPlayer.SetLoadingScreen(true);

            ShowNextTip();

            if (progressBar != null)
            {
                progressBar.fillAmount = 0f;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }

            // 화면이 덮인 다음 프레임에 옮긴다. 같은 프레임에 옮기면 이동 장면이 한 프레임 보인다
            yield return null;

            Util.TeleportCharacter(player, position, rotation);
            // 캐릭터만 돌리면 카메라는 이전 지역에서 보던 방향 그대로라, 도착하면 엉뚱한 쪽을 보게 된다.
            // 도착 지점이 정한 방향(보스·사냥터 안쪽)을 카메라도 똑같이 바라보게 맞춘다
            CameraRig.SetYaw(rotation.eulerAngles.y);
            CameraRig.SnapToTarget();

            float elapsed = 0f;

            while (elapsed < loadingTime)
            {
                elapsed += Time.deltaTime;

                if (progressBar != null)
                {
                    progressBar.fillAmount = Mathf.Clamp01(elapsed / loadingTime);
                }

                yield return null;
            }

            // 바가 다 찬 모습을 잠깐 보여준 뒤 닫는다 (UISceneLoader와 같은 여운)
            yield return new WaitForSeconds(0.3f);

            if (panel != null)
            {
                panel.SetActive(false);
            }

            LastTeleportEndTime = Time.time;
            IsOn = false;

            BackgroundMusicPlayer.SetLoadingScreen(false);

            TeleportFinished?.Invoke();
        }

        // 팁을 순서대로 보여준다. 로비 로딩과 같은 목록을 쓰므로 순서도 이어서 돈다
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
    }
}
