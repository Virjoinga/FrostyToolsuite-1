using AssetBankPlugin.Ant;
using AssetBankPlugin.Enums;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Formats.GenericData2;
using FrostySdk;
using FrostySdk.IO;
using System;
using System.Collections.Generic;

namespace AssetBankPlugin.Formats
{
    public class Bank
    {
        public uint PackagingType { get; set; }
        public List<Section> Sections { get; set; } = new List<Section>();

        public Dictionary<uint, GenericClass> Classes { get; set; } = new Dictionary<uint, GenericClass>();

        public Dictionary<uint, GenericClass2> Classes2 { get; set; } = new Dictionary<uint, GenericClass2>();

        public Dictionary<string, Guid> DataNames { get; set; } = new Dictionary<string, Guid>();

        public Dictionary<Guid, ulong> OriginalHashes { get; } = new Dictionary<Guid, ulong>();

        private readonly Dictionary<string, int> _nameCounters = new Dictionary<string, int>();

        public Bank(NativeReader r, int bundleId, bool isHashMode = false)
        {
            PackagingType = r.ReadUInt(Endian.Big);

            ProcessLegacyAntRefMap(r);

            uint headerStart = (uint)r.BaseStream.Position;
            string str = r.ReadSizedString(3);
            r.BaseStream.Position = headerStart;

            if (str != "GD.")
            {
                uint headerSize = r.ReadUInt(Endian.Big);
                r.BaseStream.Position = headerStart + headerSize;
            }

            // Read section headers and metadata (REFL/REF2).
            // Data sections are bucketed here to avoid redundant iterations later.
            var data2Sections = new List<SectionData2>(8);
            var legacySections = new List<SectionData>(8);

            void Merge<T>(Dictionary<uint, T> src, Dictionary<uint, T> dst)
            {
                foreach (var kvp in src) dst[kvp.Key] = kvp.Value;
            }

            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                var section = Section.ReadSection(r);
                if (section == null) break;
                Sections.Add(section);

                if (section is SectionRefl refl) Merge(refl.Classes, Classes);
                else if (section is SectionRef2 ref2) Merge(ref2.Classes, Classes2);
                else if (section is SectionData2 d2) data2Sections.Add(d2);
                else if (section is SectionData legacy) legacySections.Add(legacy);
            }

            void HandleAsset(AntAsset asset)
            {
                if (asset == null) return;
                if (isHashMode) ProcessHashMode(asset);
                else AddAsset(asset, bundleId);
            }

            // DAT1 object deserialization.
            foreach (var legacyData in legacySections)
                HandleAsset(AntAsset.Deserialize(r, legacyData, Classes, this));

            // DAT2 Specific Object Deserialization.
            foreach (var data2Section in data2Sections)
            {
                long currentObjHeader = data2Section.DataOffset + 28;
                if (currentObjHeader + 16 <= data2Section.DataOffset + data2Section.DataSize)
                    HandleAsset(AntAsset.Deserialize(r, data2Section, Classes2, this, currentObjHeader));
            }

            if (isHashMode)
            {
                foreach (var kvp in AntRefTable.Refs)
                    if (kvp.Value.Bank == this) AntRefTable.Refs.TryRemove(kvp.Key, out _);
            }

            foreach (var section in Sections)
                if (section is SectionData2 d2) d2.ClearCaches();

            Sections.Clear();
        }

        private void ProcessLegacyAntRefMap(NativeReader r)
        {
            var version = (ProfileVersion)ProfilesLibrary.DataVersion;
            if (PackagingType != 3) return;
            if (version != ProfileVersion.PlantsVsZombiesGardenWarfare2 &&
                version != ProfileVersion.PlantsVsZombiesGardenWarfare &&
                version != ProfileVersion.Battlefield4 &&
                version != ProfileVersion.Battlefield1) return;

            r.BaseStream.Position = 56;
            uint count = r.ReadUInt(Endian.Big) / 20;
            for (int i = 0; i < count; i++)
            {
                Guid a = r.ReadGuid();
                Guid b = new Guid(r.ReadBytes(16));
                AntRefTable.InternalRefs[a] = b;
                Cache.AntRefMap[a] = b;
            }
            r.BaseStream.Position = 4;
        }

        private void AddAsset(AntAsset asset, int bundleId)
        {
            string baseName = asset.Name;

            _nameCounters.TryGetValue(baseName, out int count);

            string name = count == 0
                ? baseName
                : string.Concat(baseName, " [", count.ToString(), "]");

            _nameCounters[baseName] = count + 1;

            DataNames[name] = asset.ID;
            Cache.AntStateBundleIndices[asset.ID] = bundleId;
        }

        private void ProcessHashMode(AntAsset asset)
        {
            OriginalHashes[asset.ID] = ComputeAssetHash(asset);
        }

        private static readonly HashSet<string> VolatileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Offset", "Size", "__offset", "__size"
        };

        public static ulong ComputeAssetHash(AntAsset asset)
        {
            if (asset?.RawData == null) return 0;
            return ComputeHash(asset.RawData);
        }

        private static ulong ComputeHash(object obj)
        {
            switch (obj)
            {
                case null: return 0;
                case string str: return (ulong)str.GetHashCode();
                case Dictionary<string, object> dict:
                    ulong hd = 0;
                    foreach (var kvp in dict)
                        if (!VolatileKeys.Contains(kvp.Key)) hd ^= ((ulong)kvp.Key.GetHashCode() * 397) ^ ComputeHash(kvp.Value);
                    return hd;
                case byte[] bArr:
                    ulong hb = 14695981039346656037;
                    for (int i = 0; i < bArr.Length; i++) { hb ^= bArr[i]; hb *= 1099511628211; }
                    return hb;
                case uint[] uiArr:
                    ulong hu = 14695981039346656037;
                    for (int i = 0; i < uiArr.Length; i++) { hu ^= uiArr[i]; hu *= 1099511628211; }
                    return hu;
                case float[] fArr:
                    ulong hf = 14695981039346656037;
                    for (int i = 0; i < fArr.Length; i++) { hf ^= (ulong)fArr[i].GetHashCode(); hf *= 1099511628211; }
                    return hf;
                case object[] objArr:
                    ulong ho = 17;
                    for (int i = 0; i < objArr.Length; i++) ho = ho * 31 + ComputeHash(objArr[i]);
                    return ho;
                case System.Collections.IEnumerable enumerable:
                    ulong he = 17;
                    foreach (var item in enumerable) he = he * 31 + ComputeHash(item);
                    return he;
                default:
                    return (ulong)obj.GetHashCode();
            }
        }
    }
}
