using System;
using System.Collections.Generic;
using HackSlash.Enemies;
using UnityEngine;

namespace HackSlash.Waves
{
    public enum EnemyKind { Melee, Ranged }

    [Serializable]
    public struct WaveEntry
    {
        public EnemyKind kind;
        [Min(1)] public int count;
    }

    [CreateAssetMenu(menuName = "HackSlash/Wave Definition", fileName = "Wave")]
    public class WaveDefinition : ScriptableObject
    {
        [Tooltip("Composition for this wave — each entry spawns `count` enemies of `kind`.")]
        public List<WaveEntry> entries = new();

        [Tooltip("Seconds between individual spawns inside this wave.")]
        [Min(0f)] public float spawnInterval = 0.4f;

        [Tooltip("Seconds to wait after the previous wave is cleared before this one starts.")]
        [Min(0f)] public float startDelay = 1.5f;

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < entries.Count; i++) total += Mathf.Max(0, entries[i].count);
                return total;
            }
        }
    }
}
