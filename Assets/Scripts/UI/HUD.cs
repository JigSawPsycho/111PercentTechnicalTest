using HackSlash.Core;
using HackSlash.Waves;
using UnityEngine;
using UnityEngine.UI;

namespace HackSlash.UI
{
    public class HUD : MonoBehaviour
    {
        [SerializeField] private Slider healthBar;
        [SerializeField] private Text waveLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private WaveSpawner spawner;

        private Health playerHealth;

        private void Start()
        {
            if (GameManager.Instance != null)
                playerHealth = GameManager.Instance.PlayerHealth;
        }

        private void Update()
        {
            if (playerHealth == null && GameManager.Instance != null)
                playerHealth = GameManager.Instance.PlayerHealth;

            if (healthBar != null && playerHealth != null)
                healthBar.value = playerHealth.Normalized;

            if (waveLabel != null && spawner != null)
            {
                int idx = Mathf.Clamp(spawner.WaveIndex + 1, 1, Mathf.Max(1, spawner.WaveCount));
                waveLabel.text = $"Wave {idx} / {spawner.WaveCount}  ·  Enemies: {spawner.AliveEnemies}";
            }

            if (statusLabel != null)
            {
                if (playerHealth != null && !playerHealth.IsAlive) statusLabel.text = "DEFEATED — restarting…";
                else if (spawner != null && spawner.AllWavesDone) statusLabel.text = "VICTORY — restarting…";
                else statusLabel.text = string.Empty;
            }
        }
    }
}
