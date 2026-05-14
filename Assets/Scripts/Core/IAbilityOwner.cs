namespace HackSlash.Core
{
    /// <summary>
    /// Minimum contract any character must satisfy for abilities to act on its behalf.
    /// Implemented by PlayerController and EnemyBase so the same ability class works on either.
    /// </summary>
    public interface IAbilityOwner
    {
        Faction Faction { get; }
        int Facing { get; }
    }

    /// <summary>
    /// Optional secondary contract: characters whose abilities can use directional input
    /// (currently DodgeAbility) implement this. Enemies omit it; dodge falls back to facing.
    /// </summary>
    public interface IMoveInputProvider
    {
        float MoveInputX { get; }
    }
}
