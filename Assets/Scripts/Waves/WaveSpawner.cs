using System;
using System.Collections;
using System.Collections.Generic;
using HackSlash.Core;
using HackSlash.Enemies;
using UnityEngine;

namespace HackSlash.Waves
{
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Waves")]
        [SerializeField] private List<WaveDefinition> waves = new();
        [SerializeField] private bool autoStart = true;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Enemy Prefabs")]
        [SerializeField] private EnemyBase meleePrefab;
        [SerializeField] private EnemyBase rangedPrefab;

        private readonly HashSet<EnemyBase> live = new();
        private int waveIndex = -1;
        private bool spawning;

        public int WaveIndex => waveIndex;
        public int WaveCount => waves.Count;
        public int AliveEnemies => live.Count;
        public bool AllWavesDone => waveIndex >= waves.Count;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action AllWavesCleared;

        private void Start()
        {
            if (autoStart) StartWaves();
        }

        public void StartWaves()
        {
            if (waves.Count == 0) return;
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            for (waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                var wave = waves[waveIndex];
                if (wave == null) continue;

                if (wave.startDelay > 0f)
                    yield return new WaitForSeconds(wave.startDelay);

                WaveStarted?.Invoke(waveIndex);
                yield return SpawnWave(wave);

                // Wait until all enemies in this wave die.
                while (live.Count > 0)
                    yield return null;

                WaveCleared?.Invoke(waveIndex);
            }

            AllWavesCleared?.Invoke();
            if (GameManager.Instance != null) GameManager.Instance.NotifyAllWavesCleared();
        }

        private IEnumerator SpawnWave(WaveDefinition wave)
        {
            spawning = true;
            for (int e = 0; e < wave.entries.Count; e++)
            {
                var entry = wave.entries[e];
                for (int i = 0; i < entry.count; i++)
                {
                    SpawnOne(entry.kind);
                    if (wave.spawnInterval > 0f)
                        yield return new WaitForSeconds(wave.spawnInterval);
                }
            }
            spawning = false;
        }

        private void SpawnOne(EnemyKind kind)
        {
            EnemyBase prefab = kind == EnemyKind.Melee ? meleePrefab : rangedPrefab;
            if (prefab == null) return;
            if (spawnPoints == null || spawnPoints.Length == 0) return;

            Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            var enemy = Instantiate(prefab, point.position, Quaternion.identity);
            live.Add(enemy);
            enemy.Defeated += OnEnemyDefeated;
        }

        private void OnEnemyDefeated(EnemyBase enemy)
        {
            enemy.Defeated -= OnEnemyDefeated;
            live.Remove(enemy);
        }
    }
}
