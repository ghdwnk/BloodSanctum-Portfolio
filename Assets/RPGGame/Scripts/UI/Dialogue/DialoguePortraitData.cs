using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 대화창에 보여줄 화자별 일러스트와 이름표 색을 모아 둔 표.
    /// 화자 이름은 대사 앞에 [이름]을 붙인 것(또는 NPC 데이터의 이름)과 같은 글자여야 연결된다.
    /// 새 캐릭터는 이 에셋에 항목만 추가하면 되고 코드는 건드리지 않는다
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue Portraits", menuName = "Scriptable Objects/Dialogue Portraits")]
    public class DialoguePortraitData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string speakerName = string.Empty;
            public Sprite portrait;
            // 이름표 글자 색
            public Color nameColor = new Color(1f, 0.82f, 0.29f);
        }

        public List<Entry> entries = new();

        public Entry Find(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName))
            {
                return null;
            }

            return entries.Find(e => e.speakerName == speakerName);
        }
    }
}
