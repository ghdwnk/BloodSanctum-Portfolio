using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 모든 NPC의 정보(이름·역할·대화 거리·잡담)를 담은 에셋.
    ///
    /// 에셋: Resources/Data/NPC Data. DataManager가 불러 두고, 각 NPC의 NPCStateManager가
    /// 자기 번호(npcID)로 attributes[npcID - 1]을 꺼내 쓴다. 그래서 목록 순서가 NPC 번호 순서와 같아야 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC Data", menuName = "Scriptable Objects/NPC Data")]
    public class NPCData : ScriptableObject
    {
        /// <summary>NPC 한 명의 정보.</summary>
        [System.Serializable]
        public class Attribute
        {
            [Tooltip("NPC 번호 (1부터). 목록 순서와 같아야 한다")]
            public int id;
            [Tooltip("이름. 머리 위 이름표와 대화창 화자 이름에 쓴다")]
            public string name = string.Empty;
            [Tooltip("머리 위 이름표에 '[역할] 이름'으로 붙는 역할 (예: 대장장이, 상인). 비워 두면 이름만 보인다")]
            public string role = string.Empty;
            [Tooltip("이 거리(m) 안에 들어오면 F키로 말을 걸 수 있다")]
            public float interactionSight;
            [Tooltip("관련 퀘스트가 없을 때 들려주는 잡담. '|'로 페이지를 나눈다")]
            public string idleSmallTalk = string.Empty;
            [Tooltip("엔딩을 본 뒤 잡담 대신 들려주는 후일담. '|'로 페이지를 나눈다. 비워 두면 잡담을 그대로 쓴다")]
            [TextArea(2, 4)] public string postEndingTalk = string.Empty;
        }

        public List<Attribute> attributes = new();
    }
}
