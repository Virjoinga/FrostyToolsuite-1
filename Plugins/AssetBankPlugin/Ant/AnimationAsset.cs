using AssetBankPlugin.Enums;
using AssetBankPlugin.Export;
using Frosty.Core;
using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AssetBankPlugin.Ant
{
    public class ChannelInfo
    {
        public string Name;
        public BoneChannelType Type;
        public uint DofId;
    }

    public class AnimationAsset : AntAsset
    {
        public override string Name { get; set; }
        public override Guid ID { get; set; }

        public uint CodecType;
        public uint AnimId;
        public float TrimOffset;
        public ushort EndFrame;
        public bool Additive;
        public Guid ChannelToDofAsset;
        public Guid DofSetList;

        // Backward compatible dictionary
        public Dictionary<string, BoneChannelType> Channels;
        // Ordered list preserving IndexData sequence, with DofId for default lookups
        public List<ChannelInfo> OrderedChannels;

        public float FPS;
        public StorageType StorageType;

        private RigAsset _cachedRig;
        private Dictionary<uint, Vector4> _rigDefaultVector4;
        private Dictionary<uint, Vector3> _rigDefaultVector3;
        private Dictionary<uint, float> _rigDefaultFloat;

        public AnimationAsset() { }

        private void CollectDofSetGuidsFromList(Guid listId, HashSet<Guid> guids)
        {
            var list = AntRefTable.Get(listId);
            if (list == null || list.AssetType != "DofSetListAsset") return;

            var dofSetAssets = list.GetPropertyArray<Guid>("DofSetAssets");
            if (dofSetAssets.Length > 0)
                guids.UnionWith(dofSetAssets);

            var dofSetListAssets = list.GetPropertyArray<Guid>("DofSetListAssets");
            foreach (var childId in dofSetListAssets)
                CollectDofSetGuidsFromList(childId, guids);
        }

        public override void SetData(Dictionary<string, object> data)
        {
            ParseBasicData(data);

            if (data.TryGetValue("CodecType", out object codec)) CodecType = Convert.ToUInt32(codec);
            if (data.TryGetValue("AnimId", out object animid)) unchecked { AnimId = (uint)Convert.ToInt64(animid); }
            if (data.TryGetValue("EndFrame", out object endf)) EndFrame = Convert.ToUInt16(endf);
            if (data.TryGetValue("Additive", out object additive)) Additive = Convert.ToBoolean(additive);

            if (data.TryGetValue("ChannelToDofAsset", out object dof)) ChannelToDofAsset = SafeGuid(dof);
            if (data.TryGetValue("DofSetList", out object dsl)) DofSetList = SafeGuid(dsl);
        }

        public AntAsset RecursiveHierarchySearch(AntAsset initialHierarchy, AntAsset TargetHierarchy)
        {
            if (initialHierarchy == null || TargetHierarchy == null) return null;
            if (initialHierarchy.ID == TargetHierarchy.ID) return initialHierarchy;

            var children = initialHierarchy.GetPropertyArray<Guid>("Children");
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    AntAsset asset = AntRefTable.Get(children[i]);
                    if (asset != null && asset.AssetType == "LayoutHierarchyAsset")
                    {
                        AntAsset returnAsset = RecursiveHierarchySearch(asset, TargetHierarchy);
                        if (returnAsset != null && returnAsset.ID == TargetHierarchy.ID)
                            return returnAsset;
                    }
                }
            }
            return null;
        }

        // Populates both Channels (dictionary) and OrderedChannels (list with DofId)
        public Dictionary<string, BoneChannelType> GetChannels(Guid channelToDofAsset)
        {
            ProfileVersion currentProfile = (ProfileVersion)ProfilesLibrary.DataVersion;

            AntAsset dofAssetRaw = AntRefTable.Get(channelToDofAsset);
            ChannelToDofAsset dof = dofAssetRaw as ChannelToDofAsset;
            if (dof == null)
            {
                App.Logger.LogError($"[AnimationAsset] Missing ChannelToDofAsset ({channelToDofAsset}) for '{Name}'.");
                OrderedChannels = new List<ChannelInfo>();
                return new Dictionary<string, BoneChannelType>();
            }

            StorageType = dof.StorageType;
            Guid hierarchyOrDofListId = Guid.Empty;
            var refsValues = AntRefTable.Refs.Values;

            foreach (var asset in refsValues)
            {
                if (asset is ClipControllerAsset cl)
                {
                    if (cl.ID == this.ID || cl.Anim == this.ID || (cl.Anims != null && cl.Anims.Contains(this.ID)))
                    {
                        FPS = cl.FPS;
                        hierarchyOrDofListId = cl.Target;
                        break;
                    }
                }
                else if (asset.AssetType == "ClipControllerData")
                {
                    if (asset.GetProperty<Guid>("Anim") == this.ID)
                    {
                        FPS = asset.GetProperty<float>("FPS");
                        hierarchyOrDofListId = asset.GetProperty<Guid>("Target");
                        break;
                    }
                }
            }

            if (hierarchyOrDofListId == Guid.Empty && DofSetList != Guid.Empty)
                hierarchyOrDofListId = DofSetList;

            if (hierarchyOrDofListId == Guid.Empty)
            {
                App.Logger.LogError($"[AnimationAsset] Could not find a Target Hierarchy/DofSetList for '{Name}'.");
                OrderedChannels = new List<ChannelInfo>();
                return new Dictionary<string, BoneChannelType>();
            }

            // Collect all bone names from the hierarchy
            var channelNames = new Dictionary<string, BoneChannelType>();
            var encounteredDofSets = new HashSet<Guid>();

            void ExtractSlotsFromDofSet(Guid dofSetId)
            {
                encounteredDofSets.Add(dofSetId);
                var dsaRaw = AntRefTable.Get(dofSetId);
                if (dsaRaw != null && dsaRaw.AssetType == "DofSetAsset")
                {
                    var slotsObj = dsaRaw.GetProperty<object>("Slots");
                    if (slotsObj is System.Collections.IEnumerable rawList)
                    {
                        foreach (var item in rawList)
                        {
                            string slotName = "unknown";
                            BoneChannelType slotType = BoneChannelType.None;

                            if (item is AntAsset antAsset)
                            {
                                if (antAsset.AssetType == "LayoutEntry")
                                {
                                    slotName = antAsset.Name;
                                    slotType = (BoneChannelType)antAsset.GetProperty<uint>("Type", 0);
                                }
                                else
                                {
                                    slotName = GetStringFromObj(antAsset.RawData);
                                    slotType = (BoneChannelType)antAsset.GetProperty<uint>("Type", 0);
                                }
                            }
                            else if (item is Dictionary<string, object> dict)
                            {
                                slotName = GetStringFromObj(dict);
                                if (dict.TryGetValue("Type", out object t))
                                    slotType = (BoneChannelType)Convert.ToUInt32(t);
                            }

                            channelNames[slotName] = slotType;
                        }
                    }

                    var dofAssets = dsaRaw.GetPropertyArray<Guid>("DofAssets");
                    foreach (var childId in dofAssets)
                        ExtractSlotsFromDofSet(childId);
                }
            }

            var targetHierarchyAsset = AntRefTable.Get(hierarchyOrDofListId);
            if (targetHierarchyAsset != null && targetHierarchyAsset.AssetType == "LayoutHierarchyAsset")
            {
                var layoutAssets = targetHierarchyAsset.GetPropertyArray<Guid>("LayoutAssets");
                foreach (var layoutId in layoutAssets)
                {
                    var laRaw = AntRefTable.Get(layoutId);
                    if (laRaw != null && laRaw.AssetType == "LayoutAsset")
                    {
                        var slotsObj = laRaw.GetProperty<object>("Slots");
                        if (slotsObj is System.Collections.IEnumerable rawList)
                        {
                            foreach (var item in rawList)
                            {
                                if (item is Dictionary<string, object> dict)
                                {
                                    string sName = dict.TryGetValue("Name", out object nameObj) ? SafeString(nameObj) : "unknown";
                                    BoneChannelType sType = dict.TryGetValue("Type", out object typeObj) ? (BoneChannelType)Convert.ToInt32(typeObj) : BoneChannelType.None;
                                    channelNames[sName] = sType;
                                }
                            }
                        }
                    }
                    else if (laRaw != null && laRaw.AssetType == "DofSetAsset")
                    {
                        ExtractSlotsFromDofSet(layoutId);
                    }
                    else if (laRaw?.AssetType == "DeltaTrajLayoutAsset")
                    {
                        for (int x = 0; x < 8; x++) channelNames[x.ToString()] = BoneChannelType.Rotation;
                    }
                }
            }
            else if (targetHierarchyAsset != null && targetHierarchyAsset.AssetType == "DofSetListAsset")
            {
                var dofSetAssets = targetHierarchyAsset.GetPropertyArray<Guid>("DofSetAssets");
                foreach (var dofSetId in dofSetAssets) ExtractSlotsFromDofSet(dofSetId);
            }
            else if (targetHierarchyAsset != null && targetHierarchyAsset.AssetType == "DofSetAsset")
            {
                ExtractSlotsFromDofSet(targetHierarchyAsset.ID);
            }

            uint[] data = dof.IndexData;
            int dataLength = data != null ? data.Length : 0;
            var channelNamesList = channelNames.ToArray();
            int channelNamesLength = channelNamesList.Length;

            // Automatic Rig Detection
            if (dof.rigId == Guid.Empty && encounteredDofSets.Count > 0)
            {
                foreach (var asset in refsValues)
                {
                    if (!(asset is RigAsset rig) || rig.DofIds == null || rig.DofIds.Length == 0)
                        continue;

                    HashSet<Guid> rigDofSetGuids = null;

                    // Rig directly contains the compiled DofSet list

                    if (rig.RigDofSets != null)
                    {
                        rigDofSetGuids = new HashSet<Guid>(rig.RigDofSets);
                    }

                    // Flatten referenced DofSetList assets

                    else if (rig.DofSetLists != null)
                    {
                        rigDofSetGuids = new HashSet<Guid>();
                        foreach (var dslId in rig.DofSetLists)
                            CollectDofSetGuidsFromList(dslId, rigDofSetGuids);
                    }

                    if (rigDofSetGuids != null && rigDofSetGuids.IsSupersetOf(encounteredDofSets))
                    {
                        dof.rigId = rig.ID;
                        App.Logger.Log($"[AnimationAsset] Automatic rig match: '{rig.Name}' for animation '{Name}'");
                        break;
                    }
                }
            }

            // Fallback: GW2 deep hierarchy search if no rig found via DofSet superset
            if (dof.rigId == Guid.Empty)
            {
                foreach (var asset in refsValues)
                {
                    if (!(asset is RigAsset rig) || rig.DofSetLists == null)
                        continue;

                    foreach (var dslId in rig.DofSetLists)
                    {
                        var rigLh = AntRefTable.Get(dslId);
                        if (rigLh != null && rigLh.AssetType == "LayoutHierarchyAsset" &&
                            targetHierarchyAsset != null && targetHierarchyAsset.AssetType == "LayoutHierarchyAsset")
                        {
                            if (RecursiveHierarchySearch(rigLh, targetHierarchyAsset) != null)
                            {
                                dof.rigId = rig.ID;
                                App.Logger.Log($"[AnimationAsset] Fallback rig match via hierarchy search: '{rig.Name}'");
                                break;
                            }
                        }
                    }
                    if (dof.rigId != Guid.Empty) break;
                }
            }

            // Build ordered channel list
            var ordered = new List<ChannelInfo>(dataLength);

            if (currentProfile == ProfileVersion.PlantsVsZombiesBattleforNeighborville || currentProfile == ProfileVersion.StarWarsBattlefrontII)
            {
                if (dof.rigId == Guid.Empty)
                { OrderedChannels = ordered; return new Dictionary<string, BoneChannelType>(); }
                RigAsset rig = AntRefTable.Get(dof.rigId) as RigAsset;
                if (rig == null)
                { OrderedChannels = ordered; return new Dictionary<string, BoneChannelType>(); }

                CacheRigDefaults(rig);
                var rigDofOrder = GetRigDofOrder(rig);   // Corrected mapping using DofSetIdIndices
                var dofIdToSlot = rigDofOrder.ToDictionary(k => k.DofId, v => (v.Name, v.Type));

                for (int i = 0; i < dataLength; i++)
                {
                    uint dofId = data[i];
                    if (dofIdToSlot.TryGetValue(dofId, out var slotInfo))
                        ordered.Add(new ChannelInfo { Name = slotInfo.Name, Type = slotInfo.Type, DofId = dofId });
                    else
                        ordered.Add(new ChannelInfo { Name = $"Unknown_{dofId}", Type = BoneChannelType.None, DofId = dofId });
                }
            }
            else if (currentProfile == ProfileVersion.PlantsVsZombiesGardenWarfare2)
            {
                if (dof.rigId == Guid.Empty)
                {
                    OrderedChannels = ordered;
                    return new Dictionary<string, BoneChannelType>();
                }
                RigAsset rig = AntRefTable.Get(dof.rigId) as RigAsset;
                if (rig == null || rig.DofIds == null)
                {
                    OrderedChannels = ordered;
                    return new Dictionary<string, BoneChannelType>();
                }

                var dofIdToIndex = new Dictionary<uint, int>(rig.DofIds.Length);
                for (int idx = 0; idx < rig.DofIds.Length; idx++)
                {
                    dofIdToIndex[rig.DofIds[idx]] = idx;
                }

                for (int i = 0; i < dataLength; i++)
                {
                    uint dofId = data[i];
                    if (dofIdToIndex.TryGetValue(dofId, out int idx) && idx >= 0 && idx < channelNamesLength)
                    {
                        var kv = channelNamesList[idx];
                        ordered.Add(new ChannelInfo { Name = kv.Key, Type = kv.Value, DofId = dofId });
                    }
                    else
                    {
                        ordered.Add(new ChannelInfo { Name = $"Unknown_{dofId}", Type = BoneChannelType.None, DofId = dofId });
                    }
                }
            }
            else if (currentProfile == ProfileVersion.PlantsVsZombiesGardenWarfare || StorageType == StorageType.Overwrite)
            {
                for (int i = 0; i < dataLength; i++)
                {
                    int channelId = (int)data[i];
                    if (channelId >= 0 && channelId < channelNamesLength)
                    {
                        var kv = channelNamesList[channelId];
                        ordered.Add(new ChannelInfo { Name = kv.Key, Type = kv.Value, DofId = 0 });
                    }
                }
            }
            else if (StorageType == StorageType.Append)
            {
                var offsets = new Dictionary<int, int>(dataLength / 2);
                int offset = 0;
                for (int i = 0; i < dataLength; i += 2)
                {
                    int appendTo = (int)data[i];
                    int channelId = (int)data[i + 1];
                    offsets[appendTo] = offset++;
                    int targetIndex = offsets[appendTo];
                    string key = (channelId >= 0 && channelId < channelNamesLength) ? channelNamesList[channelId].Key : "";
                    var info = new ChannelInfo { Name = key, Type = BoneChannelType.None, DofId = 0 };
                    if (targetIndex <= ordered.Count) ordered.Insert(targetIndex, info);
                    else ordered.Add(info);
                }
                // Fill types for Append
                for (int i = 0; i < ordered.Count; i++)
                    if (!string.IsNullOrEmpty(ordered[i].Name) && channelNames.TryGetValue(ordered[i].Name, out var typ))
                        ordered[i].Type = typ;
            }

            // Build the dictionary from the ordered list (for backward compatibility)
            var output = new Dictionary<string, BoneChannelType>();
            foreach (var info in ordered)
                if (!string.IsNullOrEmpty(info.Name) && !output.ContainsKey(info.Name))
                    output[info.Name] = info.Type;

            OrderedChannels = ordered;
            return output;
        }

        private List<(uint DofId, string Name, BoneChannelType Type)> GetRigDofOrder(RigAsset rig)
        {
            var result = new List<(uint, string, BoneChannelType)>();
            if (rig.DofIds == null) return result;

            var dofSetSlots = new List<Slot>[(rig.RigDofSets != null) ? rig.RigDofSets.Length : 0];
            for (int i = 0; rig.RigDofSets != null && i < rig.RigDofSets.Length; i++)
            {
                var dsa = AntRefTable.Get(rig.RigDofSets[i]);
                var slots = new List<Slot>();
                if (dsa != null && dsa.AssetType == "DofSetAsset")
                {
                    var slotsObj = dsa.GetProperty<object>("Slots");
                    if (slotsObj is System.Collections.IEnumerable rawList)
                    {
                        foreach (var item in rawList)
                        {
                            string sName = "unknown";
                            BoneChannelType sType = BoneChannelType.None;
                            if (item is AntAsset antItem)
                            {
                                sName = antItem.Name;
                                sType = (BoneChannelType)antItem.GetProperty<uint>("Type", 0);
                            }
                            else if (item is Dictionary<string, object> dict)
                            {
                                sName = GetStringFromObj(dict);
                                if (dict.TryGetValue("Type", out object t))
                                    sType = (BoneChannelType)Convert.ToUInt32(t);
                            }
                            slots.Add(new Slot { Name = sName, Type = sType });
                        }
                    }
                }
                dofSetSlots[i] = slots;
            }

            // Determine whether we have valid DofSetIdIndices (BfN) or must fall back to sequential (GW2/GW1)
            bool useIndices = rig.DofSetIdIndices != null && rig.RigDofSets != null && rig.DofSetIdIndices.Length == rig.RigDofSets.Length;

            uint sequentialOffset = 0;   // for fallback mode
            for (int i = 0; rig.RigDofSets != null && i < rig.RigDofSets.Length; i++)
            {
                // Start index into DofIds for this DofSet
                uint start = useIndices ? rig.DofSetIdIndices[i] : sequentialOffset;
                int slotCount = dofSetSlots[i].Count;

                for (int j = 0; j < slotCount; j++)
                {
                    uint dofId = rig.DofIds[start + j];
                    var slot = dofSetSlots[i][j];
                    string name = !string.IsNullOrEmpty(slot.Name) ? slot.Name : $"Unknown_{dofId}";
                    result.Add((dofId, name, slot.Type));
                }

                // In fallback mode, advance sequential offset
                if (!useIndices)
                    sequentialOffset += (uint)slotCount;
            }

            // GW1/GW2 fallback when RigDofSets is null but DofSetLists exist
            if (rig.RigDofSets == null && rig.DofSetLists != null)
            {
                var allSlots = new List<Slot>();
                foreach (var dslId in rig.DofSetLists)
                    CollectSlotsGW2(dslId, allSlots);   // keep the existing recursive helper

                for (int i = 0; i < rig.DofIds.Length && i < allSlots.Count; i++)
                {
                    uint dofId = rig.DofIds[i];
                    var slot = allSlots[i];
                    string name = !string.IsNullOrEmpty(slot.Name) ? slot.Name : $"Unknown_{dofId}";
                    result.Add((dofId, name, slot.Type));
                }
            }

            return result;
        }

        private string GetStringFromObj(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("chars", out object c)) return SafeString(c);
            if (dict.TryGetValue("Name", out object n)) return SafeString(n);
            return "unknown";
        }

        private void CollectSlotsGW2(Guid assetId, List<Slot> accumulator)
        {
            var asset = AntRefTable.Get(assetId);
            if (asset == null) return;

            if (asset.AssetType == "DofSetAsset")
            {
                var slotsObj = asset.GetProperty<object>("Slots");
                if (slotsObj is System.Collections.IEnumerable rawList)
                {
                    foreach (var item in rawList)
                    {
                        string sName = "unknown";
                        BoneChannelType sType = BoneChannelType.None;
                        if (item is AntAsset antItem)
                        {
                            sName = antItem.Name;
                            sType = (BoneChannelType)antItem.GetProperty<uint>("Type", 0);
                        }
                        else if (item is Dictionary<string, object> dict)
                        {
                            sName = GetStringFromObj(dict);
                            if (dict.TryGetValue("Type", out object t))
                                sType = (BoneChannelType)Convert.ToUInt32(t);
                        }
                        accumulator.Add(new Slot { Name = sName, Type = sType });
                    }
                }

                var dofAssets = asset.GetPropertyArray<Guid>("DofAssets");
                foreach (var childId in dofAssets)
                    CollectSlotsGW2(childId, accumulator);
            }
            else if (asset.AssetType == "DofSetListAsset")
            {
                var dofSetAssets = asset.GetPropertyArray<Guid>("DofSetAssets");
                foreach (var childId in dofSetAssets)
                    CollectSlotsGW2(childId, accumulator);
            }
            else if (asset.AssetType == "LayoutHierarchyAsset")
            {
                var layoutAssets = asset.GetPropertyArray<Guid>("LayoutAssets");
                foreach (var layoutId in layoutAssets)
                    CollectSlotsGW2(layoutId, accumulator);
            }
        }

        private void CacheRigDefaults(RigAsset rig)
        {
            if (_cachedRig == rig) return;
            _cachedRig = rig;

            _rigDefaultVector4 = new Dictionary<uint, Vector4>();
            if (rig.DefaultVector4Values != null)
                foreach (var def in rig.DefaultVector4Values)
                    if (def.Value != null && def.Value.Length >= 4)
                        _rigDefaultVector4[def.DofId] = new Vector4(def.Value[0], def.Value[1], def.Value[2], def.Value[3]);

            _rigDefaultVector3 = new Dictionary<uint, Vector3>();
            if (rig.DefaultVector3Values != null)
                foreach (var def in rig.DefaultVector3Values)
                    if (def.Value != null && def.Value.Length >= 3)
                        _rigDefaultVector3[def.DofId] = new Vector3(def.Value[0], def.Value[1], def.Value[2]);

            _rigDefaultFloat = new Dictionary<uint, float>();
            if (rig.DefaultFloatValues != null)
                foreach (var def in rig.DefaultFloatValues)
                    _rigDefaultFloat[def.DofId] = def.Value;
        }

        public bool TryGetRigDefaultVector4(uint dofId, out Vector4 value)
        {
            value = default;
            return _rigDefaultVector4 != null && _rigDefaultVector4.TryGetValue(dofId, out value);
        }
        public bool TryGetRigDefaultVector3(uint dofId, out Vector3 value)
        {
            value = default;
            return _rigDefaultVector3 != null && _rigDefaultVector3.TryGetValue(dofId, out value);
        }
        public bool TryGetRigDefaultFloat(uint dofId, out float value)
        {
            value = 0f;
            return _rigDefaultFloat != null && _rigDefaultFloat.TryGetValue(dofId, out value);
        }

        public virtual InternalAnimation ConvertToInternal()
        {
            switch (AssetType)
            {
                case "FrameAnimationAsset":
                    return ConvertFrameAnimation();
                case "CurveAnimationAsset":
                    return ConvertCurveAnimation();
                default:
                    return null;
            }
        }

        private InternalAnimation ConvertFrameAnimation()
        {
            float[] d = GetPropertyArray<float>("Data");
            if (d == null || d.Length == 0) return null;

            List<ChannelInfo> ordered = OrderedChannels;
            if (ordered == null || ordered.Count == 0) return null;

            var positions = new List<Vector3>();
            var rotations = new List<Quaternion>();
            var scales = new List<Vector3>();
            var posChannels = new List<string>();
            var rotChannels = new List<string>();
            var scaleChannels = new List<string>();
            int i = 0;

            foreach (var ch in ordered)
            {
                if (i >= d.Length) break;
                switch (ch.Type)
                {
                    case BoneChannelType.Rotation:
                        if (i + 3 < d.Length) { rotChannels.Add(ch.Name.Replace(".q", "")); rotations.Add(new Quaternion(d[i], d[i + 1], d[i + 2], d[i + 3])); }
                        i += 4; break;
                    case BoneChannelType.Position:
                        if (i + 2 < d.Length) { posChannels.Add(ch.Name.Replace(".t", "")); positions.Add(new Vector3(d[i], d[i + 1], d[i + 2])); }
                        i += 4; break;
                    case BoneChannelType.Scale:
                        if (i + 2 < d.Length) { scaleChannels.Add(ch.Name.Replace(".s", "")); scales.Add(new Vector3(d[i], d[i + 1], d[i + 2])); }
                        i += 4; break;
                    default: i++; break;
                }
            }

            var ret = new InternalAnimation { Name = Name };
            ret.Frames.Add(new Frame { FrameIndex = 0, Positions = positions, Rotations = rotations, Scales = scales });
            ret.PositionChannels = posChannels;
            ret.RotationChannels = rotChannels;
            ret.ScaleChannels = scaleChannels;
            ret.Additive = Additive;

            return ret;
        }

        private InternalAnimation ConvertCurveAnimation()
        {
            InternalAnimation ret = new InternalAnimation();

            List<string> posChannels = new List<string>();
            List<string> rotChannels = new List<string>();

            foreach (var channel in Channels)
            {
                if (channel.Value == BoneChannelType.Rotation)
                    rotChannels.Add(channel.Key.Replace(".q", ""));
                else if (channel.Value == BoneChannelType.Position)
                    posChannels.Add(channel.Key.Replace(".t", ""));
            }

            ret.Name = Name;
            ret.PositionChannels = posChannels;
            ret.RotationChannels = rotChannels;
            ret.Additive = Additive;

            return ret;
        }
    }

    public struct Slot
    {
        public string Name;
        public BoneChannelType Type;

        public override string ToString()
        {
            return Name + ", " + Type.ToString();
        }
    }

    public enum BoneChannelType
    {
        None = 0,
        Rotation = 14,
        Position = 2049856663,
        Scale = 2049856454,
    }
}