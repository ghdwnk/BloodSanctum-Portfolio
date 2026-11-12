using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPGGame
{
    /// <summary>
    /// 화면 한쪽에 진행 중인 퀘스트와 목표 진행도를 상시 표시하는 HUD.
    /// 창(J)을 열지 않아도 무엇을 해야 하는지 보이게 하는 것이 목적이다.
    /// </summary>
    public class UIQuestTracker : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI bodyText;
        [Tooltip("한 번에 표시할 최대 퀘스트 수 (화면을 덮지 않도록 제한)")]
        [SerializeField] private int maxTrackedQuests = 3;

        // 문자열을 매번 새로 만들지 않도록 하나를 재사용한다 (갱신이 잦은 HUD라서)
        private readonly StringBuilder builder = new();
        private bool isSubscribed;

        // 보고만 남은(완료한) 퀘스트가 있을 때 패널 테두리와 해당 줄을 노랗게 깜빡여 눈에 띄게 한다
        private static readonly Color GlowColor = new Color(1f, 0.82f, 0.29f);
        private const float GlowOutset = 14f;
        private const float GlowSpeed = 3.5f;
        private const int PulseSteps = 6;
        private int readyCount;
        private int lastPulseStep = -1;
        private Image glowImage;
        // 글자색도 같이 맥동시키되, 색이 바뀔 때만 텍스트를 다시 만들도록 단계로 쪼갠다
        private string pulseHex = "FFD24A";

        private void OnEnable()
        {
            TrySubscribe();
            Refresh();
        }

        private void Start()
        {
            TrySubscribe();
            Refresh();
        }

        // QuestManager가 아직 준비되지 않았을 수 있어 두 시점에서 시도한다
        private void TrySubscribe()
        {
            if (isSubscribed || QuestManager.Instance == null)
            {
                return;
            }

            QuestManager.Instance.SubscribeOnQuestAccepted(_ => Refresh());
            QuestManager.Instance.SubscribeOnQuestDeclined(_ => Refresh());
            QuestManager.Instance.SubscribeOnQuestProgressChanged((_, __) => Refresh());
            QuestManager.Instance.SubscribeOnQuestReadyToTurnIn(_ => Refresh());
            QuestManager.Instance.SubscribeOnQuestCompleted(_ => Refresh());
            QuestManager.Instance.SubscribeOnQuestReset(_ => Refresh());
            // 이어하기로 불러온 퀘스트 상태를 반영한다 (불러오기는 개별 알림을 보내지 않는다)
            QuestManager.Instance.SubscribeOnStateLoaded(Refresh);

            isSubscribed = true;
        }

        private void Update()
        {
            if (readyCount <= 0)
            {
                if (glowImage != null && glowImage.gameObject.activeSelf)
                {
                    glowImage.gameObject.SetActive(false);
                }

                return;
            }

            // 일시정지 중에도 깜빡이도록 unscaled 시간을 쓴다
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * GlowSpeed);

            EnsureGlow();
            if (glowImage != null)
            {
                glowImage.gameObject.SetActive(true);
                Color color = GlowColor;
                color.a = Mathf.Lerp(0.25f, 1f, pulse);
                glowImage.color = color;
            }

            int step = Mathf.Min(PulseSteps - 1, (int)(pulse * PulseSteps));
            if (step != lastPulseStep)
            {
                lastPulseStep = step;
                pulseHex = ColorUtility.ToHtmlStringRGB(Color.Lerp(new Color(1f, 0.78f, 0.2f), new Color(1f, 0.97f, 0.75f), step / (float)(PulseSteps - 1)));
                Refresh();
            }
        }

        // 패널 뒤에 깔리는 노란 테두리 광채를 처음 필요할 때 코드로 만든다 (씬에 따로 배치하지 않아도 되게)
        private void EnsureGlow()
        {
            if (glowImage != null || panel == null)
            {
                return;
            }

            GameObject go = new GameObject("Ready Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(panel.transform, false);
            // 패널의 레이아웃 그룹이 이 오브젝트를 칸으로 세지 않도록 한다
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            // 글자 뒤에 그려지도록 맨 앞 자식으로 둔다
            go.transform.SetAsFirstSibling();

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-GlowOutset, -GlowOutset);
            rect.offsetMax = new Vector2(GlowOutset, GlowOutset);

            glowImage = go.GetComponent<Image>();
            glowImage.sprite = CreateGlowSprite();
            glowImage.type = Image.Type.Sliced;
            glowImage.raycastTarget = false;
        }

        // 바깥 가장자리에서 안쪽으로 서서히 진해지는 테두리 모양 스프라이트 (가운데는 옅게)
        private static Sprite CreateGlowSprite()
        {
            const int size = 48;
            const int edge = 16;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    float alpha = d < edge ? Mathf.SmoothStep(0f, 0.9f, d / (float)edge) : 0.18f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
        }

        private void Refresh()
        {
            if (QuestManager.Instance == null || DataManager.Instance == null || DataManager.Instance.questData == null)
            {
                return;
            }

            builder.Clear();
            int shown = 0;
            int ready = 0;

            foreach (QuestData.Quest quest in DataManager.Instance.questData.quests)
            {
                if (shown >= maxTrackedQuests)
                {
                    break;
                }

                QuestManager.QuestProgressState state = QuestManager.Instance.GetState(quest.questID);

                // 수락했거나 보고만 남은 퀘스트만 추적한다
                if (state != QuestManager.QuestProgressState.Accepted &&
                    state != QuestManager.QuestProgressState.ReadyToTurnIn)
                {
                    continue;
                }

                if (shown > 0)
                {
                    builder.Append('\n');
                }

                if (state == QuestManager.QuestProgressState.ReadyToTurnIn)
                {
                    // 완료한 퀘스트는 제목도 노랗게 맥동시킨다
                    builder.Append("<color=#").Append(pulseHex).Append("><b>").Append(quest.questTitle).Append("</b></color>\n");
                    ++ready;
                }
                else
                {
                    builder.Append("<color=#E8D9A8>").Append(quest.questTitle).Append("</color>\n");
                }

                builder.Append(BuildObjectiveLine(quest, state));

                ++shown;
            }

            readyCount = ready;
            bool hasAny = shown > 0;

            if (panel != null)
            {
                panel.SetActive(hasAny);
            }

            if (bodyText != null && hasAny)
            {
                bodyText.text = builder.ToString();
            }
        }

        // 목표 한 줄을 만드는 함수. 완료된 퀘스트는 어느 NPC에게 보고할지 알려준다
        private string BuildObjectiveLine(QuestData.Quest quest, QuestManager.QuestProgressState state)
        {
            if (state == QuestManager.QuestProgressState.ReadyToTurnIn)
            {
                // 보고할 NPC가 없는 퀘스트(최종 퀘스트)는 봉인 왕좌에서 마무리한다
                if (quest.turnInNpcID <= 0)
                {
                    return $"<color=#{pulseHex}>→ 붉은 수정 왕좌에서 봉인을 완성하세요</color>";
                }

                return $"<color=#{pulseHex}>→ 완료! {GetNPCName(quest.turnInNpcID)}에게 보고</color>";
            }

            int progress = QuestManager.Instance.GetProgress(quest.questID);
            string label = string.IsNullOrEmpty(quest.objectiveLabel) ? "목표 달성" : quest.objectiveLabel;

            return $"→ {label} <color=#FFD24A>{progress}/{quest.countToComplete}</color>";
        }

        private string GetNPCName(int npcID)
        {
            NPCData npcData = DataManager.Instance.npcData;

            if (npcData == null || npcID < 1 || npcID > npcData.attributes.Count)
            {
                return "의뢰인";
            }

            return npcData.attributes[npcID - 1].name;
        }
    }
}
