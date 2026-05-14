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
        // Remember the kind each live enemy was spawned as so we can attribute the
        // kill to the correct icon column when it dies — the HUD's enemy tracker
        // needs per-type kill counts, not just a total.
        private readonly Dictionary<EnemyBase, EnemyKind> kindByEnemy = new();
        private int waveIndex = -1;
        private int killedThisWave;
        private int meleeKilledThisWave;
        private int rangedKilledThisWave;
        private bool spawning;

        public int WaveIndex => waveIndex;
        public int WaveCount => waves.Count;
        public int AliveEnemies => live.Count;
        public int EnemiesKilledThisWave => killedThisWave;
        public int MeleeKilledThisWave => meleeKilledThisWave;
        public int RangedKilledThisWave => rangedKilledThisWave;
        public WaveDefinition CurrentWave =>
            (waveIndex >= 0 && waveIndex < waves.Count) ? waves[waveIndex] : null;
        public int CurrentWaveTotal => CurrentWave != null ? CurrentWave.TotalEnemies : 0;
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

                killedThisWave = 0;
                meleeKilledThisWave = 0;
                rangedKilledThisWave = 0;
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
                    bool moreInEntry = i < entry.count - 1;
                    if (moreInEntry && entry.spawnInterval > 0f)
                        yield return new WaitForSeconds(entry.spawnInterval);
                }

                bool moreEntries = e < wave.entries.Count - 1;
                if (moreEntries && wave.entryInterval > 0f)
                    yield return new WaitForSeconds(wave.entryInterval);
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
            kindByEnemy[enemy] = kind;
            enemy.Defeated += OnEnemyDefeated;
        }

        private void OnEnemyDefeated(EnemyBase enemy)
        {
            enemy.Defeated -= OnEnemyDefeated;
            live.Remove(enemy);
            killedThisWave++;
            if (kindByEnemy.TryGetValue(enemy, out var kind))
            {
                if (kind == EnemyKind.Melee) meleeKilledThisWave++;
                else rangedKilledThisWave++;
                kindByEnemy.Remove(enemy);
            }
        }
    }
}
