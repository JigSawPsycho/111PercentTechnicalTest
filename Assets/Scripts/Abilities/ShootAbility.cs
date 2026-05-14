using HackSlash.Core;
using HackSlash.Enemies;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Fires a Projectile after a windup. Spawn direction comes from the owner's Facing
    /// and the projectile's faction tag comes from the owner's Faction — so dropping this
    /// onto the player versus an enemy automatically routes friendly-fire correctly.
    /// </summary>
    public class ShootAbility : TimedStrikeAbility
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float projectileSpeed = 9f;
        [SerializeField] private float projectileDamage = 8f;

        protected override void OnStrike()
        {
            if (projectilePrefab == null) return;
            Transform origin = muzzle != null ? muzzle : transform;
            int facing = owner != null ? owner.Facing : 1;
            Faction source = owner != null ? owner.Faction : Faction.Enemy;
            var p = Instantiate(projectilePrefab, origin.position, Quaternion.identity);
            p.Launch(new Vector2(facing, 0f), projectileSpeed, projectileDamage, source);
        }
    }
}
