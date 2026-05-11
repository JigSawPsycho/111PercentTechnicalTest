using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combo")]
        [SerializeField] private float[] swingDurations = { 0.35f, 0.45f };
        [SerializeField] private float[] strikeTimings = { 0.15f, 0.18f };
        [SerializeField] private float comboWindow = 0.45f;

        [Header("Refs")]
        [SerializeField] private Animator animator;
        [SerializeField] private Hitbox hitbox;

        [Header("Animator Params")]
        [SerializeField] private string attack1Trigger = "Attack1";
        [SerializeField] private string attack2Trigger = "Attack2";

        private int comboStep;
        private float swingEndsAt;
        private float strikeAt;
        private bool strikeFired;
        private float comboWindowEndsAt;
        private bool bufferedNext;

        public bool IsLocked => Time.time < swingEndsAt;
        public bool InComboWindow => comboStep > 0 && Time.time < comboWindowEndsAt;

        public void TryAttack()
        {
            if (IsLocked)
            {
                if (comboStep == 1) bufferedNext = true;
                return;
            }

            if (InComboWindow && comboStep == 1)
                StartSwing(2);
            else
                StartSwing(1);
        }

        private void StartSwing(int step)
        {
            comboStep = step;
            int idx = Mathf.Clamp(step - 1, 0, swingDurations.Length - 1);
            float duration = swingDurations[idx];
            float strikeOffset = idx < strikeTimings.Length ? strikeTimings[idx] : duration * 0.4f;
            swingEndsAt = Time.time + duration;
            strikeAt = Time.time + strikeOffset;
            strikeFired = false;
            comboWindowEndsAt = swingEndsAt + comboWindow;
            bufferedNext = false;

            if (animator != null)
                animator.SetTrigger(step == 1 ? attack1Trigger : attack2Trigger);

            if (hitbox != null) hitbox.BeginSwing();
        }

        private void Update()
        {
            if (!strikeFired && comboStep > 0 && Time.time >= strikeAt && Time.time < swingEndsAt)
            {
                strikeFired = true;
                if (hitbox != null) hitbox.Strike();
            }

            if (!IsLocked && comboStep > 0 && Time.time > comboWindowEndsAt)
                comboStep = 0;

            if (!IsLocked && bufferedNext && comboStep == 1)
            {
                bufferedNext = false;
                StartSwing(2);
            }
        }
    }
}
