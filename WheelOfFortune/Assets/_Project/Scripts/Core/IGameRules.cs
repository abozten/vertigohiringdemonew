namespace WheelOfFortune.Core
{
    /// <summary>
    /// Policies used by a session: zone classification, wheel selection, and reward growth.
    /// Implementations may come from an Inspector asset or ordinary C# code.
    /// </summary>
    public interface IGameRules
    {
        ZoneType GetZoneType(int zone);
        WheelDefinition GetWheel(ZoneType type);
        int GetAmount(int baseAmount, int zone);
    }
}
