using TMPro;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// NPC 머리 위의 퀘스트 표식. 받을 수 있는 퀘스트가 있으면 보라색 '?', 완료 보고할 퀘스트가 있으면 노란색 '!'를 띄운다.
    ///
    /// NPC 프리팹의 자식에 붙어 있고, 부모의 NPCStateManager 번호로 QuestManager.CheckNPCState를 주기적으로 물어본다.
    /// 퀘스트 상태는 자주 바뀌지 않으므로 매 프레임이 아니라 checkInterval마다 확인하고,
    /// 표식이 위아래로 떠다니는 움직임만 매 프레임 갱신한다.
    /// </summary>
    public class NPCQuestMarker : MonoBehaviour
    {
        [Tooltip("켜고 끌 표식 오브젝트 (월드 스페이스 Canvas)")]
        [SerializeField] private GameObject markerRoot;
        [Tooltip("? / ! 를 표시할 텍스트")]
        [SerializeField] private TextMeshProUGUI markerText;

        [Header("표시")]
        [Tooltip("받을 수 있는 퀘스트가 있을 때의 기호")]
        [SerializeField] private string offerSymbol = "?";
        [Tooltip("완료 보고할 퀘스트가 있을 때의 기호")]
        [SerializeField] private string turnInSymbol = "!";
        [SerializeField] private Color offerColor = new Color(0.78f, 0.42f, 1f);
        [SerializeField] private Color turnInColor = new Color(1f, 0.82f, 0.29f);

        [Header("움직임")]
        [Tooltip("위아래로 떠다니는 높이")]
        [SerializeField] private float bobHeight = 0.12f;
        [Tooltip("위아래로 떠다니는 빠르기")]
        [SerializeField] private float bobSpeed = 2.2f;
        [Tooltip("퀘스트 상태를 다시 확인하는 간격(초)")]
        [SerializeField] private float checkInterval = 0.3f;

        private NPCStateManager manager;
        private Transform markerTransform;
        private Vector3 basePosition;
        private float nextCheckTime;

        private void Awake()
        {
            manager = GetComponentInParent<NPCStateManager>();

            if (markerRoot != null)
            {
                markerTransform = markerRoot.transform;
                basePosition = markerTransform.localPosition;
                markerRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (markerRoot == null)
            {
                return;
            }

            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                Refresh();
            }

            // 표식이 위아래로 떠다니면 멀리서도 눈에 잘 띈다
            if (markerRoot.activeSelf)
            {
                Vector3 position = basePosition;
                position.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                markerTransform.localPosition = position;
            }
        }

        private void Refresh()
        {
            if (manager == null || QuestManager.Instance == null)
            {
                return;
            }

            // 대화 중에는 카메라가 줌인돼 표식이 화면 밖으로 밀려나고, 이미 말을 걸고 있어 안내도 필요 없다
            if (manager.CurrentState == NPCStateManager.State.Talk)
            {
                Hide();
                return;
            }

            switch (QuestManager.Instance.CheckNPCState(manager.NPCID).kind)
            {
                case QuestManager.NPCQuestInteraction.Kind.Offer:
                    Show(offerSymbol, offerColor);
                    break;

                case QuestManager.NPCQuestInteraction.Kind.TurnIn:
                    Show(turnInSymbol, turnInColor);
                    break;

                // 진행 중이거나 볼일이 없으면 숨긴다
                default:
                    Hide();
                    break;
            }
        }

        private void Show(string symbol, Color color)
        {
            // 기호가 같으면 글자를 다시 만들지 않는다 (기호마다 색이 정해져 있어 색도 같이 맞춘다)
            if (markerText != null && markerText.text != symbol)
            {
                markerText.text = symbol;
                markerText.color = color;
            }

            if (!markerRoot.activeSelf)
            {
                markerRoot.SetActive(true);
            }
        }

        private void Hide()
        {
            if (markerRoot.activeSelf)
            {
                markerRoot.SetActive(false);
            }
        }
    }
}
