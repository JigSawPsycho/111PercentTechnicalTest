using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HackSlash.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private float restartDelay = 2f;

        public event Action PlayerDied;
        public event Action GameWon;

        public Transform Player { get; private set; }
        public Health PlayerHealth { get; private set; }

        private float restartAt;
        private bool restartScheduled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterPlayer(Transform player, Health health)
        {
            Player = player;
            PlayerHealth = health;
            if (health != null)
                health.Died += HandlePlayerDied;
        }

        public void NotifyAllWavesCleared()
        {
            GameWon?.Invoke();
            ScheduleRestart();
        }

        private void HandlePlayerDied()
        {
            PlayerDied?.Invoke();
            ScheduleRestart();
        }

        private void ScheduleRestart()
        {
            if (restartScheduled) return;
            restartScheduled = true;
            restartAt = Time.time + restartDelay;
        }

        private void Update()
        {
            if (restartScheduled && Time.time >= restartAt)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
