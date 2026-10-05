using AssetBankPlugin.Ant;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Formats.GenericData2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetBankPlugin.Import
{
    public static class DynamicDat2Serializer
    {
        private const ulong kIID = 0x030E6205UL;

        private class SerializationContext
        {
            public bool BigEndian { get; }
            public Dictionary<uint, GenericClass2> Classes { get; }
            public MemoryStream Stream { get; } = new MemoryStream();
            public List<int> LayoutDataOffsets { get; } = new List<int>();
            public List<PendingPointer> PendingPointers { get; } = new List<PendingPointer>();
            public Dictionary<object, int> SerializedAddresses { get; } = new Dictionary<object, int>();

            public Dictionary<object, int> ObjectRefCounts { get; } = new Dictionary<object, int>();

            public SerializationContext(bool bigEndian, Dictionary<uint, GenericClass2> classes)
            {
                BigEndian = bigEndian;
                Classes = classes;
            }

            public void RegisterReference(object target)
            {
                if (target == null) return;
                if (!ObjectRefCounts.ContainsKey(target))
                    ObjectRefCounts[target] = 0;
                ObjectRefCounts[target]++;
            }
        }

        private class PendingPointer
        {
            public int SourceOffset { get; set; }
            public object Target { get; set; }
            public int Alignment { get; set; }
            public bool IsLayoutData { get; set; }
            public uint TypeHash { get; set; }
        }

        private class StringPayload
        {
            public string Value { get; }
            public StringPayload(string v) { Value = v; }
        }

        private class PrimitiveArrayPayload
        {
            public System.Collections.IEnumerable List { get; }
            public uint ElementTypeHash { get; }
            public uint ElementSize { get; }
            public uint ElementAlignment { get; }

            public PrimitiveArrayPayload(System.Collections.IEnumerable list, uint typeHash, uint size, uint align)
            {
                List = list; ElementTypeHash = typeHash; ElementSize = size; ElementAlignment = align;
            }
        }

        private class InlineStructArrayPayload
        {
            public System.Collections.IEnumerable List { get; }
            public uint ElementTypeHash { get; }
            public uint ElementSize { get; }
            public uint ElementAlignment { get; }

            public InlineStructArrayPayload(System.Collections.IEnumerable list, uint typeHash, uint size, uint align)
            {
                List = list; ElementTypeHash = typeHash; ElementSize = size; ElementAlignment = align;
            }
        }

        private class StringArrayPayload
        {
            public List<string> Strings { get; }
            public StringArrayPayload(List<string> strings) { Strings = strings; }
        }

        private class ReferenceObjectArrayPayload
        {
            public System.Collections.IEnumerable List { get; }
            public uint ElementTypeHash { get; }
            public ReferenceObjectArrayPayload(System.Collections.IEnumerable list, uint typeHash)
            {
                List = list; ElementTypeHash = typeHash;
            }
        }

        public static byte[] Serialize(AntAsset asset, Dictionary<uint, GenericClass2> classes, bool bigEndian)
        {
            var ctx = new SerializationContext(bigEndian, classes);
            uint typeHash = GetTypeHashForAsset(asset, classes);

            byte[] tagHeader = new byte[12];
            ctx.Stream.Write(tagHeader, 0, 12);

            ctx.PendingPointers.Add(new PendingPointer
            {
                SourceOffset = -1,
                Target = asset,
                Alignment = 16,
                IsLayoutData = true,
                TypeHash = typeHash
            });

            int pIndex = 0;
            while (pIndex < ctx.PendingPointers.Count)
            {
                var pending = ctx.PendingPointers[pIndex];
                pIndex++;

                object target = pending.Target;

                if (ctx.SerializedAddresses.TryGetValue(target, out int targetAddr))
                {
                    if (pending.SourceOffset != -1)
                        PatchRelativePointer(ctx.Stream, pending.SourceOffset, targetAddr, bigEndian);
                    continue;
                }

                int alignedPos = AlignUp((int)ctx.Stream.Length - 12, pending.Alignment) + 12;
                ctx.Stream.SetLength(alignedPos);
                ctx.Stream.Position = alignedPos;

                ctx.SerializedAddresses[target] = alignedPos;

                if (pending.SourceOffset != -1)
                    PatchRelativePointer(ctx.Stream, pending.SourceOffset, alignedPos, bigEndian);

                if (pending.IsLayoutData)
                {
                    ctx.LayoutDataOffsets.Add(alignedPos);

                    uint actualTypeHash = pending.TypeHash;
                    if (actualTypeHash == 0x12 || actualTypeHash == 0)
                    {
                        if (target is Dictionary<string, object> dict && dict.TryGetValue("__typeHash", out object th))
                            actualTypeHash = Convert.ToUInt32(th);
                        else if (target is AntAsset ast)
                            actualTypeHash = GetTypeHashForAsset(ast, ctx.Classes);
                    }

                    uint refCount = 0;
                    if (target != asset && ctx.ObjectRefCounts.TryGetValue(target, out int rCount))
                        refCount = (uint)rCount;

                    WriteLayoutDataHeader(ctx.Stream, actualTypeHash, refCount, bigEndian);
                    int fieldDataStart = alignedPos + 32;
                    SerializeObject(target, actualTypeHash, fieldDataStart, ctx);
                }
                else
                {
                    if (target is StringPayload sp)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(sp.Value);
                        ctx.Stream.Write(bytes, 0, bytes.Length);
                        ctx.Stream.WriteByte(0);
                    }
                    else if (target is PrimitiveArrayPayload pap) SerializePrimitiveArray(pap, ctx);
                    else if (target is InlineStructArrayPayload isap) SerializeInlineStructArray(isap, ctx);
                    else if (target is StringArrayPayload sap) SerializeStringArray(sap, ctx);
                    else if (target is ReferenceObjectArrayPayload roap) SerializeReferenceObjectArray(roap, ctx);
                }
            }

            return BuildDAT2Section(ctx.Stream.ToArray(), ctx.LayoutDataOffsets, bigEndian);
        }

        private static void SerializeObject(object obj, uint typeHash, int startOffset, SerializationContext ctx)
        {
            if (!ctx.Classes.TryGetValue(typeHash, out var layout))
                throw new Exception($"Layout for type hash 0x{typeHash:X8} not found in this bank.");

            byte[] fieldData = new byte[layout.Size];

            foreach (var field in layout.Elements)
            {
                object val = GetFieldValue(obj, field.Name);
                int fieldOff = field.Offset;

                if (field.IsList)
                {
                    int count = val != null ? GetCountOfVal(val) : 0;
                    if (count > 0)
                    {
                        WriteU32(fieldData, fieldOff, (uint)count, ctx.BigEndian);
                        WriteU32(fieldData, fieldOff + 4, (uint)count, ctx.BigEndian);

                        object payload;
                        if (field.ElementTypeHash == 0x11 || field.Type == "String[]")
                        {
                            var stringList = new List<string>();
                            foreach (var s in (System.Collections.IEnumerable)val) stringList.Add(s?.ToString() ?? "");
                            payload = new StringArrayPayload(stringList);
                        }
                        else if (PrimitiveTypeMap.IsPrimitive(field.ElementTypeHash) && field.ElementTypeHash != 0x12)
                        {
                            payload = new PrimitiveArrayPayload((System.Collections.IEnumerable)val, field.ElementTypeHash, field.ElementSize, field.ElementAlignment);
                        }
                        else if (IsReferenceType(field, ctx.Classes))
                        {
                            payload = new ReferenceObjectArrayPayload((System.Collections.IEnumerable)val, field.ElementTypeHash);
                        }
                        else
                        {
                            uint elemSize = field.ElementSize;
                            uint elemAlign = field.ElementAlignment;
                            if (ctx.Classes.TryGetValue(field.ElementTypeHash, out var elemClass))
                            {
                                if (elemSize == 0) elemSize = (uint)elemClass.Size;
                                if (elemAlign == 0) elemAlign = (uint)elemClass.Alignment;
                            }
                            if (elemSize == 0) elemSize = 8;
                            if (elemAlign == 0) elemAlign = 4;

                            payload = new InlineStructArrayPayload((System.Collections.IEnumerable)val, field.ElementTypeHash, elemSize, elemAlign);
                        }

                        ctx.PendingPointers.Add(new PendingPointer
                        {
                            SourceOffset = startOffset + fieldOff + 8,
                            Target = payload,
                            Alignment = (int)(field.ElementAlignment > 0 ? field.ElementAlignment : 8),
                            IsLayoutData = false
                        });
                    }
                    else
                    {
                        WriteU32(fieldData, fieldOff, 0, ctx.BigEndian);
                        WriteU32(fieldData, fieldOff + 4, 0, ctx.BigEndian);
                        WriteU64(fieldData, fieldOff + 8, 0, ctx.BigEndian);
                    }
                }
                else if (field.Type == "String")
                {
                    if (val != null && val.ToString().Length > 0)
                    {
                        string s = val.ToString();
                        int count = s.Length + 1;
                        WriteU32(fieldData, fieldOff, (uint)count, ctx.BigEndian);
                        WriteU32(fieldData, fieldOff + 4, (uint)count, ctx.BigEndian);

                        ctx.PendingPointers.Add(new PendingPointer
                        {
                            SourceOffset = startOffset + fieldOff + 8,
                            Target = new StringPayload(s),
                            Alignment = 1,
                            IsLayoutData = false
                        });
                    }
                    else
                    {
                        WriteU32(fieldData, fieldOff, 0, ctx.BigEndian);
                        WriteU32(fieldData, fieldOff + 4, 0, ctx.BigEndian);
                        WriteU64(fieldData, fieldOff + 8, 0, ctx.BigEndian);
                    }
                }
                else if (field.InlineCount > 1) SerializeInlineArray(fieldData, fieldOff, val, field, startOffset + fieldOff, ctx);
                else SerializePrimitiveOrStruct(fieldData, fieldOff, val, field, startOffset + fieldOff, ctx);
            }

            ctx.Stream.Position = startOffset;
            ctx.Stream.Write(fieldData, 0, fieldData.Length);
        }

        private static bool IsReferenceType(GenericField2 field, Dictionary<uint, GenericClass2> classes)
        {
            if (field.ElementTypeHash == 0x12) return true;
            if (field.ElementSize == 8 && field.ElementAlignment == 8 && !field.IsNative)
            {
                if (classes.TryGetValue(field.ElementTypeHash, out var cls))
                {
                    if (cls.Elements.Count > 1 || (cls.Elements.Count == 1 && PrimitiveTypeMap.IsPrimitive(cls.Elements[0].TypeHash)))
                        return false;
                }
                return true;
            }
            return false;
        }

        private static void SerializePrimitiveOrStruct(byte[] fieldData, int fieldOff, object val, GenericField2 field, int absoluteOffset, SerializationContext ctx)
        {
            if (PrimitiveTypeMap.IsPrimitive(field.TypeHash) && field.TypeHash != 0x12)
            {
                WritePrimitiveValue(fieldData, fieldOff, val, field.TypeHash, ctx.BigEndian);
            }
            else if (field.Type == "Guid")
            {
                Guid g = SafeGuid(val);
                byte[] bytes = g.ToByteArray();
                if (ctx.BigEndian)
                {
                    Array.Reverse(bytes, 0, 4); Array.Reverse(bytes, 4, 2); Array.Reverse(bytes, 6, 2);
                }
                Buffer.BlockCopy(bytes, 0, fieldData, fieldOff, 16);
            }
            else if (field.TypeHash == 0x12 || (field.Size == 8 && field.Alignment == 8 && !field.IsNative))
            {
                if (val != null)
                {
                    ctx.RegisterReference(val);        
                    ctx.PendingPointers.Add(new PendingPointer
                    {
                        SourceOffset = absoluteOffset,
                        Target = val,
                        Alignment = 16,
                        IsLayoutData = true,
                        TypeHash = field.TypeHash
                    });
                }
                else WriteU64(fieldData, fieldOff, 0, ctx.BigEndian);
            }
            else if (val != null)
            {
                SerializeObject(val, field.TypeHash, absoluteOffset, ctx);
            }
        }

        private static void SerializeInlineArray(byte[] fieldData, int fieldOff, object val, GenericField2 field, int absoluteOffset, SerializationContext ctx)
        {
            if (val is System.Collections.IEnumerable list)
            {
                var elements = new List<object>();
                foreach (var item in list) elements.Add(item);

                uint stride = field.ElementSize;
                if (stride == 0)
                {
                    if (ctx.Classes.TryGetValue(field.ElementTypeHash, out var elemClass)) stride = (uint)elemClass.Size;
                    else stride = 8;
                }
                int align = (int)field.ElementAlignment;
                if (align == 0) align = 4;
                stride = (uint)((stride + align - 1) & ~(align - 1));

                for (int i = 0; i < field.InlineCount && i < elements.Count; i++)
                {
                    int elemOff = fieldOff + (int)(i * stride);

                    if (PrimitiveTypeMap.IsPrimitive(field.ElementTypeHash) && field.ElementTypeHash != 0x12)
                    {
                        WritePrimitiveValue(fieldData, elemOff, elements[i], field.ElementTypeHash, ctx.BigEndian);
                    }
                    else if (field.ElementTypeHash == 0x12 || (field.ElementSize == 8 && field.ElementAlignment == 8 && !field.IsNative))
                    {
                        if (elements[i] != null)
                        {
                            ctx.RegisterReference(elements[i]);
                            ctx.PendingPointers.Add(new PendingPointer
                            {
                                SourceOffset = absoluteOffset + elemOff,
                                Target = elements[i],
                                Alignment = 16,
                                IsLayoutData = true,
                                TypeHash = field.ElementTypeHash
                            });
                        }
                        else WriteU64(fieldData, elemOff, 0, ctx.BigEndian);
                    }
                    else
                    {
                        SerializeObject(elements[i], field.ElementTypeHash, absoluteOffset + elemOff, ctx);
                    }
                }
            }
        }

        private static void SerializePrimitiveArray(PrimitiveArrayPayload pap, SerializationContext ctx)
        {
            var elements = new List<object>();
            foreach (var item in pap.List) elements.Add(item);

            uint stride = pap.ElementSize;
            int align = (int)pap.ElementAlignment;
            if (align == 0) align = 4;
            stride = (uint)((stride + align - 1) & ~(align - 1));

            byte[] buffer = new byte[elements.Count * stride];
            for (int i = 0; i < elements.Count; i++)
                WritePrimitiveValue(buffer, (int)(i * stride), elements[i], pap.ElementTypeHash, ctx.BigEndian);

            ctx.Stream.Write(buffer, 0, buffer.Length);
        }

        private static void SerializeInlineStructArray(InlineStructArrayPayload isap, SerializationContext ctx)
        {
            var elements = new List<object>();
            foreach (var item in isap.List) elements.Add(item);

            uint stride = isap.ElementSize;
            int align = (int)isap.ElementAlignment;
            if (align == 0) align = 4;
            stride = (uint)((stride + align - 1) & ~(align - 1));

            int startOffset = (int)ctx.Stream.Position;
            int totalBytes = elements.Count * (int)stride;

            byte[] padding = new byte[totalBytes];
            ctx.Stream.Write(padding, 0, totalBytes);

            for (int i = 0; i < elements.Count; i++)
            {
                object elem = elements[i];
                if (elem != null)
                {
                    int elemOffset = startOffset + (i * (int)stride);
                    SerializeObject(elem, isap.ElementTypeHash, elemOffset, ctx);
                }
            }
        }

        private static void SerializeStringArray(StringArrayPayload sap, SerializationContext ctx)
        {
            var stringList = sap.Strings;
            long startPos = ctx.Stream.Position;

            for (int i = 0; i < stringList.Count; i++)
            {
                string s = stringList[i];
                int count = s.Length + 1;
                int elemOff = (int)(startPos + (i * 16));

                byte[] container = new byte[16];
                WriteU32(container, 0, (uint)count, ctx.BigEndian);
                WriteU32(container, 4, (uint)count, ctx.BigEndian);

                ctx.PendingPointers.Add(new PendingPointer
                {
                    SourceOffset = elemOff + 8,
                    Target = new StringPayload(s),
                    Alignment = 1,
                    IsLayoutData = false
                });

                ctx.Stream.Write(container, 0, 16);
            }
        }

        private static void SerializeReferenceObjectArray(ReferenceObjectArrayPayload roap, SerializationContext ctx)
        {
            var elements = new List<object>();
            foreach (var item in roap.List) elements.Add(item);

            long startPos = ctx.Stream.Position;
            for (int i = 0; i < elements.Count; i++)
            {
                object elem = elements[i];
                if (elem != null)
                {
                    ctx.RegisterReference(elem);
                    ctx.PendingPointers.Add(new PendingPointer
                    {
                        SourceOffset = (int)(startPos + (i * 8)),
                        Target = elem,
                        Alignment = 16,
                        IsLayoutData = true,
                        TypeHash = roap.ElementTypeHash
                    });
                }
                WriteU64ToStream(ctx.Stream, 0, ctx.BigEndian);
            }
        }

        public static uint GetTypeHashForAsset(AntAsset asset, Dictionary<uint, GenericClass2> classes)
        {
            foreach (var kvp in classes)
                if (kvp.Value.Name == asset.AssetType) return kvp.Key;

            if (asset.AssetType == "ClipControllerAsset") return 0x7DAF6AAC;
            if (asset.AssetType == "ActorControllerAsset") return 0xBA67EA60;
            throw new Exception($"Unknown asset layout type: {asset.AssetType}");
        }

        private static object GetFieldValue(object obj, string fieldName)
        {
            if (obj is AntAsset asset && asset.RawData != null && asset.RawData.TryGetValue(fieldName, out object dVal))
                return dVal;

            if (obj is Dictionary<string, object> dict && dict.TryGetValue(fieldName, out object dictVal))
                return dictVal;

            var prop = obj.GetType().GetProperty(fieldName);
            if (prop != null) return prop.GetValue(obj);

            var field = obj.GetType().GetField(fieldName);
            if (field != null) return field.GetValue(obj);

            return null;
        }

        private static int GetCountOfVal(object val)
        {
            if (val is string s) return s.Length > 0 ? s.Length + 1 : 0;
            if (val is System.Collections.ICollection c) return c.Count;
            if (val is System.Collections.IEnumerable e)
            {
                int count = 0;
                foreach (var _ in e) count++;
                return count;
            }
            return 0;
        }

        private static Guid SafeGuid(object obj)
        {
            if (obj == null) return Guid.Empty;
            if (obj is Guid g) return g;
            if (obj is string s && Guid.TryParse(s, out Guid parsed)) return parsed;
            return Guid.Empty;
        }

        private static void PatchRelativePointer(MemoryStream ms, int sourceOffset, int targetOffset, bool bigEndian)
        {
            long delta = targetOffset - sourceOffset;
            long encoded = (delta & 0x0FFFFFFFFFFFFFFF) | (1L << 60);

            long originalPos = ms.Position;
            ms.Position = sourceOffset;

            byte[] bytes = BitConverter.GetBytes(encoded);
            if (bigEndian) Array.Reverse(bytes);

            ms.Write(bytes, 0, 8);
            ms.Position = originalPos;
        }

        private static void WriteLayoutDataHeader(MemoryStream ms, uint typeHash, uint refCount, bool bigEndian)
        {
            byte[] header = new byte[32];
            WriteU64(header, 0, kIID, bigEndian);
            WriteU64(header, 8, 0, bigEndian);
            WriteU64(header, 16, (ulong)typeHash, bigEndian);
            WriteU32(header, 24, refCount, bigEndian);       
            WriteU16(header, 28, 32, bigEndian);
            WriteU16(header, 30, 0, bigEndian);
            ms.Write(header, 0, 32);
        }

        private static byte[] BuildDAT2Section(byte[] rawPayload, List<int> layoutDataOffsets, bool bigEndian)
        {
            int blockCount = layoutDataOffsets.Count;
            int footerSize = (blockCount * 8) + 2;
            int totalSize = rawPayload.Length + footerSize;

            byte[] finalBytes = new byte[totalSize];
            Buffer.BlockCopy(rawPayload, 0, finalBytes, 0, rawPayload.Length);

            finalBytes[0] = (byte)'G'; finalBytes[1] = (byte)'D'; finalBytes[2] = (byte)'.';
            finalBytes[3] = (byte)'D'; finalBytes[4] = (byte)'A'; finalBytes[5] = (byte)'T';
            finalBytes[6] = (byte)'2';
            finalBytes[7] = bigEndian ? (byte)'b' : (byte)'l';

            WriteU32(finalBytes, 8, (uint)totalSize, bigEndian);

            int footerOffset = rawPayload.Length;
            for (int i = 0; i < blockCount; i++)
            {
                int lAddr = layoutDataOffsets[i];
                int pOff = footerOffset + (i * 8);
                long delta = lAddr - pOff;
                long encoded = (delta & 0x0FFFFFFFFFFFFFFF) | (1L << 60);
                WriteU64(finalBytes, pOff, (ulong)encoded, bigEndian);
            }

            WriteU16(finalBytes, footerOffset + (blockCount * 8), (ushort)blockCount, bigEndian);
            return finalBytes;
        }

        private static int AlignUp(int v, int a) => a <= 1 ? v : (v + a - 1) & ~(a - 1);

        private static void WriteU64(byte[] b, int o, ulong v, bool bigEndian) { if (bigEndian) v = Swap(v); for (int i = 0; i < 8; i++) b[o + i] = (byte)(v >> (i * 8)); }
        private static void WriteU32(byte[] b, int o, uint v, bool bigEndian) { if (bigEndian) v = Swap(v); for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (i * 8)); }
        private static void WriteU16(byte[] b, int o, ushort v, bool bigEndian) { if (bigEndian) v = Swap(v); b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
        private static void WriteU64ToStream(MemoryStream ms, ulong v, bool bigEndian) { byte[] b = new byte[8]; WriteU64(b, 0, v, bigEndian); ms.Write(b, 0, 8); }

        private static ulong Swap(ulong v) => ((v & 0xFF) << 56) | (((v >> 8) & 0xFF) << 48) | (((v >> 16) & 0xFF) << 40) | (((v >> 24) & 0xFF) << 32) | (((v >> 32) & 0xFF) << 24) | (((v >> 40) & 0xFF) << 16) | (((v >> 48) & 0xFF) << 8) | ((v >> 56) & 0xFF);
        private static uint Swap(uint v) => ((v & 0xFF) << 24) | (((v >> 8) & 0xFF) << 16) | (((v >> 16) & 0xFF) << 8) | ((v >> 24) & 0xFF);
        private static ushort Swap(ushort v) => (ushort)(((v & 0xFF) << 8) | ((v >> 8) & 0xFF));

        private static ulong GuidToKey(Guid g) => BitConverter.ToUInt64(g.ToByteArray(), 0);

        private static void WritePrimitiveValue(byte[] b, int o, object val, uint typeHash, bool bigEndian)
        {
            if (val == null) return;
            switch (typeHash)
            {
                case 0x01: b[o] = Convert.ToBoolean(val) ? (byte)1 : (byte)0; break;
                case 0x02: b[o] = (byte)Convert.ToSByte(val); break;
                case 0x03: b[o] = Convert.ToByte(val); break;
                case 0x04: WriteU16(b, o, (ushort)Convert.ToInt16(val), bigEndian); break;
                case 0x05: WriteU16(b, o, Convert.ToUInt16(val), bigEndian); break;
                case 0x06: WriteU32(b, o, (uint)Convert.ToInt32(val), bigEndian); break;
                case 0x07: WriteU32(b, o, Convert.ToUInt32(val), bigEndian); break;
                case 0x08: WriteU64(b, o, (ulong)Convert.ToInt64(val), bigEndian); break;
                case 0x09: WriteU64(b, o, Convert.ToUInt64(val), bigEndian); break;
                case 0x0A: WriteU32(b, o, BitConverter.ToUInt32(BitConverter.GetBytes(Convert.ToSingle(val)), 0), bigEndian); break;
                case 0x13: WriteU64(b, o, BitConverter.ToUInt32(BitConverter.GetBytes(Convert.ToDouble(val)), 0), bigEndian); break;
                case 0x23:
                    ulong keyVal = 0;
                    if (val is Guid g) keyVal = GuidToKey(g);
                    else if (val is string s)
                    {
                        if (Guid.TryParse(s, out Guid parsedG)) keyVal = GuidToKey(parsedG);
                        else if (ulong.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedHex)) keyVal = parsedHex;
                        else ulong.TryParse(s, out keyVal);
                    }
                    else keyVal = Convert.ToUInt64(val);
                    WriteU64(b, o, keyVal, bigEndian);
                    break;
                case 0x0B:
                case 0x0C:
                case 0x0D:
                case 0x0E:
                    float[] arr = (float[])val;
                    for (int i = 0; i < arr.Length; i++) WriteU32(b, o + (i * 4), BitConverter.ToUInt32(BitConverter.GetBytes(arr[i]), 0), bigEndian);
                    break;
                case 0x0F:
                    float[] arrMat = (float[])val;
                    for (int i = 0; i < 16 && i < arrMat.Length; i++) WriteU32(b, o + (i * 4), BitConverter.ToUInt32(BitConverter.GetBytes(arrMat[i]), 0), bigEndian);
                    break;
            }
        }
    }
}
