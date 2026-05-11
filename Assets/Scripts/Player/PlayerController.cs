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

        public bool IsDodging => Time.time < dodgeUntil;
        public bool CanAct => playerHealth == null || playerHealth.CanAct;
        public int Facing => facing;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            if (animator == null && spriteRoot != null) animator = spriteRoot.GetComponent<Animator>();
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
            if (groundCheck != null && groundCheck.IsGrounded)
                jumpQueued = true;
        }

        public void OnDodge(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAct) return;
            if (Time.time < dodgeReadyAt) return;
            TriggerDodge();
        }

        public void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAct) return;
            if (IsDodging) return;
            if (combat != null) combat.TryAttack();
        }

        private void TriggerDodge()
        {
            dodgeUntil = Time.time + dodgeDuration;
            dodgeReadyAt = Time.time + dodgeCooldown;
            if (playerHealth != null) playerHealth.SetInvulnerable(dodgeDuration);
            int dir = Mathf.Abs(moveInput) > 0.1f ? (int)Mathf.Sign(moveInput) : facing;
            rb.linearVelocity = new Vector2(dir * dodgeSpeed, rb.linearVelocity.y);
        }

        private void FixedUpdate()
        {
            bool grounded = groundCheck != null && groundCheck.IsGrounded;
            bool acting = !CanAct || IsDodging || (combat != null && combat.IsLocked);

            float vx = rb.linearVelocity.x;
            if (IsDodging)
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

            float vy = rb.linearVelocity.y;
            if (jumpQueued && grounded && !acting)
            {
                vy = jumpForce;
                jumpQueued = false;
            }
            else if (!grounded || !jumpQueued)
            {
                jumpQueued = false;
            }

            rb.linearVelocity = new Vector2(vx, vy);

            if (!acting && !IsDodging && Mathf.Abs(moveInput) > 0.05f)
                facing = moveInput > 0f ? 1 : -1;

            ApplyFacing();
            UpdateAnimator(grounded, vx, vy);
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
