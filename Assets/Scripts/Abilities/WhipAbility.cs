using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Second hit of a melee combo. Gated by a MeleeComboCoordinator when one is wired
    /// in — without a coordinator the whip can fire freely, keeping the class usable on
    /// any character that doesn't care about chaining.
    /// </summary>
    public class WhipAbility : MeleeSwingAbility
    {
        [SerializeField] private MeleeComboCoordinator coordinator;

        protected override bool CanActivateInternal()
        {
            return coordinator == null || coordinator.WhipUnlocked;
        }
    }
}
