namespace RPGGame
{
    /// <summary>
    /// 엔딩 화면에 보여 줄 이번 판의 플레이 기록.
    /// 세이브에 남길 값이 아니라 한 판의 요약이라 정적 값으로만 들고 있고,
    /// 게임 씬이 새로 시작될 때(MonsterSpawner 초기화) 0으로 되돌린다.
    /// </summary>
    public static class GameStats
    {
        /// <summary>이번 판에 처치한 몬스터 수 (보스 포함).</summary>
        public static int MonsterKills { get; private set; }

        public static void ResetForNewSession()
        {
            MonsterKills = 0;
        }

        public static void AddMonsterKill()
        {
            ++MonsterKills;
        }
    }
}
