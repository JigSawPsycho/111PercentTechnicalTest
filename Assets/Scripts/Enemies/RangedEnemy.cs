using HackSlash.Abilities;
using UnityEngine;

namespace HackSlash.Enemies
{
    public class RangedEnemy : EnemyBase
    {
        [Header("Kiting")]
        [SerializeField] private float preferredDistance = 6f;
        [SerializeField] private float distanceTolerance = 0.5f;
        [SerializeField] private float retreatDistance = 4f;

        [Header("Shooting")]
        [SerializeField] private ShootAbility shootAbility;

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
            float dirToPlayer = Mathf.Sign(dx);
            float vx = 0f;

            bool locked = InHitstun || (shootAbility != null && shootAbility.IsActive);

            if (!locked)
            {
                if (dist < retreatDistance)
                    vx = -dirToPlayer * moveSpeed;
                else if (dist > preferredDistance + distanceTolerance)
                    vx = dirToPlayer * moveSpeed;
                else if (shootAbility != null)
                    shootAbility.TryActivate();
            }

            FaceTowards(dx);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            UpdateLocomotionAnimator(vx);
        }
    }
}
