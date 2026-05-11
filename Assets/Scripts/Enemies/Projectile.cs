using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private float knockback = 3f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private SpriteRenderer sprite;

        private Rigidbody2D rb;
        private Collider2D col;
        private Faction source;
        private float damage;
        private float dieAt;
        private bool alive = true;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        public void Launch(Vector2 direction, float speed, float damageAmount, Faction sourceFaction)
        {
            source = sourceFaction;
            damage = damageAmount;
            dieAt = Time.time + lifetime;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            rb.linearVelocity = dir * speed;

            if (sprite != null)
            {
                Vector3 s = sprite.transform.localScale;
                s.x = Mathf.Abs(s.x) * (dir.x >= 0f ? 1f : -1f);
                sprite.transform.localScale = s;
            }
        }

        private void Update()
        {
            if (alive && Time.time >= dieAt) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!alive) return;
            if (((1 << other.gameObject.layer) & hitMask) == 0) return;

            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg != null && dmg.Faction != source && dmg.IsAlive)
            {
                Vector2 dir = rb.linearVelocity.normalized;
                dmg.TakeDamage(new DamageInfo
                {
                    Amount = damage,
                    Source = source,
                    Origin = transform.position,
                    Knockback = dir * knockback
                });
                Despawn();
                return;
            }

            // Hit something solid that isn't damageable — vanish on contact.
            if (!other.isTrigger) Despawn();
        }

        private void Despawn()
        {
            alive = false;
            Destroy(gameObject);
        }
    }
}
