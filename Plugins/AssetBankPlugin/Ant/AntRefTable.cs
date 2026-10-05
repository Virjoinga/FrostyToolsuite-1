using Frosty.Core;
using System;
using System.Collections.Concurrent;

namespace AssetBankPlugin.Ant
{
    public class AntRefTable
    {
        // ConcurrentDictionary because Add() is called from multiple concurrent
        // bundle-loading tasks. Plain Dictionary under concurrent writes corrupts
        // its internal chain pointers, forcing expensive ntdll heap recovery.
        public static ConcurrentDictionary<Guid, Guid> InternalRefs = new ConcurrentDictionary<Guid, Guid>();
        public static ConcurrentDictionary<Guid, AntAsset> Refs = new ConcurrentDictionary<Guid, AntAsset>();

        public static void Add(AntAsset asset)
        {
            Refs[asset.ID] = asset;
        }

        // Allow the editor to wipe the cache on close
        public static void Clear()
        {
            InternalRefs.Clear();
            Refs.Clear();
        }

        public static AntAsset Get(Guid refId, bool recurse = false)
        {
            if (refId == Guid.Empty) return null;
            if (Refs.TryGetValue(refId, out var asset)) return asset;
            if (InternalRefs.TryGetValue(refId, out var internalId))
            {
                Refs.TryGetValue(internalId, out var internalAsset);
                return internalAsset; // null if still not loaded, caller handles it
            }
            if (recurse) return null;

            if (!Cache.AntStateBundleIndices.TryGetValue(refId, out int bundleId))
            {
                if (!Cache.AntRefMap.TryGetValue(refId, out var mappedId) ||
                    !Cache.AntStateBundleIndices.TryGetValue(mappedId, out bundleId))
                    return null; // Unknown GUID - not a critical error, caller will handle null.
            }

            AntStateAssetDefinition.LoadAntStateFromBundle(App.AssetManager.GetBundleEntry(bundleId));
            return Get(refId, true);
        }
    }
}