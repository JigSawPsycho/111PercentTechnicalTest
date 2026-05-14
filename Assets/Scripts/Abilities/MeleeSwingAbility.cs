using System;
using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Generic single melee swing. Delegates strike to a Hitbox child, whose owner faction
    /// is automatically aligned with the host character — so the same component works for
    /// the player's punch and an enemy's claw without any code changes.
    /// </summary>
    public class MeleeSwingAbility : TimedStrikeAbility
    {
        [SerializeField] private Hitbox hitbox;

        public event Action<int> SwingHit;

        protected override void Awake()
        {
            base.Awake();
            if (hitbox != null && owner != null) hitbox.Owner = owner.Faction;
        }

        protected override void OnSwingStart()
        {
            if (hitbox != null) hitbox.BeginSwing();
        }

        protected override void OnStrike()
        {
            if (hitbox == null) return;
            int hits = hitbox.Strike();
            if (hits > 0) SwingHit?.Invoke(hits);
        }
    }
}
