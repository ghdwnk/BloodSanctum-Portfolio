#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// GameTestTools 인스펙터. 기능별 접이식 구역으로 나누고 값·버튼을 한 구역에 모아 둔다.
    /// 값은 플레이 전에도 고쳐 둘 수 있고, 버튼은 플레이 모드에서만 눌린다
    /// </summary>
    [CustomEditor(typeof(GameTestTools))]
    public class GameTestToolsEditor : Editor
    {
        private static readonly Color PlayerColor = new Color(0.55f, 0.85f, 0.55f);
        private static readonly Color ItemColor = new Color(0.95f, 0.8f, 0.4f);
        private static readonly Color EquipColor = new Color(0.75f, 0.65f, 0.95f);
        private static readonly Color QuestColor = new Color(0.5f, 0.8f, 0.95f);
        private static readonly Color WorldColor = new Color(0.95f, 0.65f, 0.5f);
        private static readonly Color BossColor = new Color(0.95f, 0.5f, 0.5f);

        // 퀘스트 목록은 길어서 따로 접을 수 있게 한다
        private const string QuestListKey = "GameTestTools.QuestList";

        private GameTestTools tools;

        // 플레이 중에는 상태 요약이 실시간으로 바뀌므로 계속 다시 그린다
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            tools = (GameTestTools)target;
            serializedObject.Update();

            DrawStatus();

            Section("player", "플레이어  (체력 · 레벨 · 포인트 · 스킬)", PlayerColor, DrawPlayer);
            Section("item", "재화 · 아이템  (골드 · 강화석 · 아이템 지급)", ItemColor, DrawItems);
            Section("equip", "장비  (지급 · 착용 · 강화)", EquipColor, DrawEquipment);
            Section("quest", "퀘스트  (어느 단계로든 클리어 / 초기화)", QuestColor, DrawQuests);
            Section("world", "월드 · 전투  (이동 · 몬스터 · 게임 속도)", WorldColor, DrawWorld);
            Section("boss", "보스전", BossColor, DrawBoss);

            serializedObject.ApplyModifiedProperties();
        }

        // ── 맨 위 상태 요약 ──

        private void DrawStatus()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("값은 지금도 고쳐 둘 수 있고, 버튼은 플레이 모드에서만 눌립니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(tools.BuildStatusText(), MessageType.None);
            EditorGUILayout.HelpBox(
                "테스트로 바꾼 레벨·골드·퀘스트 상태도 60초마다 save.json에 자동 저장됩니다. 테스트가 끝나면 저장 파일을 확인하세요.",
                MessageType.Warning);
        }

        // ── 구역별 화면 ──

        private void DrawPlayer()
        {
            Label("체력 / MP");
            Prop("playerMaxHP", "최대 체력");
            Prop("playerHPRatio", "현재 체력 비율");
            Prop("keepPlayerMaxHP", "최대 체력 고정", "켜면 레벨업·장비 교체로 최대 체력이 다시 계산돼도 위 값으로 되돌려 놓는다");
            Prop("godMode", "무적 (체력 항상 가득)", "켜면 체력이 줄어도 매 프레임 최대치로 되돌린다. 최대 체력도 넉넉히 올려 두는 것이 안전하다");
            Prop("infiniteMP", "무한 MP");
            Row(("체력 적용", tools.ApplyPlayerHP), ("체력 완전 회복", tools.HealPlayerFull), ("MP 채우기", tools.FillMP));

            Space();
            Label("레벨 / 경험치");
            Prop("targetLevel", "목표 레벨");
            Button("레벨 적용  (올리면 포인트·레벨업 연출 포함)", tools.ApplyLevel, 24f);
            Prop("expAmount", "경험치 양");
            Button("경험치 추가", tools.AddExp);

            Space();
            Label("포인트 / 스킬");
            Prop("skillPointAmount", "스킬 포인트 양");
            Prop("statPointAmount", "스탯 포인트 양");
            Row(("스킬 포인트 지급", tools.GiveSkillPoints), ("스탯 포인트 지급", tools.GiveStatPoints));
            Button("스킬 전부 배우고 최대 레벨로", tools.UnlockAllSkills);
        }

        private void DrawItems()
        {
            Label("골드");
            Prop("goldAmount", "골드 양");
            Row(("골드를 이 값으로 설정", tools.SetGold), ("골드 더하기", tools.AddGold));

            Space();
            Label("강화석");
            Prop("enhanceStoneAmount", "강화석 개수");
            Row(("강화석을 이 개수로 설정", tools.SetEnhanceStones), ("강화석 더하기", tools.AddEnhanceStones));

            Space();
            Label("아이템 지급");
            Prop("giveItem", "지급할 아이템");
            Prop("giveItemCount", "개수");
            Row(("아이템 지급", tools.GiveItem), ("모든 소모품·재료 지급", tools.GiveAllConsumables));
        }

        private void DrawEquipment()
        {
            Prop("rollEquipmentStats", "스탯 랜덤 굴림", "끄면 원본 수치 그대로, 켜면 실제 드랍처럼 스탯이 랜덤으로 정해진다");

            Space();
            Label("모든 장비");
            Button("모든 장비 1종씩 지급", tools.GiveAllEquipment);

            Space();
            Label("골라서 착용  (레벨 제한이 없어 어떤 장비든 입을 수 있다)");
            Prop("equipItem", "장비");
            Prop("equipItemEnhanceLevel", "강화 단계");
            Button("지급하고 바로 착용", tools.GiveAndEquip, 24f);

            Space();
            Label("착용 중인 장비 강화");
            Prop("equippedEnhanceLevel", "맞출 강화 단계");
            Button("착용 장비 강화 단계 적용", tools.ApplyEquippedEnhance);
        }

        private void DrawQuests()
        {
            Prop("giveQuestRewards", "보상도 지급", "켜면 강제로 완료하는 퀘스트의 골드·경험치·아이템도 받는다. 끄면 상태만 바뀐다");
            EditorGUILayout.HelpBox("선행 퀘스트는 자동으로 먼저 완료됩니다. 순서를 신경 쓰지 않고 원하는 퀘스트만 고르세요.", MessageType.None);

            Space();
            Label("대상 퀘스트까지 한 번에 이동");
            Prop("jumpToQuestID", "대상 퀘스트 ID");

            // 입력한 ID가 어떤 퀘스트인지 바로 알 수 있게 제목을 보여 준다
            if (Application.isPlaying)
            {
                int id = serializedObject.FindProperty("jumpToQuestID").intValue;
                string title = tools.GetQuestTitle(id);
                EditorGUILayout.LabelField("    → " + (title != null ? $"[{id}] {title}" : "없는 퀘스트 ID"), EditorStyles.miniLabel);
            }

            Prop("jumpClearsTarget", "대상 퀘스트도 클리어", "끄면 선행 퀘스트만 클리어해 대상 퀘스트를 '수락 가능' 상태로 남긴다. 그 퀘스트 자체를 테스트할 때 쓴다");
            Prop("resetBeforeJump", "먼저 전체 초기화", "앞 단계로 돌아가려면 켜 둔다. 끄면 이미 완료한 퀘스트는 그대로 둔다");
            Button("대상 퀘스트까지 클리어  (선행 퀘스트 자동 포함)", tools.QuestJump, 24f);

            Space();
            Label("일괄");
            Row(("메인 전부 클리어", () => tools.QuestClearAll(true)), ("전체 전부 클리어", () => tools.QuestClearAll(false)));
            Row(("진행 중 목표 전부 달성", tools.QuestReachAllObjectives), ("진행 중 전부 완료", tools.QuestCompleteAllInProgress));
            Button("모든 퀘스트 처음 상태로 초기화", tools.QuestResetAll);

            Space();
            DrawQuestList();
        }

        private void DrawQuestList()
        {
            bool open = SessionState.GetBool(QuestListKey, true);
            open = EditorGUILayout.Foldout(open, "퀘스트별 조작", true);
            SessionState.SetBool(QuestListKey, open);

            if (!open)
            {
                return;
            }

            if (!Application.isPlaying || QuestManager.Instance == null || tools.QuestList == null)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 퀘스트 목록이 표시됩니다.", MessageType.None);
                return;
            }

            GUIStyle small = new GUIStyle(EditorStyles.miniButton);

            foreach (QuestData.Quest quest in tools.QuestList)
            {
                QuestManager.QuestProgressState state = QuestManager.Instance.GetState(quest.questID);
                int progress = QuestManager.Instance.GetProgress(quest.questID);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string category = quest.category == QuestData.Category.Main ? "메인" : "서브";
                    string label = $"[{quest.questID}] {quest.questTitle}  ({category})\n    {StateName(state)}  {progress}/{quest.countToComplete}";

                    Color previous = GUI.contentColor;
                    GUI.contentColor = StateColor(state);
                    EditorGUILayout.LabelField(label, GUILayout.Height(32f));
                    GUI.contentColor = previous;

                    int id = quest.questID;
                    if (GUILayout.Button("수락", small, GUILayout.Width(40f), GUILayout.Height(30f))) tools.QuestAccept(id);
                    if (GUILayout.Button("목표달성", small, GUILayout.Width(56f), GUILayout.Height(30f))) tools.QuestReachObjective(id);
                    if (GUILayout.Button("완료", small, GUILayout.Width(40f), GUILayout.Height(30f))) tools.QuestComplete(id);
                    if (GUILayout.Button("초기화", small, GUILayout.Width(48f), GUILayout.Height(30f))) tools.QuestReset(id);
                }
            }
        }

        private void DrawWorld()
        {
            Label("이동");
            Button("마을로 이동", tools.TeleportToVillage);

            if (Application.isPlaying)
            {
                ZoneArea[] zones = FindObjectsByType<ZoneArea>()
                    .OrderBy(zone => zone.ZoneName).ToArray();

                // 지역 이동 버튼은 한 줄에 두 개씩 놓는다
                for (int ix = 0; ix < zones.Length; ix += 2)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        for (int offset = 0; offset < 2; ++offset)
                        {
                            if (ix + offset >= zones.Length)
                            {
                                GUILayout.FlexibleSpace();
                                continue;
                            }

                            ZoneArea zone = zones[ix + offset];
                            if (GUILayout.Button(zone.ZoneName))
                            {
                                tools.TeleportToZone(zone);
                            }
                        }
                    }
                }
            }

            Space();
            Label("몬스터");
            Button("필드의 몬스터 전부 처치 (보스 제외)", tools.KillAllMonsters);

            Space();
            Label("게임 속도");
            Prop("gameSpeed", "속도 배율");
            Row(("속도 적용", tools.ApplyGameSpeed), ("1배속으로", tools.ResetGameSpeed));
        }

        private void DrawBoss()
        {
            Prop("bossType", "보스 종류");
            Prop("bossLevel", "보스 레벨");
            Prop("bossStartHPRatio", "시작 체력 비율", "50% 이하로 두면 2페이즈부터 바로 확인할 수 있다");
            Prop("playerStartDistance", "플레이어 시작 거리");
            Prop("skipEncounterDialogue", "등장 대사 건너뛰기");
            Prop("healPlayerOnStart", "시작할 때 체력 회복");
            Button("보스전 시작 (보스 소환 + 플레이어 이동)", tools.StartBossFight, 28f);

            Space();
            Prop("bossHPCutRatio", "체력 깎기 비율", "한 번에 최대 체력의 이 비율만큼 깎는다. 0.5면 가득 찬 보스가 50%가 되어 2페이즈로 넘어간다");
            Button("보스 체력 깎기 (2페이즈 테스트)", tools.CutBossHP);
            Row(("보스 즉시 처치", tools.KillBoss), ("보스 제거", tools.RemoveBoss));
        }

        // ── 화면 구성 도우미 ──

        // 색을 입힌 제목 줄을 눌러 접고 펴는 구역. 펼친 상태는 에디터를 다시 열어도 유지된다
        private void Section(string key, string title, Color color, System.Action body)
        {
            string stateKey = "GameTestTools." + key;
            bool open = SessionState.GetBool(stateKey, key == "player" || key == "quest");

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUIStyle header = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fixedHeight = 26f
            };

            if (GUILayout.Button((open ? "▼  " : "▶  ") + title, header))
            {
                open = !open;
                SessionState.SetBool(stateKey, open);
            }

            GUI.backgroundColor = previous;

            if (open)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    body();
                }
            }

            GUILayout.Space(4f);
        }

        private static void Label(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private static void Space()
        {
            GUILayout.Space(6f);
        }

        private void Prop(string name, string label, string tooltip = null)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"필드를 찾을 수 없습니다: {name}", MessageType.Error);
                return;
            }

            EditorGUILayout.PropertyField(property, tooltip == null ? new GUIContent(label) : new GUIContent(label, tooltip));
        }

        // 버튼 하나. 플레이 모드가 아니면 눌리지 않는다. 눌리면 인스펙터 값을 먼저 대상에 반영한 뒤 실행한다
        private void Button(string label, System.Action action, float height = 20f)
        {
            bool clicked;
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                clicked = GUILayout.Button(label, GUILayout.Height(height));
            }

            if (clicked)
            {
                serializedObject.ApplyModifiedProperties();
                action();
            }
        }

        // 버튼 여러 개를 한 줄에 놓는다
        private void Row(params (string label, System.Action action)[] buttons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach ((string label, System.Action action) in buttons)
                {
                    Button(label, action);
                }
            }
        }

        private static string StateName(QuestManager.QuestProgressState state)
        {
            switch (state)
            {
                case QuestManager.QuestProgressState.Available: return "수락 가능";
                case QuestManager.QuestProgressState.Accepted: return "진행 중";
                case QuestManager.QuestProgressState.ReadyToTurnIn: return "보고 대기";
                case QuestManager.QuestProgressState.Completed: return "완료";
                default: return "잠김";
            }
        }

        private static Color StateColor(QuestManager.QuestProgressState state)
        {
            switch (state)
            {
                case QuestManager.QuestProgressState.Available: return new Color(0.5f, 0.9f, 0.5f);
                case QuestManager.QuestProgressState.Accepted: return new Color(1f, 0.9f, 0.4f);
                case QuestManager.QuestProgressState.ReadyToTurnIn: return new Color(1f, 0.65f, 0.3f);
                case QuestManager.QuestProgressState.Completed: return new Color(0.45f, 0.85f, 1f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }
    }
}
#endif
