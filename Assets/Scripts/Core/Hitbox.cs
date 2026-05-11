using System.Collections.Generic;
using UnityEngine;

namespace HackSlash.Core
{
    [DisallowMultipleComponent]
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] private Faction owner = Faction.Player;
        [SerializeField] private float damage = 10f;
        [SerializeField] private Vector2 size = new Vector2(1.2f, 1f);
        [SerializeField] private Vector2 offset = new Vector2(0.7f, 0f);
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float knockback = 4f;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.3f, 0.3f, 0.4f);

        private readonly HashSet<IDamageable> hitThisSwing = new();
        private readonly Collider2D[] buffer = new Collider2D[8];

        public Faction Owner { get => owner; set => owner = value; }
        public float Damage { get => damage; set => damage = value; }

        public void BeginSwing()
        {
            hitThisSwing.Clear();
        }

        public int FacingSign
        {
            get
            {
                Vector3 s = transform.lossyScale;
                return s.x >= 0f ? 1 : -1;
            }
        }

        public int Strike()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(offset.x * FacingSign, offset.y);
            int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, buffer, targetMask);
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                var col = buffer[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                if (dmg.Faction == owner) continue;
                if (!dmg.IsAlive) continue;
                if (!hitThisSwing.Add(dmg)) continue;

                Vector2 origin = transform.position;
                Vector2 dir = new Vector2(FacingSign, 0f);
                dmg.TakeDamage(new DamageInfo
                {
                    Amount = damage,
                    Source = owner,
                    Origin = origin,
                    Knockback = dir * knockback
                });
                hits++;
            }
            return hits;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            int sign = Application.isPlaying ? FacingSign : (transform.lossyScale.x >= 0f ? 1 : -1);
            Vector2 center = (Vector2)transform.position + new Vector2(offset.x * sign, offset.y);
            Gizmos.DrawCube(center, size);
        }
    }
}
