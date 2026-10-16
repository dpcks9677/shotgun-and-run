namespace ShotgunAndRun.Combat
{
    /// <summary>
    /// 공격 주체의 표시 이름(결과 화면 사망 원인 등에 사용)을 제공하는 인터페이스입니다.
    /// </summary>
    public interface IAttacker
    {
        public string DisplayName
        {
            get;
        }
    }
}
