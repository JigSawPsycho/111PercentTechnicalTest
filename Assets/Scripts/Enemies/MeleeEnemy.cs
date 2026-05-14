using HackSlash.Abilities;
using UnityEngine;

namespace HackSlash.Enemies
{
    public class MeleeEnemy : EnemyBase
    {
        [Header("Melee")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private MeleeSwingAbility attackAbility;

        private void FixedUpdate()
        {
            if (isDead) return;

            if (target == null)
            {
                AcquireTarget();
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                UpdateLocomotionAnimator(0f);
                return;
            }

            float dx = target.position.x - transform.position.x;
            float dist = Mathf.Abs(dx);
            float vx = 0f;

            bool locked = InHitstun || (attackAbility != null && attackAbility.IsActive);

            if (!locked)
            {
                if (dist > attackRange * 0.9f)
                    vx = Mathf.Sign(dx) * moveSpeed;
                else if (attackAbility != null)
                    attackAbility.TryActivate();
            }

            FaceTowards(dx);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            UpdateLocomotionAnimator(vx);
        }
    }
}
