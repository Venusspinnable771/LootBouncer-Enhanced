/*
Modified & Updated Branch of Lootbouncer by VisEntities. The readme notes can be found at https://github.com/SeesAll/
I take no credit for the original plugin found on uMod. I merely changed a few things to improve upon it.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
[Info("Loot Bouncer", "Sorrow/Arainrr, enhanced by SeesAll", "1.3.1")]
    [Description("Automatically clears abandoned loot containers and optional junkpiles when players leave items behind")]
    public class LootBouncer : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Slap, Trade;

        private readonly Dictionary<ulong, LootTrackData> _trackedLoot = new Dictionary<ulong, LootTrackData>();
        private readonly Dictionary<ulong, Timer> _junkPileCleanupTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, Timer> _roadsideVehicleCleanupTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, JunkPile> _junkPileAssociations = new Dictionary<ulong, JunkPile>();
        private readonly Dictionary<ulong, BaseEntity> _roadsideVehicleAssociations = new Dictionary<ulong, BaseEntity>();
        private readonly HashSet<string> _barrelShortPrefabNames = new HashSet<string>(StringComparer.Ordinal);

        #endregion Fields

        #region Data Classes

        private class LootTrackData
        {
            public int InitialItemCount;
            public bool PendingCleanup;
            public double LastInteractionTime;
            public readonly HashSet<ulong> PlayerIds = new HashSet<ulong>();
            public Timer CleanupTimer;
        }

        #endregion Data Classes

        #region Oxide Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnPlayerAttack));
        }

        private void OnServerInitialized()
        {
            RefreshContainerCaches();

            if (configData.slapPlayer && Slap == null)
            {
                PrintError("Slap is not loaded, get it at https://umod.org/plugins/slap");
            }

            if (_barrelShortPrefabNames.Any(shortPrefabName => IsContainerEnabled(shortPrefabName)))
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnPlayerAttack));
            }
        }

        private void Unload()
        {
            foreach (var track in _trackedLoot.Values)
            {
                track.CleanupTimer?.Destroy();
            }

            foreach (var cleanupTimer in _junkPileCleanupTimers.Values)
            {
                cleanupTimer?.Destroy();
            }

            foreach (var cleanupTimer in _roadsideVehicleCleanupTimers.Values)
            {
                cleanupTimer?.Destroy();
            }

            _trackedLoot.Clear();
            _junkPileCleanupTimers.Clear();
            _roadsideVehicleCleanupTimers.Clear();
            _junkPileAssociations.Clear();
            _roadsideVehicleAssociations.Clear();
            _barrelShortPrefabNames.Clear();
        }

        private void OnLootEntity(BasePlayer player, LootContainer lootContainer)
        {
            if (!CanProcessLootContainer(player, lootContainer))
            {
                return;
            }

            var entityId = lootContainer.net.ID.Value;
            var track = GetOrCreateTrack(entityId);
            track.InitialItemCount = GetInventoryItemCount(lootContainer);
            track.LastInteractionTime = GetNow();
            track.PlayerIds.Add(player.userID);
            CancelLootCleanupTimer(track);
        }

        private void OnLootEntityEnd(BasePlayer player, LootContainer lootContainer)
        {
            if (player == null || lootContainer == null || lootContainer.net == null)
            {
                return;
            }

            var entityId = lootContainer.net.ID.Value;
            LootTrackData track;
            if (!_trackedLoot.TryGetValue(entityId, out track))
            {
                return;
            }

            track.LastInteractionTime = GetNow();

            var itemCount = GetInventoryItemCount(lootContainer);
            if (itemCount <= 0)
            {
                track.PendingCleanup = false;
                track.PlayerIds.Remove(player.userID);
                ClearLootTrackingIfUnused(entityId, track);
                return;
            }

            var removedItems = itemCount < track.InitialItemCount;
            if (removedItems || track.PendingCleanup)
            {
                ScheduleLootCleanup(entityId, lootContainer, track);
                EvaluateAssociatedSpawnBlocking(lootContainer);
                ScheduleAssociatedGroupCleanup(lootContainer);
                return;
            }

            track.PlayerIds.Remove(player.userID);
            ClearLootTrackingIfUnused(entityId, track);
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || !attacker.userID.IsSteamId())
            {
                return;
            }

            var lootContainer = info?.HitEntity as LootContainer;
            if (lootContainer == null || lootContainer.net == null)
            {
                return;
            }

            if (!_barrelShortPrefabNames.Contains(lootContainer.ShortPrefabName) || !IsContainerEnabled(lootContainer.ShortPrefabName))
            {
                return;
            }

            var entityId = lootContainer.net.ID.Value;
            var track = GetOrCreateTrack(entityId);
            track.PlayerIds.Add(attacker.userID);
            track.LastInteractionTime = GetNow();
            ScheduleLootCleanup(entityId, lootContainer, track);
            EvaluateAssociatedSpawnBlocking(lootContainer);
            ScheduleAssociatedGroupCleanup(lootContainer);
        }

        private void OnEntityDeath(LootContainer lootContainer, HitInfo info)
        {
            if (lootContainer == null || lootContainer.net == null)
            {
                return;
            }

            if (!_barrelShortPrefabNames.Contains(lootContainer.ShortPrefabName))
            {
                return;
            }

            var attacker = info?.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId())
            {
                return;
            }

            LootTrackData track;
            if (!_trackedLoot.TryGetValue(lootContainer.net.ID.Value, out track))
            {
                return;
            }

            track.PlayerIds.Remove(attacker.userID);
            ClearLootTrackingIfUnused(lootContainer.net.ID.Value, track);
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            var lootContainer = entity as LootContainer;
            if (lootContainer != null && lootContainer.net != null)
            {
                HandleLootContainerKill(lootContainer);
                return;
            }

            var junkPile = entity as JunkPile;
            if (junkPile != null && junkPile.net != null)
            {
                HandleJunkPileKill(junkPile);
                return;
            }

            var baseEntity = entity as BaseEntity;
            if (baseEntity != null && baseEntity.net != null && LooksLikeRoadsideVehicleAnchor(baseEntity.ShortPrefabName))
            {
                HandleRoadsideVehicleKill(baseEntity);
            }
        }

        #endregion Oxide Hooks

        #region Methods

        private LootTrackData GetOrCreateTrack(ulong entityId)
        {
            LootTrackData track;
            if (!_trackedLoot.TryGetValue(entityId, out track))
            {
                track = new LootTrackData();
                _trackedLoot.Add(entityId, track);
            }

            return track;
        }

        private bool CanProcessLootContainer(BasePlayer player, LootContainer lootContainer)
        {
            if (player == null || lootContainer == null || lootContainer.net == null)
            {
                return false;
            }

            if (!IsContainerEnabled(lootContainer.ShortPrefabName))
            {
                return false;
            }

            var tradeResult = Trade?.Call("IsTradeBox", lootContainer);
            return !(tradeResult is bool) || !(bool)tradeResult;
        }

        private bool IsContainerEnabled(string shortPrefabName)
        {
            bool enabled;
            return !configData.lootContainers.TryGetValue(shortPrefabName, out enabled) || enabled;
        }

        private int GetInventoryItemCount(LootContainer lootContainer)
        {
            return lootContainer?.inventory?.itemList?.Count ?? 0;
        }

        private double GetNow()
        {
            return Interface.Oxide.Now;
        }

        private float GetRoadsideCleanupRadius()
        {
            return Math.Max(1f, configData.maximumCleanupRadiusForRoadsideGroups);
        }

        private double GetStaleLootAgeThreshold()
        {
            return Math.Max(5d, configData.timeBeforeLootEmpty);
        }

        private void CancelJunkPileCleanupTimer(ulong junkPileId)
        {
            Timer cleanupTimer;
            if (!_junkPileCleanupTimers.TryGetValue(junkPileId, out cleanupTimer))
            {
                return;
            }

            cleanupTimer?.Destroy();
            _junkPileCleanupTimers.Remove(junkPileId);
        }

        private void CancelRoadsideVehicleCleanupTimer(ulong entityId)
        {
            Timer cleanupTimer;
            if (!_roadsideVehicleCleanupTimers.TryGetValue(entityId, out cleanupTimer))
            {
                return;
            }

            cleanupTimer?.Destroy();
            _roadsideVehicleCleanupTimers.Remove(entityId);
        }

        private static SpawnGroup GetSpawnGroup(BaseEntity entity)
        {
            return entity?.GetComponent<SpawnPointInstance>()?.parentSpawnPointUser as SpawnGroup;
        }

        private bool IsTrackedContainerStale(LootContainer lootContainer, LootTrackData track, double now)
        {
            if (track == null || lootContainer == null || lootContainer.IsDestroyed || GetInventoryItemCount(lootContainer) <= 0)
            {
                return false;
            }

            if (track.PendingCleanup)
            {
                return true;
            }

            return now - track.LastInteractionTime >= GetStaleLootAgeThreshold();
        }

        private bool TryGetJunkPileContainers(JunkPile junkPile, List<LootContainer> results)
        {
            if (junkPile == null || junkPile.net == null || junkPile.IsDestroyed || results == null)
            {
                return false;
            }

            results.Clear();
            Vis.Entities(junkPile.transform.position, GetRoadsideCleanupRadius(), results, Layers.Solid);
            return true;
        }

        private bool IsContainerInJunkPileGroup(LootContainer lootContainer, JunkPile junkPile)
        {
            if (lootContainer == null || junkPile == null || junkPile.spawngroups == null)
            {
                return false;
            }

            var lootSpawnGroup = GetSpawnGroup(lootContainer);
            return lootSpawnGroup != null && junkPile.spawngroups.Contains(lootSpawnGroup);
        }

        private static bool IsEntityInSpawnGroup(BaseEntity entity, SpawnGroup spawnGroup)
        {
            return entity != null && spawnGroup != null && ReferenceEquals(GetSpawnGroup(entity), spawnGroup);
        }

        private static bool LooksLikeRoadsideVehicleAnchor(string shortPrefabName)
        {
            if (string.IsNullOrEmpty(shortPrefabName))
            {
                return false;
            }

            return shortPrefabName.IndexOf("vehicle", StringComparison.OrdinalIgnoreCase) >= 0
                || shortPrefabName.IndexOf("wreck", StringComparison.OrdinalIgnoreCase) >= 0
                || shortPrefabName.IndexOf("van", StringComparison.OrdinalIgnoreCase) >= 0
                || shortPrefabName.IndexOf("truck", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TryFindAssociatedRoadsideVehicleAnchor(LootContainer lootContainer, out BaseEntity anchorEntity, out SpawnGroup spawnGroup)
        {
            anchorEntity = null;
            spawnGroup = GetSpawnGroup(lootContainer);
            if (lootContainer == null || lootContainer.net == null || spawnGroup == null)
            {
                return false;
            }

            var entityId = lootContainer.net.ID.Value;
            if (_roadsideVehicleAssociations.TryGetValue(entityId, out anchorEntity))
            {
                if (anchorEntity != null && !anchorEntity.IsDestroyed && anchorEntity.net != null && IsEntityInSpawnGroup(anchorEntity, spawnGroup))
                {
                    return true;
                }

                _roadsideVehicleAssociations.Remove(entityId);
                anchorEntity = null;
            }

            var nearbyEntities = Pool.Get<List<BaseEntity>>();
            nearbyEntities.Clear();

            try
            {
                Vis.Entities(lootContainer.transform.position, GetRoadsideCleanupRadius(), nearbyEntities, Layers.Solid);

                foreach (var nearbyEntity in nearbyEntities)
                {
                    if (nearbyEntity == null || nearbyEntity.IsDestroyed || nearbyEntity.net == null)
                    {
                        continue;
                    }

                    if (nearbyEntity == lootContainer || !LooksLikeRoadsideVehicleAnchor(nearbyEntity.ShortPrefabName) || !IsEntityInSpawnGroup(nearbyEntity, spawnGroup))
                    {
                        continue;
                    }

                    anchorEntity = nearbyEntity;
                    _roadsideVehicleAssociations[entityId] = anchorEntity;
                    return true;
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref nearbyEntities);
            }

            return false;
        }

        private bool TryGetRoadsideVehicleGroupContainers(BaseEntity anchorEntity, SpawnGroup spawnGroup, List<LootContainer> results)
        {
            if (anchorEntity == null || anchorEntity.net == null || anchorEntity.IsDestroyed || spawnGroup == null || results == null)
            {
                return false;
            }

            results.Clear();
            Vis.Entities(anchorEntity.transform.position, GetRoadsideCleanupRadius(), results, Layers.Solid);
            results.RemoveAll(container => container == null || container.IsDestroyed || container.net == null || !IsEntityInSpawnGroup(container, spawnGroup));
            return true;
        }

        private void ScheduleAssociatedSpawnBlockingCleanup(IEnumerable<LootContainer> nearbyLootContainers, Func<LootContainer, bool> belongsToGroup)
        {
            if (nearbyLootContainers == null || belongsToGroup == null)
            {
                return;
            }

            var now = GetNow();
            foreach (var nearbyLootContainer in nearbyLootContainers)
            {
                if (nearbyLootContainer == null || nearbyLootContainer.IsDestroyed || nearbyLootContainer.net == null)
                {
                    continue;
                }

                if (!belongsToGroup(nearbyLootContainer) || !IsContainerEnabled(nearbyLootContainer.ShortPrefabName))
                {
                    continue;
                }

                var relatedEntityId = nearbyLootContainer.net.ID.Value;
                LootTrackData relatedTrack;
                if (!_trackedLoot.TryGetValue(relatedEntityId, out relatedTrack))
                {
                    continue;
                }

                if (!IsTrackedContainerStale(nearbyLootContainer, relatedTrack, now))
                {
                    continue;
                }

                ScheduleLootCleanup(relatedEntityId, nearbyLootContainer, relatedTrack);
            }
        }

        private void EvaluateAssociatedSpawnBlocking(LootContainer sourceContainer)
        {
            var junkPile = FindAssociatedJunkPile(sourceContainer);
            if (junkPile != null)
            {
                var nearbyLootContainers = Pool.Get<List<LootContainer>>();
                try
                {
                    if (!TryGetJunkPileContainers(junkPile, nearbyLootContainers))
                    {
                        return;
                    }

                    ScheduleAssociatedSpawnBlockingCleanup(nearbyLootContainers, nearbyLootContainer => IsContainerInJunkPileGroup(nearbyLootContainer, junkPile));
                    return;
                }
                finally
                {
                    Pool.FreeUnmanaged(ref nearbyLootContainers);
                }
            }

            BaseEntity anchorEntity;
            SpawnGroup spawnGroup;
            if (!TryFindAssociatedRoadsideVehicleAnchor(sourceContainer, out anchorEntity, out spawnGroup))
            {
                return;
            }

            var nearbyVehicleLootContainers = Pool.Get<List<LootContainer>>();
            try
            {
                if (!TryGetRoadsideVehicleGroupContainers(anchorEntity, spawnGroup, nearbyVehicleLootContainers))
                {
                    return;
                }

                ScheduleAssociatedSpawnBlockingCleanup(nearbyVehicleLootContainers, nearbyLootContainer => IsEntityInSpawnGroup(nearbyLootContainer, spawnGroup));
            }
            finally
            {
                Pool.FreeUnmanaged(ref nearbyVehicleLootContainers);
            }
        }

        private void ScheduleLootCleanup(ulong entityId, LootContainer lootContainer, LootTrackData track)
        {
            if (track == null)
            {
                return;
            }

            track.PendingCleanup = true;

            if (track.CleanupTimer != null && !track.CleanupTimer.Destroyed)
            {
                return;
            }

            track.CleanupTimer = timer.Once(configData.timeBeforeLootEmpty, () =>
            {
                track.CleanupTimer = null;

                LootTrackData currentTrack;
                if (!_trackedLoot.TryGetValue(entityId, out currentTrack) || !ReferenceEquals(currentTrack, track))
                {
                    return;
                }

                DropItems(lootContainer);
            });
        }

        private void CancelLootCleanupTimer(LootTrackData track)
        {
            if (track == null || track.CleanupTimer == null)
            {
                return;
            }

            track.CleanupTimer.Destroy();
            track.CleanupTimer = null;
        }

        private void ClearLootTrackingIfUnused(ulong entityId, LootTrackData track)
        {
            if (track == null)
            {
                return;
            }

            if (track.PlayerIds.Count > 0)
            {
                return;
            }

            if (track.PendingCleanup)
            {
                return;
            }

            if (track.CleanupTimer != null && !track.CleanupTimer.Destroyed)
            {
                return;
            }

            _trackedLoot.Remove(entityId);
            _junkPileAssociations.Remove(entityId);
            _roadsideVehicleAssociations.Remove(entityId);
        }

        private void HandleLootContainerKill(LootContainer lootContainer)
        {
            var entityId = lootContainer.net.ID.Value;
            LootTrackData track;
            if (_trackedLoot.TryGetValue(entityId, out track))
            {
                CancelLootCleanupTimer(track);
                _trackedLoot.Remove(entityId);

                if (configData.slapPlayer && Slap != null)
                {
                    foreach (var playerId in track.PlayerIds)
                    {
                        var player = BasePlayer.FindByID(playerId);
                        if (player == null || player.IPlayer == null)
                        {
                            continue;
                        }

                        Slap.Call("SlapPlayer", player.IPlayer);
                        Print(player, Lang("SlapMessage", player.UserIDString));
                    }
                }
            }

            _junkPileAssociations.Remove(entityId);
            _roadsideVehicleAssociations.Remove(entityId);
        }

        private void HandleJunkPileKill(JunkPile junkPile)
        {
            var junkPileId = junkPile.net.ID.Value;

            CancelJunkPileCleanupTimer(junkPileId);

            var associatedContainerIds = Pool.Get<List<ulong>>();
            associatedContainerIds.Clear();

            try
            {
                foreach (var entry in _junkPileAssociations)
                {
                    if (entry.Value == junkPile)
                    {
                        associatedContainerIds.Add(entry.Key);
                    }
                }

                foreach (var containerId in associatedContainerIds)
                {
                    _junkPileAssociations.Remove(containerId);
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref associatedContainerIds);
            }
        }

        private void HandleRoadsideVehicleKill(BaseEntity anchorEntity)
        {
            var entityId = anchorEntity.net.ID.Value;
            CancelRoadsideVehicleCleanupTimer(entityId);

            var associatedContainerIds = Pool.Get<List<ulong>>();
            associatedContainerIds.Clear();

            try
            {
                foreach (var entry in _roadsideVehicleAssociations)
                {
                    if (entry.Value == anchorEntity)
                    {
                        associatedContainerIds.Add(entry.Key);
                    }
                }

                foreach (var containerId in associatedContainerIds)
                {
                    _roadsideVehicleAssociations.Remove(containerId);
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref associatedContainerIds);
            }
        }

        private void DropItems(LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer.IsDestroyed)
            {
                return;
            }

            if (lootContainer.net != null)
            {
                LootTrackData track;
                if (_trackedLoot.TryGetValue(lootContainer.net.ID.Value, out track))
                {
                    track.PendingCleanup = false;
                }
            }

            var inventory = lootContainer.inventory;
            if (inventory != null)
            {
                if (configData.removeItems)
                {
                    inventory.Clear();
                }
                else if (inventory.itemList != null && inventory.itemList.Count > 0)
                {
                    DropUtil.DropItems(inventory, lootContainer.GetDropPosition());
                }
            }

            lootContainer.RemoveMe();
        }

        private void ScheduleAssociatedGroupCleanup(LootContainer lootContainer)
        {
            if (!configData.emptyJunkpile)
            {
                return;
            }

            if (FindAssociatedJunkPile(lootContainer) != null)
            {
                ScheduleJunkPileCleanup(lootContainer);
                return;
            }

            ScheduleRoadsideVehicleCleanup(lootContainer);
        }

        private void ScheduleJunkPileCleanup(LootContainer lootContainer)
        {
            if (!configData.emptyJunkpile)
            {
                return;
            }

            var junkPile = FindAssociatedJunkPile(lootContainer);
            if (junkPile == null || junkPile.net == null || junkPile.IsDestroyed)
            {
                return;
            }

            var junkPileId = junkPile.net.ID.Value;
            Timer existingTimer;
            if (_junkPileCleanupTimers.TryGetValue(junkPileId, out existingTimer))
            {
                if (existingTimer != null && !existingTimer.Destroyed)
                {
                    return;
                }

                _junkPileCleanupTimers.Remove(junkPileId);
            }

            _junkPileCleanupTimers.Add(junkPileId, timer.Once(configData.timeBeforeJunkpileEmpty, () =>
            {
                _junkPileCleanupTimers.Remove(junkPileId);

                if (junkPile == null || junkPile.IsDestroyed || junkPile.net == null)
                {
                    return;
                }

                var nearbyLootContainers = Pool.Get<List<LootContainer>>();
                try
                {
                    if (!TryGetJunkPileContainers(junkPile, nearbyLootContainers))
                    {
                        return;
                    }

                    var now = GetNow();
                    var totalRelevantContainers = 0;
                    var remainingContainersWithLoot = 0;
                    var stalledContainersWithLoot = 0;

                    foreach (var nearbyLootContainer in nearbyLootContainers)
                    {
                        if (nearbyLootContainer == null || nearbyLootContainer.IsDestroyed || nearbyLootContainer.net == null)
                        {
                            continue;
                        }

                        if (!IsContainerInJunkPileGroup(nearbyLootContainer, junkPile) || !IsContainerEnabled(nearbyLootContainer.ShortPrefabName))
                        {
                            continue;
                        }

                        totalRelevantContainers++;

                        if (GetInventoryItemCount(nearbyLootContainer) <= 0)
                        {
                            continue;
                        }

                        remainingContainersWithLoot++;

                        var relatedEntityId = nearbyLootContainer.net.ID.Value;
                        LootTrackData relatedTrack;
                        if (_trackedLoot.TryGetValue(relatedEntityId, out relatedTrack) && IsTrackedContainerStale(nearbyLootContainer, relatedTrack, now))
                        {
                            stalledContainersWithLoot++;
                            ScheduleLootCleanup(relatedEntityId, nearbyLootContainer, relatedTrack);
                        }
                    }

                    var shouldDestroyJunkPile = remainingContainersWithLoot == 0;
                    if (!shouldDestroyJunkPile && totalRelevantContainers > 0)
                    {
                        var clampedThreshold = Math.Min(1f, Math.Max(0f, configData.junkPileCleanupThreshold));
                        var lootedContainerCount = totalRelevantContainers - remainingContainersWithLoot;
                        var lootedRatio = (float)lootedContainerCount / totalRelevantContainers;
                        var thresholdMet = lootedRatio >= clampedThreshold;

                        shouldDestroyJunkPile = thresholdMet && stalledContainersWithLoot == remainingContainersWithLoot;
                    }

                    if (!shouldDestroyJunkPile)
                    {
                        return;
                    }

                    if (configData.dropNearbyLoot)
                    {
                        DropNearbyJunkPileLoot(junkPile);
                    }

                    CancelJunkPileCleanupTimer(junkPileId);
                    junkPile.SinkAndDestroy();
                }
                finally
                {
                    Pool.FreeUnmanaged(ref nearbyLootContainers);
                }
            }));
        }

        private void ScheduleRoadsideVehicleCleanup(LootContainer lootContainer)
        {
            BaseEntity anchorEntity;
            SpawnGroup spawnGroup;
            if (!TryFindAssociatedRoadsideVehicleAnchor(lootContainer, out anchorEntity, out spawnGroup) || anchorEntity == null || anchorEntity.net == null)
            {
                return;
            }

            var anchorEntityId = anchorEntity.net.ID.Value;
            Timer existingTimer;
            if (_roadsideVehicleCleanupTimers.TryGetValue(anchorEntityId, out existingTimer))
            {
                if (existingTimer != null && !existingTimer.Destroyed)
                {
                    return;
                }

                _roadsideVehicleCleanupTimers.Remove(anchorEntityId);
            }

            _roadsideVehicleCleanupTimers.Add(anchorEntityId, timer.Once(configData.timeBeforeJunkpileEmpty, () =>
            {
                _roadsideVehicleCleanupTimers.Remove(anchorEntityId);

                if (anchorEntity == null || anchorEntity.IsDestroyed || anchorEntity.net == null)
                {
                    return;
                }

                var nearbyLootContainers = Pool.Get<List<LootContainer>>();
                try
                {
                    if (!TryGetRoadsideVehicleGroupContainers(anchorEntity, spawnGroup, nearbyLootContainers))
                    {
                        return;
                    }

                    var now = GetNow();
                    var totalRelevantContainers = 0;
                    var remainingContainersWithLoot = 0;
                    var stalledContainersWithLoot = 0;

                    foreach (var nearbyLootContainer in nearbyLootContainers)
                    {
                        if (nearbyLootContainer == null || nearbyLootContainer.IsDestroyed || nearbyLootContainer.net == null)
                        {
                            continue;
                        }

                        if (!IsEntityInSpawnGroup(nearbyLootContainer, spawnGroup) || !IsContainerEnabled(nearbyLootContainer.ShortPrefabName))
                        {
                            continue;
                        }

                        totalRelevantContainers++;

                        if (GetInventoryItemCount(nearbyLootContainer) <= 0)
                        {
                            continue;
                        }

                        remainingContainersWithLoot++;

                        var relatedEntityId = nearbyLootContainer.net.ID.Value;
                        LootTrackData relatedTrack;
                        if (_trackedLoot.TryGetValue(relatedEntityId, out relatedTrack) && IsTrackedContainerStale(nearbyLootContainer, relatedTrack, now))
                        {
                            stalledContainersWithLoot++;
                            ScheduleLootCleanup(relatedEntityId, nearbyLootContainer, relatedTrack);
                        }
                    }

                    var shouldDestroyGroup = remainingContainersWithLoot == 0;
                    if (!shouldDestroyGroup && totalRelevantContainers > 0)
                    {
                        var clampedThreshold = Math.Min(1f, Math.Max(0f, configData.junkPileCleanupThreshold));
                        var lootedContainerCount = totalRelevantContainers - remainingContainersWithLoot;
                        var lootedRatio = (float)lootedContainerCount / totalRelevantContainers;
                        var thresholdMet = lootedRatio >= clampedThreshold;

                        shouldDestroyGroup = thresholdMet && stalledContainersWithLoot == remainingContainersWithLoot;
                    }

                    if (!shouldDestroyGroup)
                    {
                        return;
                    }

                    DropNearbyRoadsideVehicleLoot(anchorEntity, spawnGroup);
                    DestroyRoadsideVehicleAnchors(anchorEntity, spawnGroup);
                    CancelRoadsideVehicleCleanupTimer(anchorEntityId);
                }
                finally
                {
                    Pool.FreeUnmanaged(ref nearbyLootContainers);
                }
            }));
        }

        private void DestroyRoadsideVehicleAnchors(BaseEntity anchorEntity, SpawnGroup spawnGroup)
        {
            if (anchorEntity == null || anchorEntity.IsDestroyed || spawnGroup == null)
            {
                return;
            }

            var nearbyEntities = Pool.Get<List<BaseEntity>>();
            nearbyEntities.Clear();

            try
            {
                Vis.Entities(anchorEntity.transform.position, GetRoadsideCleanupRadius(), nearbyEntities, Layers.Solid);

                foreach (var nearbyEntity in nearbyEntities)
                {
                    if (nearbyEntity == null || nearbyEntity.IsDestroyed || nearbyEntity.net == null)
                    {
                        continue;
                    }

                    if (LooksLikeRoadsideVehicleAnchor(nearbyEntity.ShortPrefabName) && IsEntityInSpawnGroup(nearbyEntity, spawnGroup))
                    {
                        nearbyEntity.Kill();
                    }
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref nearbyEntities);
            }
        }

        private void DropNearbyRoadsideVehicleLoot(BaseEntity anchorEntity, SpawnGroup spawnGroup)
        {
            if (anchorEntity == null || anchorEntity.IsDestroyed || spawnGroup == null)
            {
                return;
            }

            var nearbyLootContainers = Pool.Get<List<LootContainer>>();
            nearbyLootContainers.Clear();

            try
            {
                if (!TryGetRoadsideVehicleGroupContainers(anchorEntity, spawnGroup, nearbyLootContainers))
                {
                    return;
                }

                foreach (var nearbyLootContainer in nearbyLootContainers)
                {
                    if (nearbyLootContainer == null || nearbyLootContainer.IsDestroyed || nearbyLootContainer.net == null)
                    {
                        continue;
                    }

                    if (IsEntityInSpawnGroup(nearbyLootContainer, spawnGroup))
                    {
                        DropItems(nearbyLootContainer);
                    }
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref nearbyLootContainers);
            }
        }

        private JunkPile FindAssociatedJunkPile(LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer.net == null)
            {
                return null;
            }

            var entityId = lootContainer.net.ID.Value;
            JunkPile junkPile;
            if (_junkPileAssociations.TryGetValue(entityId, out junkPile))
            {
                if (junkPile != null && !junkPile.IsDestroyed && junkPile.net != null)
                {
                    return junkPile;
                }

                _junkPileAssociations.Remove(entityId);
            }

            var spawnGroup = lootContainer.GetComponent<SpawnPointInstance>()?.parentSpawnPointUser as SpawnGroup;
            if (spawnGroup == null)
            {
                return null;
            }

            var nearbyJunkPiles = Pool.Get<List<JunkPile>>();
            nearbyJunkPiles.Clear();

            JunkPile matchedJunkPile = null;
            try
            {
                Vis.Entities(lootContainer.transform.position, GetRoadsideCleanupRadius(), nearbyJunkPiles, Layers.Solid);

                foreach (var nearbyJunkPile in nearbyJunkPiles)
                {
                    if (nearbyJunkPile == null || nearbyJunkPile.spawngroups == null)
                    {
                        continue;
                    }

                    if (nearbyJunkPile.spawngroups.Contains(spawnGroup))
                    {
                        matchedJunkPile = nearbyJunkPile;
                        break;
                    }
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref nearbyJunkPiles);
            }

            if (matchedJunkPile != null)
            {
                _junkPileAssociations[entityId] = matchedJunkPile;
            }

            return matchedJunkPile;
        }

        private void DropNearbyJunkPileLoot(JunkPile junkPile)
        {
            var nearbyLootContainers = Pool.Get<List<LootContainer>>();
            nearbyLootContainers.Clear();

            try
            {
                Vis.Entities(junkPile.transform.position, GetRoadsideCleanupRadius(), nearbyLootContainers, Layers.Solid);

                foreach (var nearbyLootContainer in nearbyLootContainers)
                {
                    if (nearbyLootContainer == null || nearbyLootContainer.IsDestroyed)
                    {
                        continue;
                    }

                    if (IsContainerInJunkPileGroup(nearbyLootContainer, junkPile))
                    {
                        DropItems(nearbyLootContainer);
                    }
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref nearbyLootContainers);
            }
        }

        private void RefreshContainerCaches()
        {
            _barrelShortPrefabNames.Clear();

            var configChanged = false;
            foreach (var prefab in GameManifest.Current.entities)
            {
                var lootContainer = GameManager.server.FindPrefab(prefab.ToLowerInvariant())?.GetComponent<LootContainer>();
                if (lootContainer == null || string.IsNullOrEmpty(lootContainer.ShortPrefabName))
                {
                    continue;
                }

                if (LooksLikeBarrelOrRoadsign(lootContainer.ShortPrefabName))
                {
                    _barrelShortPrefabNames.Add(lootContainer.ShortPrefabName);
                }

                if (configData.lootContainers.ContainsKey(lootContainer.ShortPrefabName))
                {
                    continue;
                }

                configData.lootContainers.Add(lootContainer.ShortPrefabName, !LooksLikeDisabledByDefault(lootContainer.ShortPrefabName));
                configChanged = true;
            }

            if (configChanged)
            {
                SaveConfig();
            }
        }

        private static bool LooksLikeBarrelOrRoadsign(string shortPrefabName)
        {
            return shortPrefabName.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) >= 0
                || shortPrefabName.IndexOf("roadsign", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeDisabledByDefault(string shortPrefabName)
        {
            return shortPrefabName.IndexOf("stocking", StringComparison.OrdinalIgnoreCase) >= 0
                || shortPrefabName.IndexOf("roadsign", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion Methods

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Time before loot containers are emptied (seconds)")]
            public float timeBeforeLootEmpty = 30f;

            [JsonProperty(PropertyName = "Empty the entire junkpile when automatically empty loot")]
            public bool emptyJunkpile = true;

            [JsonProperty(PropertyName = "Junkpile cleanup threshold")]
            public float junkPileCleanupThreshold = 0.6f;

            [JsonProperty(PropertyName = "Maximum cleanup radius for roadside groups")]
            public float maximumCleanupRadiusForRoadsideGroups = 12f;

            [JsonProperty(PropertyName = "Empty the nearby loot when emptying junkpile")]
            public bool dropNearbyLoot = false;

            [JsonProperty(PropertyName = "Time before junkpiles are emptied (seconds)")]
            public float timeBeforeJunkpileEmpty = 150f;

            [JsonProperty(PropertyName = "Slaps players who don't empty containers")]
            public bool slapPlayer = false;

            [JsonProperty(PropertyName = "Remove items instead of dropping them")]
            public bool removeItems = true;

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings chat = new ChatSettings();

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Chat Prefix")]
                public string prefix = "<color=#00FFFF>[Loot Bouncer]</color>: ";

                [JsonProperty(PropertyName = "Chat SteamID Icon")]
                public ulong steamIDIcon;
            }

            [JsonProperty(PropertyName = "Loot container settings")]
            public Dictionary<string, bool> lootContainers = new Dictionary<string, bool>(StringComparer.Ordinal);

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber version;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            var configChanged = false;
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                    configChanged = true;
                }
                else
                {
                    configChanged = UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
                configChanged = true;
            }

            if (configChanged)
            {
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData
            {
                version = Version
            };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        private bool UpdateConfigValues()
        {
            var configChanged = false;

            if (configData.chat == null)
            {
                configData.chat = new ConfigData.ChatSettings();
                configChanged = true;
            }

            if (configData.lootContainers == null)
            {
                configData.lootContainers = new Dictionary<string, bool>(StringComparer.Ordinal);
                configChanged = true;
            }

            if (configData.maximumCleanupRadiusForRoadsideGroups <= 0f)
            {
                configData.maximumCleanupRadiusForRoadsideGroups = 12f;
                configChanged = true;
            }

            if (configData.version < Version)
            {
                if (configData.version <= default(VersionNumber))
                {
                    string prefix;
                    string prefixColor;
                    if (GetConfigValue(out prefix, "Chat prefix") && GetConfigValue(out prefixColor, "Chat prefix color"))
                    {
                        configData.chat.prefix = $"<color={prefixColor}>{prefix}</color>: ";
                    }

                    ulong steamId;
                    if (GetConfigValue(out steamId, "Chat steamID icon"))
                    {
                        configData.chat.steamIDIcon = steamId;
                    }
                }

                configData.version = Version;
                configChanged = true;
            }

            return configChanged;
        }

        private bool GetConfigValue<T>(out T value, params string[] path)
        {
            var configValue = Config.Get(path);
            if (configValue == null)
            {
                value = default(T);
                return false;
            }

            value = Config.ConvertValue<T>(configValue);
            return true;
        }

        #endregion ConfigurationFile

        #region LanguageFile

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, configData.chat.prefix, configData.chat.steamIDIcon);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
            catch (Exception)
            {
                PrintError($"Error in the language formatting of '{key}'. (userid: {id}. lang: {lang.GetLanguage(id)}. args: {string.Join(" ,", args)})");
                throw;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SlapMessage"] = "You left loot behind, so the container slapped you."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SlapMessage"] = "wdnmd，不清空容器，给你个大耳刮子"
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}
