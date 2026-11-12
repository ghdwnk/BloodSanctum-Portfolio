using System;
using System.IO;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 세이브 파일 입출력을 맡는 정적 클래스. 세이브 슬롯은 하나뿐이다.
    ///
    /// 파일 위치: Application.persistentDataPath/save.json
    /// (macOS는 ~/Library/Application Support/회사명/제품명/save.json)
    ///
    /// 로비에서 "이어하기"를 누르면 ShouldLoadOnStart를 켜 두고 게임 씬으로 넘어가며,
    /// 게임 씬의 GameSaveLoader가 시작할 때 이 값을 보고 세이브를 적용한다.
    /// </summary>
    public static class SaveManager
    {
        private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "save.json");
        // 저장 도중 문제가 생겨도 기존 세이브가 반쯤 덮어써진 채로 남지 않게, 임시 파일에 먼저 쓴 뒤 바꿔 넣는다
        private static readonly string TempPath = SavePath + ".tmp";

        /// <summary>
        /// 게임 씬이 시작할 때 세이브를 적용해야 하는지. 씬이 바뀌어도 유지돼야 하므로 정적으로 둔다.
        /// </summary>
        public static bool ShouldLoadOnStart { get; set; }

        public static bool HasSaveData()
        {
            return File.Exists(SavePath);
        }

        /// <summary>
        /// 데이터를 파일로 저장한다. 디스크 오류 등으로 실패해도 예외를 밖으로 던지지 않고 로그만 남긴다.
        /// 자동 저장 코루틴 안에서 예외가 나면 코루틴이 멈춰 이후 자동 저장이 모두 끊기기 때문이다.
        /// </summary>
        public static void Save(SaveData data)
        {
            try
            {
                File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));

                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                File.Move(TempPath, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 저장에 실패했습니다: {e.Message}");
            }
        }

        /// <summary>
        /// 세이브 파일을 읽는다. 파일이 없거나 내용이 깨져 읽을 수 없으면 null을 돌려준다.
        /// </summary>
        public static SaveData Load()
        {
            if (!HasSaveData())
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 세이브 파일을 읽지 못했습니다: {e.Message}");
                return null;
            }
        }

        /// <summary>세이브 파일을 지운다 (로비에서 "새로 하기"를 고른 경우).</summary>
        public static void DeleteSave()
        {
            if (HasSaveData())
            {
                File.Delete(SavePath);
            }
        }
    }
}
