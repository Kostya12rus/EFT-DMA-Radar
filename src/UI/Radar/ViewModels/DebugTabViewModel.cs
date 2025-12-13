using LoneEftDmaRadar;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.UI.Misc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class DebugTabViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer;
        private string _DeviceAimbotDebugText = "DeviceAimbot Aimbot: (no data)";
        private bool _showDeviceAimbotDebug = App.Config.Device.ShowDebug;

        private bool _showAIDebugOverlay = App.Config.UI.EspAIDebug;
        private string _aiDebugText = "AI Debug Overlay: (no data)";
        private bool _showLootDebugOverlay = App.Config.UI.EspLootDebug;
        private string _lootDebugText = "Loot Debug Overlay: (no data)";

        public DebugTabViewModel()
        {
            ToggleDebugConsoleCommand = new SimpleCommand(DebugLogger.Toggle);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (_, _) =>
            {
                RefreshDeviceAimbotDebug();
                RefreshAiDebug();
            };
            _timer.Start();
            RefreshDeviceAimbotDebug();
            RefreshAiDebug();
        }

        public ICommand ToggleDebugConsoleCommand { get; }

        public bool ShowDeviceAimbotDebug
        {
            get => _showDeviceAimbotDebug;
            set
            {
                if (_showDeviceAimbotDebug == value)
                    return;
                _showDeviceAimbotDebug = value;
                App.Config.Device.ShowDebug = value;
                OnPropertyChanged(nameof(ShowDeviceAimbotDebug));
            }
        }

        public bool ShowAIDebugOverlay
        {
            get => _showAIDebugOverlay;
            set
            {
                if (_showAIDebugOverlay == value)
                    return;

                _showAIDebugOverlay = value;
                App.Config.UI.EspAIDebug = value;
                OnPropertyChanged(nameof(ShowAIDebugOverlay));
            }
        }

        public bool ShowLootDebugOverlay
        {
            get => _showLootDebugOverlay;
            set
            {
                if (_showLootDebugOverlay == value)
                    return;

                _showLootDebugOverlay = value;
                App.Config.UI.EspLootDebug = value;
                OnPropertyChanged(nameof(ShowLootDebugOverlay));
            }
        }

        public string DeviceAimbotDebugText
        {
            get => _DeviceAimbotDebugText;
            private set
            {
                if (_DeviceAimbotDebugText != value)
                {
                    _DeviceAimbotDebugText = value;
                    OnPropertyChanged(nameof(DeviceAimbotDebugText));
                }
            }
        }

        public string AiDebugText
        {
            get => _aiDebugText;
            private set
            {
                if (_aiDebugText != value)
                {
                    _aiDebugText = value;
                    OnPropertyChanged(nameof(AiDebugText));
                }
            }
        }

        public string LootDebugText
        {
            get => _lootDebugText;
            private set
            {
                if (_lootDebugText != value)
                {
                    _lootDebugText = value;
                    OnPropertyChanged(nameof(LootDebugText));
                }
            }
        }

        private void RefreshDeviceAimbotDebug()
        {
            var snapshot = MemDMA.DeviceAimbot?.GetDebugSnapshot();
            if (snapshot == null)
            {
                DeviceAimbotDebugText = "DeviceAimbot Aimbot: not running or no data yet.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== DeviceAimbot Aimbot ===");
            sb.AppendLine($"Status: {snapshot.Status}");
            sb.AppendLine($"Key: {(snapshot.KeyEngaged ? "ENGAGED" : "Idle")} | Enabled: {snapshot.Enabled} | Device: {(snapshot.DeviceConnected ? "Connected" : "Disconnected")}");
            sb.AppendLine($"InRaid: {snapshot.InRaid} | FOV: {snapshot.ConfigFov:F0}px | MaxDist: {snapshot.ConfigMaxDistance:F0}m | Mode: {snapshot.TargetingMode}");
            sb.AppendLine($"Filters -> PMC:{App.Config.Device.TargetPMC} PScav:{App.Config.Device.TargetPlayerScav} AI:{App.Config.Device.TargetAIScav} Boss:{App.Config.Device.TargetBoss} Raider:{App.Config.Device.TargetRaider}");
            sb.AppendLine($"Candidates: total {snapshot.CandidateTotal}, type {snapshot.CandidateTypeOk}, dist {snapshot.CandidateInDistance}, skeleton {snapshot.CandidateWithSkeleton}, w2s {snapshot.CandidateW2S}, final {snapshot.CandidateCount}");
            sb.AppendLine($"Target: {(snapshot.LockedTargetName ?? "None")} [{snapshot.LockedTargetType?.ToString() ?? "-"}] valid={snapshot.TargetValid}");
            if (snapshot.LockedTargetDistance.HasValue)
                sb.AppendLine($"  Dist {snapshot.LockedTargetDistance.Value:F1}m | FOVDist {(float.IsNaN(snapshot.LockedTargetFov) ? "n/a" : snapshot.LockedTargetFov.ToString("F1"))} | Bone {snapshot.TargetBone}");
            sb.AppendLine($"Fireport: {(snapshot.HasFireport ? snapshot.FireportPosition?.ToString() : "None")}");
            var bulletSpeedText = snapshot.BulletSpeed.HasValue ? snapshot.BulletSpeed.Value.ToString("F1") : "?";
            sb.AppendLine($"Ballistics: {(snapshot.BallisticsValid ? $"OK (Speed {bulletSpeedText} m/s, Predict {(snapshot.PredictionEnabled ? "ON" : "OFF")})" : "Invalid/None")}");

            DeviceAimbotDebugText = sb.ToString();
        }

        private void RefreshAiDebug()
        {
            var localPlayer = Memory.LocalPlayer;
            var players = Memory.Players;

            if (localPlayer is null || players is null || players.Count == 0)
            {
                AiDebugText = "AI Debug Overlay: waiting for raid or players.";
                return;
            }

            var aiPlayers = players.Where(p => p?.IsAI == true).ToList();

            if (aiPlayers.Count == 0)
            {
                AiDebugText = "AI Debug Overlay: no AI detected.";
                return;
            }

            int active = aiPlayers.Count(p => p.IsActive);
            int alive = aiPlayers.Count(p => p.IsAlive);
            int valid = aiPlayers.Count(p => IsValidPosition(p.Position));

            var nearest = aiPlayers
                .Where(p => p.IsAIActive && IsValidPosition(p.Position))
                .Select(p => new
                {
                    Player = p,
                    Distance = Vector3.Distance(localPlayer.Position, p.Position)
                })
                .OrderBy(p => p.Distance)
                .Take(5)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("=== AI Debug ===");
            sb.AppendLine($"Total: {aiPlayers.Count} | Active: {active} | Alive: {alive} | ValidPos: {valid}");

            var maxDist = App.Config.UI.EspAIMaxDistance;
            sb.AppendLine($"ESP Max Distance: {(maxDist > 0 ? maxDist.ToString("F0") + "m" : "unlimited")}");

            if (nearest.Count == 0)
            {
                sb.AppendLine("Closest: none within filters.");
            }
            else
            {
                sb.AppendLine("Closest AI (up to 5):");
                foreach (var entry in nearest)
                {
                    var name = string.IsNullOrWhiteSpace(entry.Player.Name) ? "AI" : entry.Player.Name;
                    string groupInfo = entry.Player.GroupID >= 0 ? $" G{entry.Player.GroupID}" : string.Empty;
                    sb.AppendLine($"- {name} [{entry.Player.Type}]{groupInfo}: {entry.Distance:F1}m @ {entry.Player.Position}");
                }
            }

            AiDebugText = sb.ToString();
        }

        private void RefreshLootDebug()
        {
            var loot = Memory.Game?.Loot?.AllLoot?.ToList();
            var localPlayer = Memory.LocalPlayer;

            if (loot is null || loot.Count == 0)
            {
                LootDebugText = "Loot Debug Overlay: waiting for raid or loot.";
                return;
            }

            int validPositions = loot.Count(item => IsValidPosition(item.Position));
            int corpses = loot.Count(item => item is LootCorpse);
            int airdrops = loot.Count(item => item is LootAirdrop);
            int containers = loot.Count(item => item is StaticLootContainer);

            List<(LootItem item, float distance)> nearest = null;
            if (localPlayer is not null)
            {
                nearest = loot
                    .Where(item => item is not null && IsValidPosition(item.Position))
                    .Select(item => (item, distance: Vector3.Distance(localPlayer.Position, item.Position)))
                    .OrderBy(x => x.distance)
                    .Take(5)
                    .ToList();
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Loot Debug ===");
            sb.AppendLine($"Total: {loot.Count} | ValidPos: {validPositions} | Corpses: {corpses} | Airdrops: {airdrops} | Static: {containers}");

            if (nearest is null || nearest.Count == 0)
            {
                sb.AppendLine("Closest: unavailable (no local player or no valid positions).");
            }
            else
            {
                sb.AppendLine("Closest loot (up to 5):");
                foreach (var entry in nearest)
                {
                    var shortName = string.IsNullOrWhiteSpace(entry.item.ShortName) ? entry.item.Name : entry.item.ShortName;
                    sb.AppendLine($"- {shortName} [{entry.item.GetType().Name}] {entry.distance:F1}m @ {entry.item.Position}");
                }
            }

            LootDebugText = sb.ToString();
        }

        private static bool IsValidPosition(Vector3 pos)
        {
            return pos != Vector3.Zero &&
                   !float.IsNaN(pos.X) && !float.IsNaN(pos.Y) && !float.IsNaN(pos.Z) &&
                   !float.IsInfinity(pos.X) && !float.IsInfinity(pos.Y) && !float.IsInfinity(pos.Z);
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
