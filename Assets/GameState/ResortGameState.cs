using System;
using System.Collections.Generic;
using System.IO;
using ParadiseTycoon.Buildings;
using UnityEngine;

namespace ParadiseTycoon.GameState
{
    [Serializable]
    public sealed class ResortEconomySettings
    {
        [Min(0)] public int startingEnergy = 80;
        [Min(1)] public int maxEnergy = 100;
        [Min(0)] public int startingGems;
        [Min(0)] public int baseValuePerBuilding = 1000;
    }

    /// <summary>Scene-owned game state. Call its operations on the Unity main thread.</summary>
    public sealed class ResortGameState : IBuildingUpgradeState
    {
        public const string SaveFileName = "resort-game-state.json";
        const string LegacyFileName = "building-upgrades-v1.json";
        const int CurrentVersion = 2;

        [Serializable]
        struct SavedBuilding
        {
            public string id;
            public int level;
        }

        [Serializable]
        sealed class Snapshot
        {
            public int version, coins = -1, energy = -1, maxEnergy = -1, gems = -1;
            public SavedBuilding[] buildings;
            public string[] completedWaveIds;

            // Each operation replaces the arrays it changes before publishing the copy.
            public Snapshot Copy() => (Snapshot)MemberwiseClone();
        }

        readonly string path;
        readonly Dictionary<string, ResortBuilding> catalog = new(StringComparer.Ordinal);
        readonly Snapshot initial;
        readonly int baseBuildingValue;
        Snapshot current;
        bool unreadableSave;
        bool externalWaveActive;

        public event Action Changed;
        public int Coins => current.coins;
        public int Energy => current.energy;
        public int MaxEnergy => current.maxEnergy;
        public int Gems => current.gems;
        public int CompletedWaves => current.completedWaveIds.Length;
        public string ActiveWaveId { get; private set; }
        public bool IsWaveActive => ActiveWaveId != null || externalWaveActive;
        public string SaveError { get; private set; }

        public long ResortValue
        {
            get
            {
                long value = 0;
                foreach (var entry in current.buildings)
                {
                    if (entry.level == 0) continue;
                    value += baseBuildingValue;
                    var building = catalog[entry.id];
                    for (int level = 2; level <= entry.level; level++)
                        value += building.GetLevel(level).Cost;
                }
                return value;
            }
        }

        public ResortGameState(string directory, ResortBuilding[] buildings, int startingCoins,
            ResortEconomySettings settings)
        {
            path = Path.Combine(directory, SaveFileName);
            baseBuildingValue = Math.Max(0, settings.baseValuePerBuilding);
            int maximumEnergy = Math.Max(1, settings.maxEnergy);
            initial = new Snapshot
            {
                version = CurrentVersion,
                coins = Math.Max(0, startingCoins),
                energy = Mathf.Clamp(settings.startingEnergy, 0, maximumEnergy),
                maxEnergy = maximumEnergy,
                gems = Math.Max(0, settings.startingGems),
                buildings = Array.ConvertAll(buildings, building =>
                {
                    catalog.Add(building.BuildingId, building);
                    return new SavedBuilding { id = building.BuildingId, level = building.InitialLevel };
                }),
                completedWaveIds = Array.Empty<string>()
            };
            current = initial;

            try
            {
                var legacyPath = Path.Combine(directory, LegacyFileName);
                if (!File.Exists(path) && !File.Exists(legacyPath))
                {
                    TrySaveBetweenWaves();
                    return;
                }
                bool legacy = !File.Exists(path);
                var saved = new Snapshot();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(legacy ? legacyPath : path), saved);
                if (legacy && saved.version == 1)
                {
                    saved.version = CurrentVersion;
                    saved.energy = initial.energy;
                    saved.maxEnergy = initial.maxEnergy;
                    saved.gems = initial.gems;
                    saved.completedWaveIds = Array.Empty<string>();
                }
                Validate(saved);
                current = saved;
                if (legacy) TrySaveBetweenWaves();
            }
            catch (Exception error) when (IsStorageError(error) || error is ArgumentException || error is InvalidDataException)
            {
                unreadableSave = true;
                ReportError(error);
            }
        }

        void Validate(Snapshot saved)
        {
            if (saved.version != CurrentVersion || saved.coins < 0 || saved.gems < 0 ||
                saved.energy < 0 || saved.maxEnergy < 1 || saved.energy > saved.maxEnergy ||
                saved.buildings == null || saved.buildings.Length != catalog.Count || saved.completedWaveIds == null)
                throw new InvalidDataException("Invalid or unsupported resort save.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in saved.buildings)
                if (string.IsNullOrWhiteSpace(entry.id) || !catalog.TryGetValue(entry.id, out var building) ||
                    !ids.Add(entry.id) || entry.level < building.InitialLevel || entry.level > building.MaxLevel)
                    throw new InvalidDataException("Invalid saved building: " + entry.id);
            ids.Clear();
            foreach (string id in saved.completedWaveIds)
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    throw new InvalidDataException("Invalid completed wave ID.");
        }

        public int GetBuildingLevel(string buildingId, int initialLevel)
        {
            int index = Array.FindIndex(current.buildings, entry => entry.id == buildingId);
            return index >= 0 ? current.buildings[index].level : initialLevel;
        }

        public bool TryPurchaseUpgrade(string buildingId, int expectedLevel, int cost)
        {
            if (IsWaveActive || string.IsNullOrEmpty(buildingId) || !catalog.TryGetValue(buildingId, out var building) ||
                expectedLevel < 1 || expectedLevel >= building.MaxLevel || cost <= 0 || Coins < cost ||
                building.GetLevel(expectedLevel + 1).Cost != cost) return false;

            int index = Array.FindIndex(current.buildings, entry => entry.id == buildingId);
            if (index < 0 || current.buildings[index].level != expectedLevel) return false;
            var next = current.Copy();
            next.coins -= cost;
            next.buildings = (SavedBuilding[])current.buildings.Clone();
            next.buildings[index].level++;
            return Commit(next);
        }

        public bool HasCompletedWave(string waveId) => Array.IndexOf(current.completedWaveIds, waveId) >= 0;

        public bool TryBeginWave(string waveId)
        {
            if (unreadableSave || ActiveWaveId != null || string.IsNullOrWhiteSpace(waveId) || HasCompletedWave(waveId))
                return false;
            // At docking, the guest wave takes over the ship's arrival lock until settlement.
            ActiveWaveId = waveId;
            externalWaveActive = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Persists the payout and its stable wave ID in one checkpoint. A failed write can be retried.</summary>
        public bool TryCompleteWave(string waveId, int rewardCoins)
        {
            if (ActiveWaveId == null || waveId != ActiveWaveId || rewardCoins < 0 || rewardCoins > int.MaxValue - Coins)
                return false;
            var next = current.Copy();
            next.coins += rewardCoins;
            Array.Resize(ref next.completedWaveIds, CompletedWaves + 1);
            next.completedWaveIds[CompletedWaves] = waveId;
            return Commit(next, finishWave: true);
        }

        // Compatibility for a wave owner that only toggles the original upgrade lock.
        internal void SetExternalWaveActive(bool active)
        {
            if (externalWaveActive == active) return;
            externalWaveActive = active;
            Changed?.Invoke();
        }

        public bool TryNewGame() => !IsWaveActive && Commit(initial);
        public bool TrySaveBetweenWaves() => !IsWaveActive && Commit(current, notify: false);

        bool Commit(Snapshot next, bool finishWave = false, bool notify = true)
        {
            if (unreadableSave) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string pending = path + ".tmp";
                File.WriteAllText(pending, JsonUtility.ToJson(next));
                if (File.Exists(path)) File.Replace(pending, path, null);
                else File.Move(pending, path);
            }
            catch (Exception error) when (IsStorageError(error))
            {
                ReportError(error);
                return false;
            }
            current = next;
            if (finishWave) ActiveWaveId = null;
            SaveError = null;
            if (notify) Changed?.Invoke();
            return true;
        }

        static bool IsStorageError(Exception error) => error is IOException || error is UnauthorizedAccessException;

        void ReportError(Exception error)
        {
            if (SaveError != error.Message) Debug.LogWarning("Could not read or write resort progress: " + error.Message);
            SaveError = error.Message;
        }
    }
}
