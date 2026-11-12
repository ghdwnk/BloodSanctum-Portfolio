namespace RPGGame
{
    /// <summary>
    /// 플레이어 사망 상태. 사망 애니메이션은 애니메이터가 State 값으로 재생하고,
    /// 부활 처리는 PlayerRespawnController가 맡는다. 이 상태는 입력을 받지 않는 것 외에 하는 일이 없다.
    /// </summary>
    public class PlayerDeadState : PlayerStateBase
    {
    }
}
