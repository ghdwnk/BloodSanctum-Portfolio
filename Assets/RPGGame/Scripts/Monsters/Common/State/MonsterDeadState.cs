namespace RPGGame
{
    /// <summary>
    /// 몬스터 사망 상태. 사망 애니메이션은 애니메이터가, 시체 처리는 MonsterDeathEffectPlayer가 맡는다.
    /// 이 상태는 아무 일도 하지 않지만, 다른 상태로 바뀌지 않게 막는 표시 역할을 한다.
    /// </summary>
    public class MonsterDeadState : MonsterStateBase
    {
    }
}
