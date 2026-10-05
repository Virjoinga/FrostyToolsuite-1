using AssetBankPlugin.Extensions;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Formats.GenericData;
using AssetBankPlugin.Formats.GenericData2;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AssetBankPlugin.Ant
{
    public abstract class AntAsset
    {
        public abstract string Name { get; set; }
        public abstract Guid ID { get; set; }

        public string AssetType { get; set; } = "Unknown";

        public Bank Bank { get; set; }
        public Dictionary<string, object> RawData { get; set; }

        public abstract void SetData(Dictionary<string, object> data);

        public T GetProperty<T>(string propertyName, T defaultValue = default)
        {
            if (RawData == null || !RawData.TryGetValue(propertyName, out object val) || val == null)
                return defaultValue;

            if (val is T exact)
                return exact;

            try
            {
                if (typeof(T) == typeof(Guid))
                    return (T)(object)SafeGuid(val);

                if (typeof(T) == typeof(string))
                    return (T)(object)SafeString(val);

                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public T[] GetPropertyArray<T>(string propertyName)
        {
            if (RawData == null || !RawData.TryGetValue(propertyName, out object val) || val == null)
                return Array.Empty<T>();

            return ConvertArray<T>(val);
        }

        protected Guid SafeGuid(object obj)
        {
            if (obj == null) return Guid.Empty;
            if (obj is Guid g) return g;
            if (obj is string s)
            {
                if (s.Length <= 16 && ulong.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out ulong val))
                {
                    byte[] guidBytes = new byte[16];
                    BitConverter.GetBytes(val).CopyTo(guidBytes, 0);
                    return new Guid(guidBytes);
                }
                if (Guid.TryParse(s, out Guid result)) return result;
            }
            return Guid.Empty;
        }

        protected string SafeString(object obj)
        {
            if (obj == null) return "unknown";
            if (obj is string s) return s;

            try
            {
                if (obj is byte[] b)
                    return System.Text.Encoding.ASCII.GetString(b).TrimEnd('\0');

                if (obj is object[] objArr)
                {
                    byte[] bytes = new byte[objArr.Length];
                    for (int i = 0; i < objArr.Length; i++)
                        bytes[i] = Convert.ToByte(objArr[i]);
                    return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
                }
            }
            catch
            {
                return "unknown_format";
            }

            return obj.ToString();
        }

        protected T[] ConvertArray<T>(object inObj)
        {
            if (inObj == null) return Array.Empty<T>();
            if (inObj is T[] exact) return exact;

            if (inObj is object[] objArr)
            {
                T[] ret = new T[objArr.Length];
                for (int i = 0; i < objArr.Length; i++)
                {
                    if (objArr[i] is T exactItem) ret[i] = exactItem;
                    else if (typeof(T) == typeof(Guid)) ret[i] = (T)(object)SafeGuid(objArr[i]);
                    else ret[i] = (T)Convert.ChangeType(objArr[i], typeof(T));
                }
                return ret;
            }

            if (inObj is System.Collections.IEnumerable ie)
            {
                var lst = (inObj is System.Collections.ICollection ic)
                    ? new List<T>(ic.Count)
                    : new List<T>();

                foreach (var val in ie)
                {
                    if (val is T exactItem) lst.Add(exactItem);
                    else if (typeof(T) == typeof(Guid)) lst.Add((T)(object)SafeGuid(val));
                    else lst.Add((T)Convert.ChangeType(val, typeof(T)));
                }
                return lst.ToArray();
            }

            return Array.Empty<T>();
        }

        protected void ParseBasicData(Dictionary<string, object> data)
        {
            if (data.TryGetValue("__name", out object n)) Name = n.ToString();
            else Name = "Unknown_Asset";

            if (data.TryGetValue("__guid", out object g)) ID = SafeGuid(g);
            else if (data.TryGetValue("__key", out object k)) ID = SafeGuid(k);

            if (ID == Guid.Empty) ID = Guid.NewGuid();
        }

        private static readonly Dictionary<string, Func<AntAsset>> s_activatorCache = new Dictionary<string, Func<AntAsset>>();

        public static AntAsset Deserialize(NativeReader r, SectionData section, Dictionary<uint, GenericClass> classes, Bank bank)
        {
            r.BaseStream.Position = section.DataOffset;
            r.ReadDataHeader(section.Endianness, out uint hash, out uint type, out uint offset);

            var values = section.ReadValues(r, classes, section.DataOffset + offset, type);

            if (values.TryGetValue("__base", out object baseVal) && (long)baseVal != 0)
            {
                r.BaseStream.Position = section.DataOffset + (long)baseVal;
                r.ReadDataHeader(section.Endianness, out uint base_hash, out uint base_type, out uint base_offset);

                var baseValues = section.ReadValues(r, classes, section.DataOffset + base_offset + Convert.ToUInt32(baseVal), base_type);

                string baseTypeName = classes[base_type].Name;
                AntAsset baseAsset = CreateAndRegisterAsset(baseTypeName, baseValues, bank);

                values["__base"] = baseAsset;

                foreach (var value in baseValues)
                {
                    if (!values.ContainsKey(value.Key)) values.Add(value.Key, value.Value);
                }
            }

            return CreateAndRegisterAsset(classes[type].Name, values, bank);
        }

        public static AntAsset Deserialize(NativeReader r, SectionData2 section, Dictionary<uint, GenericClass2> classes, Bank bank, long objectHeaderOffset)
        {
            var values = section.ReadObjectAt(objectHeaderOffset, classes);
            if (values == null) return null;

            var patchedReader = section.GetPatchedReader();
            patchedReader.BaseStream.Position = objectHeaderOffset - section.DataOffset;
            uint typeHash = patchedReader.ReadUInt(section.Endianness);

            if (!classes.TryGetValue(typeHash, out var layout)) return null;

            return CreateAndRegisterAsset(layout.Name, values, bank);
        }

        private static AntAsset CreateAndRegisterAsset(string typeName, Dictionary<string, object> values, Bank bank)
        {
            AntAsset asset = null;

            if (!s_activatorCache.TryGetValue(typeName, out Func<AntAsset> activator))
            {
                Type assetType = Type.GetType("AssetBankPlugin.Ant." + typeName);

                if (assetType == null && (typeName.EndsWith("AnimationAsset") || typeName.EndsWith("AnimAsset")))
                {
                    assetType = typeof(AnimationAsset);
                }

                if (assetType != null)
                {
                    var newExp = Expression.New(assetType);
                    var lambda = Expression.Lambda<Func<AntAsset>>(newExp);
                    activator = lambda.Compile();
                }
                s_activatorCache[typeName] = activator;
            }

            if (activator != null)
            {
                try
                {
                    asset = activator();
                    asset.AssetType = typeName;
                    asset.Bank = bank;
                    asset.RawData = values;
                    asset.SetData(values);
                    AntRefTable.Add(asset);
                    return asset;
                }
                catch { }
            }

            // Generic fallback for non animation data assets
            asset = new GenericAntAsset();
            asset.AssetType = typeName;
            asset.Bank = bank;
            asset.RawData = values;
            asset.SetData(values);
            AntRefTable.Add(asset);
            return asset;
        }
    }

    public class GenericAntAsset : AntAsset
    {
        public override string Name { get; set; } = "Unknown_Asset";
        public override Guid ID { get; set; } = Guid.Empty;

        public override void SetData(Dictionary<string, object> data)
        {
            ParseBasicData(data);
        }
    }
}
