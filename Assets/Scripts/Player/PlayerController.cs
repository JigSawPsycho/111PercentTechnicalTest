using System.Collections.Generic;
using HackSlash.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HackSlash.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 12f;

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed = 14f;
        [SerializeField] private float dodgeDuration = 0.25f;
        [SerializeField] private float dodgeCooldown = 0.9f;

        [Header("Charge Dash (Secondary)")]
        [SerializeField] private float secondaryChargeDuration = 0.5f;
        [SerializeField] private float secondaryDistanceMultiplier = 1.5f;
        [SerializeField] private float secondarySpeedMultiplier = 2f;
        [SerializeField] private float secondaryCooldown = 1.5f;
        [SerializeField] private Color secondaryFlashColor = new Color(1f, 0.45f, 0.15f, 1f);
        [SerializeField] private float secondaryFlashHz = 8f;
        [SerializeField] private float secondaryDamage = 25f;
        [SerializeField] private float secondaryKnockback = 6f;
        [SerializeField] private Vector2 secondaryHitSize = new Vector2(1.6f, 1.8f);
        [SerializeField] private LayerMask secondaryTargetMask = ~0;

        [Header("Refs")]
        [SerializeField] private GroundCheck groundCheck;
        [SerializeField] private Transform spriteRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat combat;

        [Header("Animator Params")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string verticalVelocityParam = "VerticalVelocity";

        private Rigidbody2D rb;
        private float moveInput;
        private bool jumpQueued;
        private float dodgeUntil;
        private float dodgeReadyAt;
        private int facing = 1;

        private SpriteRenderer secondarySprite;
        private Color secondarySpriteBaseColor = Color.white;
        private bool secondaryCharging;
        private float secondaryChargeStartedAt;
        private float secondaryDashUntil;
        private float secondaryReadyAt;
        private int secondaryDashDir = 1;
        private readonly HashSet<IDamageable> secondaryHits = new();
        private readonly Collider2D[] secondaryBuffer = new Collider2D[16];

        public bool IsDodging => Time.time < dodgeUntil;
        public bool IsCharging => secondaryCharging;
        public bool IsChargeDashing => Time.time < secondaryDashUntil;
        public bool CanAct => playerHealth == null || playerHealth.CanAct;
        public int Facing => facing;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            if (animator == null && spriteRoot != null) animator = spriteRoot.GetComponent<Animator>();
            if (spriteRoot != null)
            {
                secondarySprite = spriteRoot.GetComponent<SpriteRenderer>();
                if (secondarySprite != null) secondarySpriteBaseColor = secondarySprite.color;
            }
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                var health = GetComponent<Health>();
                gm.RegisterPlayer(transform, health);
            }
        }

        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>().x;
        }

        public void OnJump(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAct) return;
            if (IsCharging || IsChargeDashing) return;
            if (groundCheck != null && groundCheck.IsGrounded)
                jumpQueued = true;
        }

        public void OnDodge(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAct) return;
            if (IsCharging || IsChargeDashing) return;
            if (Time.time < dodgeReadyAt) return;
            TriggerDodge();
        }

        public void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAct) return;
            if (IsDodging) return;
            if (IsCharging || IsChargeDashing) return;
            if (combat != null) combat.TryAttack();
        }

        public void OnSecondaryAttack(InputValue value)
        {
            if (value.isPressed) BeginSecondaryCharge();
            else CancelSecondaryCharge();
        }

        private void TriggerDodge()
        {
            dodgeUntil = Time.time + dodgeDuration;
            dodgeReadyAt = Time.time + dodgeCooldown;
            if (playerHealth != null) playerHealth.SetInvulnerable(dodgeDuration);
            int dir = Mathf.Abs(moveInput) > 0.1f ? (int)Mathf.Sign(moveInput) : facing;
            rb.linearVelocity = new Vector2(dir * dodgeSpeed, rb.linearVelocity.y);
        }

        private void BeginSecondaryCharge()
        {
            if (!CanAct) return;
            if (IsDodging || IsChargeDashing || secondaryCharging) return;
            if (Time.time < secondaryReadyAt) return;
            if (combat != null && combat.IsLocked) return;
            secondaryCharging = true;
            secondaryChargeStartedAt = Time.time;
        }

        private void CancelSecondaryCharge()
        {
            if (!secondaryCharging) return;
            secondaryCharging = false;
            RestoreSpriteColor();
        }

        private void TriggerSecondaryDash()
        {
            secondaryCharging = false;
            RestoreSpriteColor();
            secondaryHits.Clear();
            secondaryDashDir = facing;
            float dashSpeed = dodgeSpeed * secondarySpeedMultiplier;
            float distance = dodgeSpeed * dodgeDuration * secondaryDistanceMultiplier;
            float duration = distance / Mathf.Max(0.01f, dashSpeed);
            secondaryDashUntil = Time.time + duration;
            secondaryReadyAt = Time.time + secondaryCooldown;
            if (playerHealth != null) playerHealth.SetInvulnerable(duration);
            rb.linearVelocity = new Vector2(secondaryDashDir * dashSpeed, 0f);
        }

        private void RestoreSpriteColor()
        {
            if (secondarySprite != null) secondarySprite.color = secondarySpriteBaseColor;
        }

        private void Update()
        {
            if (!secondaryCharging) return;

            if (!CanAct)
            {
                CancelSecondaryCharge();
                return;
            }

            float held = Time.time - secondaryChargeStartedAt;
            if (held >= secondaryChargeDuration)
            {
                TriggerSecondaryDash();
                return;
            }

            if (secondarySprite != null)
            {
                float t = (Mathf.Sin(held * secondaryFlashHz * Mathf.PI * 2f) + 1f) * 0.5f;
                secondarySprite.color = Color.Lerp(secondarySpriteBaseColor, secondaryFlashColor, t);
            }
        }

        private void FixedUpdate()
        {
            bool grounded = groundCheck != null && groundCheck.IsGrounded;
            bool acting = !CanAct || IsDodging || IsCharging || IsChargeDashing || (combat != null && combat.IsLocked);

            float vx = rb.linearVelocity.x;
            float vy = rb.linearVelocity.y;

            if (IsChargeDashing)
            {
                vx = secondaryDashDir * dodgeSpeed * secondarySpeedMultiplier;
                vy = 0f;
                ScanDashHits();
            }
            else if (IsDodging)
            {
                // velocity already set in TriggerDodge; keep horizontal momentum
            }
            else if (!acting)
            {
                vx = moveInput * moveSpeed;
            }
            else
            {
                // damp horizontal motion while attacking on ground
                if (grounded) vx = Mathf.MoveTowards(vx, 0f, moveSpeed * 4f * Time.fixedDeltaTime);
            }

            if (!IsChargeDashing)
            {
                if (jumpQueued && grounded && !acting)
                {
                    vy = jumpForce;
                    jumpQueued = false;
                }
                else if (!grounded || !jumpQueued)
                {
                    jumpQueued = false;
                }
            }

            rb.linearVelocity = new Vector2(vx, vy);

            if (!acting && !IsDodging && Mathf.Abs(moveInput) > 0.05f)
                facing = moveInput > 0f ? 1 : -1;

            ApplyFacing();
            UpdateAnimator(grounded, vx, vy);
        }

        private void ScanDashHits()
        {
            Vector2 center = rb.position;
            int count = Physics2D.OverlapBoxNonAlloc(center, secondaryHitSize, 0f, secondaryBuffer, secondaryTargetMask);
            for (int i = 0; i < count; i++)
            {
                var col = secondaryBuffer[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                if (dmg.Faction == Faction.Player) continue;
                if (!dmg.IsAlive) continue;
                if (!secondaryHits.Add(dmg)) continue;

                Vector2 dir = new Vector2(secondaryDashDir, 0f);
                dmg.TakeDamage(new DamageInfo
                {
                    Amount = secondaryDamage,
                    Source = Faction.Player,
                    Origin = center,
                    Knockback = dir * secondaryKnockback
                });
            }
        }

        private void ApplyFacing()
        {
            if (spriteRoot == null) return;
            Vector3 s = spriteRoot.localScale;
            s.x = Mathf.Abs(s.x) * facing;
            spriteRoot.localScale = s;
        }

        private void UpdateAnimator(bool grounded, float vx, float vy)
        {
            if (animator == null) return;
            animator.SetFloat(speedParam, Mathf.Abs(vx));
            animator.SetBool(groundedParam, grounded);
            animator.SetFloat(verticalVelocityParam, vy);
        }
    }
}
