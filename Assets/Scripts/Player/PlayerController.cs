using HackSlash.Abilities;
using HackSlash.Core;
using HackSlash.Waves;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HackSlash.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour, IAbilityOwner, IMoveInputProvider
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 12f;

        [Header("Refs")]
        [SerializeField] private GroundCheck groundCheck;
        [SerializeField] private Transform spriteRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Abilities")]
        [SerializeField] private MeleeComboCoordinator combo;
        [SerializeField] private DodgeAbility dodge;
        [SerializeField] private ChargeDashAbility chargeDash;
        [SerializeField] private UnstoppableAbility unstoppable;

        [Header("Animator Params")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string verticalVelocityParam = "VerticalVelocity";

        private Rigidbody2D rb;
        private float moveInput;
        private bool jumpQueued;
        private int facing = 1;
        private WaveSpawner waveSpawner;
        private Health health;

        public Faction Faction => Faction.Player;
        public int Facing => facing;
        public float MoveInputX => moveInput;

        public bool IsDodging => dodge != null && dodge.IsActive;
        public bool IsCharging => chargeDash != null && chargeDash.IsCharging;
        public bool IsChargeDashing => chargeDash != null && chargeDash.IsDashing;
        public bool CanAct => playerHealth == null || playerHealth.CanAct;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            if (animator == null && spriteRoot != null) animator = spriteRoot.GetComponent<Animator>();
            if (unstoppable == null) unstoppable = GetComponentInChildren<UnstoppableAbility>();
            health = GetComponent<Health>();
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.RegisterPlayer(transform, health);

            waveSpawner = FindFirstObjectByType<WaveSpawner>();
            if (waveSpawner != null) waveSpawner.WaveCleared += OnWaveCleared;
        }

        private void OnDestroy()
        {
            if (waveSpawner != null) waveSpawner.WaveCleared -= OnWaveCleared;
        }

        private void OnWaveCleared(int _)
        {
            if (health != null) health.RestoreToFull();
            if (chargeDash != null) chargeDash.ResetCooldown();
            if (unstoppable != null) unstoppable.ResetCharge();
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
            if (dodge != null) dodge.TryActivate();
        }

        public void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAct) return;
            if (IsDodging) return;
            if (IsCharging || IsChargeDashing) return;
            if (combo != null) combo.TryAttack();
        }

        public void OnSecondaryAttack(InputValue value)
        {
            if (chargeDash == null) return;
            if (value.isPressed)
            {
                if (!CanAct) return;
                if (IsDodging) return;
                if (combo != null && combo.IsLocked) return;
                chargeDash.BeginCharge();
            }
            else
            {
                chargeDash.CancelCharge();
            }
        }

        private void Update()
        {
            if (!CanAct && chargeDash != null && chargeDash.IsCharging)
                chargeDash.CancelCharge();
        }

        private void FixedUpdate()
        {
            bool grounded = groundCheck != null && groundCheck.IsGrounded;
            bool comboLocked = combo != null && combo.IsLocked;
            bool dashLocked = chargeDash != null && chargeDash.IsLocked;
            bool acting = !CanAct || IsDodging || dashLocked || comboLocked;

            float vx = rb.linearVelocity.x;
            float vy = rb.linearVelocity.y;

            if (IsChargeDashing)
            {
                // ChargeDashAbility drives velocity in its own FixedUpdate.
            }
            else if (IsDodging)
            {
                // Dodge burst velocity already set by DodgeAbility; preserve horizontal momentum.
            }
            else if (!acting)
            {
                vx = moveInput * moveSpeed;
            }
            else
            {
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

                rb.linearVelocity = new Vector2(vx, vy);
            }

            if (!acting && !IsDodging && Mathf.Abs(moveInput) > 0.05f)
                facing = moveInput > 0f ? 1 : -1;

            ApplyFacing();
            UpdateAnimator(grounded, rb.linearVelocity.x, rb.linearVelocity.y);
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
