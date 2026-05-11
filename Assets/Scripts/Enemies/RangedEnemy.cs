using HackSlash.Core;
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
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float shootCooldown = 1.8f;
        [SerializeField] private float shootWindup = 0.3f;
        [SerializeField] private float shootRecovery = 0.35f;
        [SerializeField] private float projectileSpeed = 9f;
        [SerializeField] private float projectileDamage = 8f;
        [SerializeField] private string shootTrigger = "Shoot";

        private float nextShotAt;
        private float shootLockUntil;
        private float fireAt;
        private bool shotFired;

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

            bool locked = InHitstun || Time.time < shootLockUntil;

            if (!locked)
            {
                if (dist < retreatDistance)
                    vx = -dirToPlayer * moveSpeed;
                else if (dist > preferredDistance + distanceTolerance)
                    vx = dirToPlayer * moveSpeed;
                else if (Time.time >= nextShotAt)
                    StartShot();
            }

            FaceTowards(dx);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            UpdateLocomotionAnimator(vx);

            if (!shotFired && Time.time >= fireAt && Time.time < shootLockUntil)
            {
                shotFired = true;
                Fire();
            }
        }

        private void StartShot()
        {
            shootLockUntil = Time.time + shootWindup + shootRecovery;
            fireAt = Time.time + shootWindup;
            shotFired = false;
            nextShotAt = Time.time + shootCooldown;
            if (animator != null) animator.SetTrigger(shootTrigger);
        }

        private void Fire()
        {
            if (projectilePrefab == null) return;
            Transform origin = muzzle != null ? muzzle : transform;
            Vector2 dir = new Vector2(facing, 0f);
            var p = Instantiate(projectilePrefab, origin.position, Quaternion.identity);
            p.Launch(dir, projectileSpeed, projectileDamage, Faction.Enemy);
        }

        protected override void CancelAttack()
        {
            shotFired = true;
            shootLockUntil = 0f;
        }
    }
}
