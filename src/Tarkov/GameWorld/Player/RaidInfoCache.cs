/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 *
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.UI.Misc;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    /// <summary>
    /// Persistent cache for raid information keyed by RaidId.
    /// RaidId is persistent across reconnects (unlike VoipId).
    /// Uses GamePlayerId (not memory address) as player key for persistence across reconnects.
    /// Stores team data, boss data, and guard data.
    /// Survives radar restarts within the same raid.
    /// </summary>
    public static class RaidInfoCache
    {
        private static readonly string _cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "raid_cache");
        private static readonly string _cacheFilePath = Path.Combine(_cacheDirectory, "current_raid_info.json");
        private static readonly object _lock = new();

        /// <summary>
        /// Raid data structure for serialization.
        /// </summary>
        private class RaidData
        {
            public int RaidId { get; set; }
            public int PlayerId { get; set; }
            public Dictionary<int, int> PlayerGroupIds { get; set; } = new(); // GamePlayerId -> GroupId
            public Dictionary<int, string> BossGuards { get; set; } = new(); // GamePlayerId -> "Boss" or "Guard"
            public DateTime LastUpdated { get; set; }
        }

        static RaidInfoCache()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        /// <summary>
        /// Load team data for the specified RaidId and PlayerId.
        /// Returns null if no cached data exists or RaidId/PlayerId doesn't match.
        /// </summary>
        public static Dictionary<int, int> LoadTeams(int raidId, int playerId)
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_cacheFilePath))
                        return null;

                    string json = File.ReadAllText(_cacheFilePath);
                    var data = JsonSerializer.Deserialize<RaidData>(json);

                    if (data == null || data.RaidId != raidId || data.PlayerId != playerId)
                    {
                        DebugLogger.LogDebug($"[RaidInfoCache] No cached data for RaidId {raidId}, PlayerId {playerId}");
                        return null;
                    }

                    DebugLogger.LogDebug($"[RaidInfoCache] Loaded cached team data for RaidId {raidId}, PlayerId {playerId} ({data.PlayerGroupIds.Count} players)");

                    return new Dictionary<int, int>(data.PlayerGroupIds);
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[RaidInfoCache] Error loading team data: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Load boss/guard data for the specified RaidId and PlayerId.
        /// Returns dictionary mapping GamePlayerId to their role ("Boss" or "Guard").
        /// </summary>
        public static Dictionary<int, string> LoadBossGuards(int raidId, int playerId)
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_cacheFilePath))
                        return null;

                    string json = File.ReadAllText(_cacheFilePath);
                    var data = JsonSerializer.Deserialize<RaidData>(json);

                    if (data == null || data.RaidId != raidId || data.PlayerId != playerId)
                    {
                        return null;
                    }

                    if (data.BossGuards.Count == 0)
                        return null;

                    DebugLogger.LogDebug($"[RaidInfoCache] Loaded cached boss/guard data ({data.BossGuards.Count} entries)");

                    return new Dictionary<int, string>(data.BossGuards);
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[RaidInfoCache] Error loading boss/guard data: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Save team data for the specified RaidId and PlayerId.
        /// </summary>
        public static void SaveTeams(int raidId, int playerId, Dictionary<int, int> playerGroupIds)
        {
            lock (_lock)
            {
                try
                {
                    // Load existing data to preserve boss/guard info
                    RaidData data = LoadExistingData() ?? new RaidData();

                    data.RaidId = raidId;
                    data.PlayerId = playerId;
                    data.LastUpdated = DateTime.Now;

                    // Update team data
                    data.PlayerGroupIds = new Dictionary<int, int>(playerGroupIds);

                    SaveData(data);

                    DebugLogger.LogDebug($"[RaidInfoCache] Saved team data for RaidId {raidId}, PlayerId {playerId} ({playerGroupIds.Count} players)");
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[RaidInfoCache] Error saving team data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Save boss/guard data for the specified RaidId and PlayerId.
        /// </summary>
        public static void SaveBossGuards(int raidId, int playerId, Dictionary<int, string> bossGuards)
        {
            lock (_lock)
            {
                try
                {
                    // Load existing data to preserve team info
                    RaidData data = LoadExistingData() ?? new RaidData();

                    data.RaidId = raidId;
                    data.PlayerId = playerId;
                    data.LastUpdated = DateTime.Now;

                    // Update boss/guard data
                    data.BossGuards = new Dictionary<int, string>(bossGuards);

                    SaveData(data);

                    DebugLogger.LogDebug($"[RaidInfoCache] Saved boss/guard data ({bossGuards.Count} entries)");
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[RaidInfoCache] Error saving boss/guard data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clear the raid cache (call when RaidId/PlayerId changes).
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_cacheFilePath))
                    {
                        File.Delete(_cacheFilePath);
                        DebugLogger.LogDebug("[RaidInfoCache] Cache cleared");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[RaidInfoCache] Error clearing cache: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load existing data from cache file.
        /// </summary>
        private static RaidData LoadExistingData()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return null;

                string json = File.ReadAllText(_cacheFilePath);
                return JsonSerializer.Deserialize<RaidData>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save data to cache file.
        /// </summary>
        private static void SaveData(RaidData data)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(_cacheFilePath, json);
        }
    }
}
