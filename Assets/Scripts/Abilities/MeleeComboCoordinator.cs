using System;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Router (not an Ability) that chains two MeleeSwingAbility instances into a
    /// 2-hit combo. The second swing is gated by WhipUnlocked, which is opened only
    /// from this coordinator's TryAttack flow. References the abstract base so any
    /// two melee swings can be composed.
    /// </summary>
    public class MeleeComboCoordinator : MonoBehaviour
    {
        [SerializeField] private MeleeSwingAbility first;
        [SerializeField] private MeleeSwingAbility second;
        [SerializeField] private float comboWindow = 0.45f;

        private int comboStep;
        private float comboWindowEndsAt;
        private bool bufferedNext;
        private bool whipUnlocked;

        public event Action<int> ComboHit;

        public bool WhipUnlocked => whipUnlocked;
        public bool IsLocked => (first != null && first.IsActive) || (second != null && second.IsActive);
        public bool InComboWindow => comboStep == 1 && Time.time < comboWindowEndsAt;

        private void Awake()
        {
            if (first != null) first.SwingHit += OnFirstHit;
            if (second != null) second.SwingHit += OnSecondHit;
        }

        private void OnDestroy()
        {
            if (first != null) first.SwingHit -= OnFirstHit;
            if (second != null) second.SwingHit -= OnSecondHit;
        }

        private void OnFirstHit(int hits) => ComboHit?.Invoke(1);
        private void OnSecondHit(int hits) => ComboHit?.Invoke(2);

        public void TryAttack()
        {
            if (first == null) return;

            if (first.IsActive)
            {
                bufferedNext = true;
                return;
            }
            if (second != null && second.IsActive) return;

            if (InComboWindow && second != null)
            {
                if (TryFireSecond()) return;
            }

            if (first.TryActivate())
            {
                comboStep = 1;
                comboWindowEndsAt = first.ActiveUntil + comboWindow;
                bufferedNext = false;
            }
        }

        private bool TryFireSecond()
        {
            whipUnlocked = true;
            bool fired = second.TryActivate();
            whipUnlocked = false;
            if (fired)
            {
                comboStep = 2;
                bufferedNext = false;
            }
            return fired;
        }

        private void Update()
        {
            if (bufferedNext && first != null && !first.IsActive && comboStep == 1)
            {
                TryFireSecond();
            }

            if (comboStep > 0 && (first == null || !first.IsActive) && (second == null || !second.IsActive) && Time.time > comboWindowEndsAt)
            {
                comboStep = 0;
            }
        }
    }
}
