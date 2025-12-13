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

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LoneEftDmaRadar.UI.Data;
using LoneEftDmaRadar.UI.Radar.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class PlayerWhitelistViewModel : INotifyPropertyChanged
    {
        private readonly PlayerWhitelistTab _parent;
        private readonly System.Timers.Timer _debounceTimer;
        private readonly HashSet<PlayerWhitelistEntry> _pendingUpdates = new();

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private readonly ConcurrentDictionary<string, PlayerWhitelistEntry> _whitelist = new(App.Config.PlayerWhitelist
            .GroupBy(p => p.AcctID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                k => k.Key, v => v.First(),
                StringComparer.OrdinalIgnoreCase));
        /// <summary>
        /// Whitelist for Lookups.
        /// </summary>
        public IReadOnlyDictionary<string, PlayerWhitelistEntry> Whitelist => _whitelist;

        /// <summary>
        /// Observable Collection used for Data Binding.
        /// </summary>
        public ObservableCollection<PlayerWhitelistEntry> WhitelistItems { get; } = new();

        public PlayerWhitelistViewModel(PlayerWhitelistTab parent)
        {
            _parent = parent;

            _debounceTimer = new System.Timers.Timer(500)
            {
                AutoReset = false
            };
            _debounceTimer.Elapsed += DebounceTimer_Elapsed;

            foreach (var entry in App.Config.PlayerWhitelist.OrderByDescending(x => x.Timestamp))
            {
                WhitelistItems.Add(entry);
                entry.PropertyChanged += Entry_PropertyChanged;
            }

            WhitelistItems.CollectionChanged += WhitelistItems_CollectionChanged;
        }

        private void DebounceTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _parent.Dispatcher.Invoke(() =>
            {
                var entriesToUpdate = new List<PlayerWhitelistEntry>(_pendingUpdates);
                _pendingUpdates.Clear();

                foreach (var entry in entriesToUpdate)
                {
                    ProcessEntryUpdate(entry);
                }

                OnPropertyChanged(nameof(WhitelistCount));
            });
        }

        private void ProcessEntryUpdate(PlayerWhitelistEntry entry)
        {
            if (entry.IsValidEntry)
            {
                _whitelist.AddOrUpdate(entry.AcctID, entry, (key, existing) => entry);
                if (!App.Config.PlayerWhitelist.Any(x => x.AcctID == entry.AcctID))
                {
                    App.Config.PlayerWhitelist.Add(entry);
                }
            }
            else
            {
                _whitelist.TryRemove(entry.AcctID, out _);
                var configEntry = App.Config.PlayerWhitelist.FirstOrDefault(x => x.AcctID == entry.AcctID);
                if (configEntry != null)
                {
                    _ = App.Config.PlayerWhitelist.Remove(configEntry);
                }
            }
        }

        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is PlayerWhitelistEntry entry && e.PropertyName == nameof(PlayerWhitelistEntry.IsValidEntry))
            {
                lock (_pendingUpdates)
                {
                    _pendingUpdates.Add(entry);
                }
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void WhitelistItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add &&
                e.NewItems is not null)
            {
                foreach (PlayerWhitelistEntry entry in e.NewItems)
                {
                    entry.PropertyChanged += Entry_PropertyChanged;

                    if (entry.IsValidEntry)
                    {
                        _whitelist.AddOrUpdate(entry.AcctID, entry, (key, existing) => entry);

                        if (!App.Config.PlayerWhitelist.Any(x => x.AcctID == entry.AcctID))
                        {
                            App.Config.PlayerWhitelist.Add(entry);
                        }
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove &&
                e.OldItems is not null)
            {
                foreach (PlayerWhitelistEntry entry in e.OldItems)
                {
                    entry.PropertyChanged -= Entry_PropertyChanged;

                    _whitelist.TryRemove(entry.AcctID, out _);

                    var configEntry = App.Config.PlayerWhitelist.FirstOrDefault(x => x.AcctID == entry.AcctID);
                    if (configEntry != null)
                    {
                        _ = App.Config.PlayerWhitelist.Remove(configEntry);
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                if (e.NewItems is not null && e.OldItems is not null)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        if (e.NewItems[i] is PlayerWhitelistEntry newEntry && e.OldItems[i] is PlayerWhitelistEntry oldEntry)
                        {
                            _whitelist.AddOrUpdate(newEntry.AcctID, newEntry, (key, existing) => newEntry);

                            var configEntry = App.Config.PlayerWhitelist.FirstOrDefault(x => x.AcctID == oldEntry.AcctID);
                            if (configEntry != null)
                            {
                                configEntry.AcctID = newEntry.AcctID;
                                configEntry.CustomName = newEntry.CustomName;
                            }

                            }
                    }
                }
            }

            OnPropertyChanged(nameof(WhitelistCount));
        }

        /// <summary>
        /// Removes a player from the whitelist.
        /// </summary>
        /// <param name="acctId">Account ID to remove</param>
        public void RemovePlayer(string acctId)
        {
            if (_whitelist.TryRemove(acctId, out var entry))
            {
                _ = WhitelistItems.Remove(entry);
                _ = App.Config.PlayerWhitelist.Remove(entry);
                OnPropertyChanged(nameof(WhitelistCount));
            }
        }

        /// <summary>
        /// Adds a player to the whitelist.
        /// </summary>
        /// <param name="acctId">Account ID to add</param>
        /// <param name="customName">Custom name for the player</param>
        public void AddPlayer(string acctId, string customName)
        {
            if (string.IsNullOrWhiteSpace(acctId) || string.IsNullOrWhiteSpace(customName))
                return;

            var newEntry = new PlayerWhitelistEntry
            {
                AcctID = acctId,
                CustomName = customName
            };

            WhitelistItems.Insert(0, newEntry);
        }

        /// <summary>
        /// Gets the count of players in the whitelist.
        /// </summary>
        public int WhitelistCount => WhitelistItems.Count(e => !string.IsNullOrWhiteSpace(e.AcctID) && !string.IsNullOrWhiteSpace(e.CustomName));

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();

            foreach (var entry in WhitelistItems)
            {
                entry.PropertyChanged -= Entry_PropertyChanged;
            }
            WhitelistItems.CollectionChanged -= WhitelistItems_CollectionChanged;
        }
    }
}