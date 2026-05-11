using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField] protected float moveSpeed = 2.5f;
        [SerializeField] protected Transform spriteRoot;
        [SerializeField] protected Animator animator;
        [SerializeField] protected GroundCheck groundCheck;
        [SerializeField] protected float hitstunDuration = 0.25f;
        [SerializeField] protected float corpseLifetime = 1.5f;

        [Header("Animator Params")]
        [SerializeField] protected string speedParam = "Speed";
        [SerializeField] protected string groundedParam = "Grounded";
        [SerializeField] protected string hurtTrigger = "Hurt";
        [SerializeField] protected string deadBool = "Dead";

        protected Rigidbody2D rb;
        protected Health health;
        protected Transform target;
        protected int facing = -1;
        protected float hitstunUntil;
        protected bool isDead;

        public bool IsDead => isDead;
        public Health Health => health;

        public event System.Action<EnemyBase> Defeated;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            health = GetComponent<Health>();
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            if (animator == null && spriteRoot != null) animator = spriteRoot.GetComponent<Animator>();
        }

        protected virtual void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }

        protected virtual void Start()
        {
            AcquireTarget();
        }

        protected void AcquireTarget()
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
                target = GameManager.Instance.Player;
        }

        protected bool InHitstun => Time.time < hitstunUntil;

        protected virtual void HandleDamaged(DamageInfo info)
        {
            hitstunUntil = Time.time + hitstunDuration;
            CancelAttack();
            if (animator != null) animator.SetTrigger(hurtTrigger);
            if (info.Source == Faction.Player && HitFeel.Instance != null) HitFeel.Instance.Pulse();
            Vector2 v = rb.linearVelocity;
            v.x = info.Knockback.x;
            rb.linearVelocity = v;
        }

        /// <summary>
        /// Drop any pending strike/shot so a hit mid-windup interrupts the attack.
        /// Subclasses override to clear their attack-specific timers.
        /// </summary>
        protected virtual void CancelAttack() { }

        protected virtual void HandleDied()
        {
            isDead = true;
            if (animator != null) animator.SetBool(deadBool, true);
            rb.linearVelocity = Vector2.zero;
            foreach (var col in GetComponentsInChildren<Collider2D>())
                col.enabled = false;
            Defeated?.Invoke(this);
            Destroy(gameObject, corpseLifetime);
        }

        protected void FaceTowards(float xDir)
        {
            if (Mathf.Abs(xDir) < 0.01f) return;
            facing = xDir > 0f ? 1 : -1;
            if (spriteRoot != null)
            {
                Vector3 s = spriteRoot.localScale;
                s.x = Mathf.Abs(s.x) * facing;
                spriteRoot.localScale = s;
            }
        }

        protected void UpdateLocomotionAnimator(float horizontalSpeed)
        {
            if (animator == null) return;
            animator.SetFloat(speedParam, Mathf.Abs(horizontalSpeed));
            if (groundCheck != null) animator.SetBool(groundedParam, groundCheck.IsGrounded);
        }
    }
}
