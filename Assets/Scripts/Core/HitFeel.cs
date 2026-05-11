using UnityEngine;

namespace HackSlash.Core
{
    /// <summary>
    /// Brief time freeze + camera shake on impact. Singleton; place on the Main Camera.
    /// </summary>
    public class HitFeel : MonoBehaviour
    {
        public static HitFeel Instance { get; private set; }

        [Header("Hitstop")]
        [SerializeField] private float defaultHitstopDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] private float defaultHitstopScale = 0f;

        [Header("Shake")]
        [SerializeField] private float defaultShakeDuration = 0.22f;
        [SerializeField] private float defaultShakeMagnitude = 0.35f;
        [SerializeField] private Transform shakeTarget;

        private Vector3 shakeBasePosition;
        private float shakeStartTime;
        private float shakeEndTime;
        private float currentShakeMagnitude;
        private bool shaking;

        private float hitstopEndTime;
        private float cachedTimeScale = 1f;
        private bool hitstopActive;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (shakeTarget == null) shakeTarget = transform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (hitstopActive) Time.timeScale = cachedTimeScale;
        }

        public void Pulse() => Pulse(defaultHitstopDuration, defaultHitstopScale, defaultShakeDuration, defaultShakeMagnitude);

        public void Pulse(float hitstopDuration, float hitstopScale, float shakeDuration, float shakeMagnitude)
        {
            if (hitstopDuration > 0f) BeginHitstop(hitstopDuration, hitstopScale);
            if (shakeDuration > 0f && shakeMagnitude > 0f) BeginShake(shakeDuration, shakeMagnitude);
        }

        public void BeginHitstop(float duration, float scale)
        {
            if (!hitstopActive)
            {
                cachedTimeScale = Time.timeScale;
                hitstopActive = true;
            }
            Time.timeScale = Mathf.Clamp01(scale);
            hitstopEndTime = Mathf.Max(hitstopEndTime, Time.unscaledTime + duration);
        }

        public void BeginShake(float duration, float magnitude)
        {
            if (shakeTarget == null) return;
            if (!shaking)
            {
                shakeBasePosition = shakeTarget.localPosition;
                shaking = true;
            }
            shakeStartTime = Time.unscaledTime;
            shakeEndTime = Mathf.Max(shakeEndTime, shakeStartTime + duration);
            currentShakeMagnitude = Mathf.Max(currentShakeMagnitude, magnitude);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;

            if (hitstopActive && now >= hitstopEndTime)
            {
                Time.timeScale = cachedTimeScale;
                hitstopActive = false;
            }

            if (!shaking) return;

            if (now < shakeEndTime)
            {
                float span = Mathf.Max(0.0001f, shakeEndTime - shakeStartTime);
                float falloff = Mathf.Clamp01((shakeEndTime - now) / span);
                falloff *= falloff;
                Vector2 random = Random.insideUnitCircle;
                Vector3 offset = new Vector3(random.x, random.y, 0f) * currentShakeMagnitude * falloff;
                shakeTarget.localPosition = shakeBasePosition + offset;
            }
            else
            {
                shakeTarget.localPosition = shakeBasePosition;
                shaking = false;
                currentShakeMagnitude = 0f;
            }
        }
    }
}
