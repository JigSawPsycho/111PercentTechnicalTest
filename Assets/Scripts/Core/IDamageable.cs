using UnityEngine;

namespace HackSlash.Core
{
    public struct DamageInfo
    {
        public float Amount;
        public Faction Source;
        public Vector2 Origin;
        public Vector2 Knockback;
    }

    public interface IDamageable
    {
        Faction Faction { get; }
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
