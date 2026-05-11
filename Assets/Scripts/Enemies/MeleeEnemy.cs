using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Enemies
{
    public class MeleeEnemy : EnemyBase
    {
        [Header("Melee")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private float attackWindup = 0.25f;
        [SerializeField] private float attackRecovery = 0.45f;
        [SerializeField] private Hitbox hitbox;
        [SerializeField] private string attackTrigger = "Attack1";

        private float nextAttackAt;
        private float attackLockUntil;
        private float strikeAt;
        private bool strikeFired;

        protected override void Awake()
        {
            base.Awake();
            if (hitbox != null) hitbox.Owner = Faction.Enemy;
        }

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

            bool locked = InHitstun || Time.time < attackLockUntil;

            if (!locked)
            {
                if (dist > attackRange * 0.9f)
                    vx = Mathf.Sign(dx) * moveSpeed;
                else if (Time.time >= nextAttackAt)
                    StartAttack();
            }

            FaceTowards(dx);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            UpdateLocomotionAnimator(vx);

            if (!strikeFired && Time.time >= strikeAt && Time.time < attackLockUntil)
            {
                strikeFired = true;
                if (hitbox != null) hitbox.Strike();
            }
        }

        private void StartAttack()
        {
            attackLockUntil = Time.time + attackWindup + attackRecovery;
            strikeAt = Time.time + attackWindup;
            strikeFired = false;
            nextAttackAt = Time.time + attackCooldown;
            if (animator != null) animator.SetTrigger(attackTrigger);
            if (hitbox != null) hitbox.BeginSwing();
        }

        protected override void CancelAttack()
        {
            strikeFired = true;
            attackLockUntil = 0f;
        }
    }
}
