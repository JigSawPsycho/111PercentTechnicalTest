using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Abstract refinement for the windup → strike → recovery pattern shared by
    /// melee swings and ranged shots. Subclasses implement OnStrike (and optionally
    /// OnSwingStart) for the actual damage delivery.
    /// </summary>
    public abstract class TimedStrikeAbility : Ability
    {
        [SerializeField] protected float windup = 0.15f;
        [SerializeField] protected float recovery = 0.2f;
        [SerializeField] protected Animator animator;
        [SerializeField] protected string animatorTrigger;

        private float strikeAt;
        private bool strikeFired;

        protected override void Awake()
        {
            base.Awake();
            if (animator == null) animator = GetComponentInParent<Animator>();
        }

        protected override void OnActivate()
        {
            float duration = windup + recovery;
            SetActive(duration);
            strikeAt = Time.time + windup;
            strikeFired = false;
            StartCooldown();

            if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
                animator.SetTrigger(animatorTrigger);

            OnSwingStart();
        }

        protected virtual void Update()
        {
            if (!strikeFired && IsActive && Time.time >= strikeAt)
            {
                strikeFired = true;
                OnStrike();
            }
        }

        protected virtual void OnSwingStart() { }
        protected abstract void OnStrike();

        public override void Cancel()
        {
            strikeFired = true;
            base.Cancel();
        }
    }
}
