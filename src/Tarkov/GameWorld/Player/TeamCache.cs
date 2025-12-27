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
OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.UI.Misc;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    /// <summary>
    /// Persistent cache for team information keyed by RaidId.
    /// Survives radar restarts within the same raid.
    /// </summary>
    public static class TeamCache
    {
        private static readonly string _cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "team_cache");
        private static readonly string _cacheFilePath = Path.Combine(_cacheDirectory, "current_raid_teams.json");
        private static readonly object _lock = new();

        /// <summary>
        /// Team data structure for serialization.
        /// </summary>
        private class TeamData
        {
            public int RaidId { get; set; }
            public string LocalPlayerBase { get; set; } = string.Empty;
            public Dictionary<string, int> PlayerGroupIds { get; set; } = new();
            public DateTime LastUpdated { get; set; }
        }

        static TeamCache()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        /// <summary>
        /// Load team data for the specified RaidId and LocalPlayer Base.
        /// Returns null if no cached data exists or RaidId/LocalPlayerBase doesn't match.
        /// </summary>
        public static Dictionary<ulong, int> Load(int raidId, ulong localPlayerBase)
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_cacheFilePath))
                        return null;

                    string json = File.ReadAllText(_cacheFilePath);
                    var data = JsonSerializer.Deserialize<TeamData>(json);

                    if (data == null || data.RaidId != raidId)
                    {
                        DebugLogger.LogDebug($"[TeamCache] No cached data for RaidId {raidId}");
                        return null;
                    }

                    string currentBaseStr = localPlayerBase.ToString("X");
                    if (data.LocalPlayerBase != currentBaseStr)
                    {
                        DebugLogger.LogDebug($"[TeamCache] LocalPlayer Base mismatch - cached: {data.LocalPlayerBase}, current: {currentBaseStr}");
                        return null;
                    }

                    DebugLogger.LogDebug($"[TeamCache] Loaded cached team data for RaidId {raidId} ({data.PlayerGroupIds.Count} players)");

                    var result = new Dictionary<ulong, int>();
                    foreach (var kvp in data.PlayerGroupIds)
                    {
                        if (ulong.TryParse(kvp.Key, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
                        {
                            result[address] = kvp.Value;
                        }
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[TeamCache] Error loading team data: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Save team data for the specified RaidId and LocalPlayer Base.
        /// </summary>
        public static void Save(int raidId, ulong localPlayerBase, Dictionary<ulong, int> playerGroupIds)
        {
            lock (_lock)
            {
                try
                {
                    var data = new TeamData
                    {
                        RaidId = raidId,
                        LocalPlayerBase = localPlayerBase.ToString("X"),
                        LastUpdated = DateTime.Now
                    };

                    foreach (var kvp in playerGroupIds)
                    {
                        data.PlayerGroupIds[kvp.Key.ToString("X")] = kvp.Value;
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(data, options);
                    File.WriteAllText(_cacheFilePath, json);

                    DebugLogger.LogDebug($"[TeamCache] Saved team data for RaidId {raidId} ({playerGroupIds.Count} players)");
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[TeamCache] Error saving team data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clear the team cache (call when RaidId changes).
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
                        DebugLogger.LogDebug("[TeamCache] Cache cleared");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogDebug($"[TeamCache] Error clearing cache: {ex.Message}");
                }
            }
        }
    }
}
