using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Abstract base for any character-agnostic skill/attack. Owns the cooldown +
    /// active-duration lifecycle; subclasses implement the actual behavior in OnActivate.
    /// Concrete abilities are MonoBehaviours dropped onto the character's GameObject;
    /// they discover their host via GetComponentInParent&lt;IAbilityOwner&gt;().
    /// </summary>
    public abstract class Ability : MonoBehaviour
    {
        [SerializeField] protected float cooldown;

        protected IAbilityOwner owner;
        protected float readyAt;
        protected float activeUntil;

        public bool IsReady => Time.time >= readyAt;
        public bool IsActive => Time.time < activeUntil;
        public virtual bool IsLocked => IsActive;
        public float ActiveUntil => activeUntil;

        public float CooldownProgress
        {
            get
            {
                if (cooldown <= 0f) return 1f;
                float remaining = readyAt - Time.time;
                if (remaining <= 0f) return 1f;
                return 1f - Mathf.Clamp01(remaining / cooldown);
            }
        }

        protected virtual void Awake()
        {
            if (owner == null) owner = GetComponentInParent<IAbilityOwner>();
        }

        public bool CanActivate()
        {
            if (!IsReady) return false;
            if (IsActive) return false;
            return CanActivateInternal();
        }

        public bool TryActivate()
        {
            if (!CanActivate()) return false;
            OnActivate();
            return true;
        }

        protected virtual bool CanActivateInternal() => true;

        protected abstract void OnActivate();

        public virtual void Cancel()
        {
            activeUntil = 0f;
        }

        protected void StartCooldown()
        {
            readyAt = Time.time + cooldown;
        }

        public void ResetCooldown()
        {
            readyAt = 0f;
        }

        protected void SetActive(float duration)
        {
            activeUntil = Time.time + duration;
        }
    }
}
