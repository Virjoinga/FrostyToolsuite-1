using AssetBankPlugin.Extensions;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetBankPlugin.Formats.GenericData2
{
    public class SectionData2 : Section
    {
        public const string Identifier = "GD.DAT2";
        public override Endian Endianness { get; set; }
        public override uint DataSize { get; set; }
        public override uint DataOffset { get; set; }

        public uint ObjectCount { get; private set; }

        private readonly byte[] _patchedData;
        private readonly NativeReader _patchedReader;

        private readonly bool _isBigEndian;

        private readonly Dictionary<int, string> _stringCache = new Dictionary<int, string>();
        private readonly Dictionary<long, Dictionary<string, object>> _objectCache = new Dictionary<long, Dictionary<string, object>>();

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatUnion { [FieldOffset(0)] public uint UInt; [FieldOffset(0)] public float Float; }

        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleUnion { [FieldOffset(0)] public ulong ULong; [FieldOffset(0)] public double Double; }

        public void ClearCaches()
        {
            _stringCache.Clear();
            _objectCache.Clear();
        }
        public SectionData2(NativeReader r, Endian bankEndian)
        {
            long startPos = r.BaseStream.Position;
            DataOffset = (uint)startPos;

            string magic = r.ReadSizedString(7);
            _ = r.ReadByte();
            Endianness = bankEndian;
            _isBigEndian = bankEndian == Endian.Big;
            DataSize = r.ReadUInt(Endianness);

            long magicConstant = r.ReadLong(Endianness);
            ObjectCount = (uint)r.ReadULong(Endianness);

            r.BaseStream.Position = startPos;
            _patchedData = r.ReadBytes((int)DataSize);

            _patchedReader = new NativeReader(new MemoryStream(_patchedData), null);
            r.BaseStream.Position = startPos + DataSize;
        }

        public NativeReader GetPatchedReader() => _patchedReader;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint R_U32(int o)
        {
            var d = _patchedData;
            return _isBigEndian
                ? (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3])
                : (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int R_I32(int o)
        {
            var d = _patchedData;
            return _isBigEndian
                ? (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]
                : d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong R_U64(int o)
        {
            var d = _patchedData;
            return _isBigEndian
                ? ((ulong)d[o] << 56) | ((ulong)d[o + 1] << 48) | ((ulong)d[o + 2] << 40) | ((ulong)d[o + 3] << 32) | ((ulong)d[o + 4] << 24) | ((ulong)d[o + 5] << 16) | ((ulong)d[o + 6] << 8) | d[o + 7]
                : (ulong)d[o] | ((ulong)d[o + 1] << 8) | ((ulong)d[o + 2] << 16) | ((ulong)d[o + 3] << 24) | ((ulong)d[o + 4] << 32) | ((ulong)d[o + 5] << 40) | ((ulong)d[o + 6] << 48) | ((ulong)d[o + 7] << 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long R_I64(int o)
        {
            var d = _patchedData;
            return _isBigEndian
                ? ((long)d[o] << 56) | ((long)d[o + 1] << 48) | ((long)d[o + 2] << 40) | ((long)d[o + 3] << 32) | ((long)d[o + 4] << 24) | ((long)d[o + 5] << 16) | ((long)d[o + 6] << 8) | d[o + 7]
                : (long)d[o] | ((long)d[o + 1] << 8) | ((long)d[o + 2] << 16) | ((long)d[o + 3] << 24) | ((long)d[o + 4] << 32) | ((long)d[o + 5] << 40) | ((long)d[o + 6] << 48) | ((long)d[o + 7] << 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort R_U16(int o)
        {
            var d = _patchedData;
            return _isBigEndian
                ? (ushort)((d[o] << 8) | d[o + 1])
                : (ushort)(d[o] | (d[o + 1] << 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short R_I16(int o)
        {
            var d = _patchedData;
            return _isBigEndian
                ? (short)((d[o] << 8) | d[o + 1])
                : (short)(d[o] | (d[o + 1] << 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float R_F32(int o) => new FloatUnion { UInt = R_U32(o) }.Float;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double R_F64(int o) => new DoubleUnion { ULong = R_U64(o) }.Double;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Guid R_Guid(int o)
        {
            var d = _patchedData;
            return new Guid((int)R_U32(o), R_I16(o + 4), R_I16(o + 6), d[o + 8], d[o + 9], d[o + 10], d[o + 11], d[o + 12], d[o + 13], d[o + 14], d[o + 15]);
        }

        private string R_NullStr(int o)
        {
            if (o < 0 || o >= _patchedData.Length) return "";

            if (_stringCache.TryGetValue(o, out string cachedStr)) return cachedStr;

            int end = o;
            int bufLen = _patchedData.Length;
            while (end < bufLen && _patchedData[end] != 0) end++;

            string s = Encoding.UTF8.GetString(_patchedData, o, end - o);
            _stringCache[o] = s;
            return s;
        }

        public Dictionary<string, object> ReadObjectAt(long headerFileOffset, Dictionary<uint, GenericClass2> classes)
        {
            if (_objectCache.TryGetValue(headerFileOffset, out var cachedLayout))
                return cachedLayout;

            int bufOff = (int)(headerFileOffset - DataOffset);
            if (bufOff < 0 || bufOff + 16 > _patchedData.Length) return null;

            // Reading type hash robustly from the ulong alignment
            ulong typeHash64 = R_U64(bufOff);
            uint typeHash = (uint)(typeHash64 & 0xFFFFFFFF);

            if (!classes.TryGetValue(typeHash, out GenericClass2 layout)) return null;

            var result = ReadValues(bufOff + 16, layout, classes);
            result["__typeHash"] = typeHash;

            _objectCache[headerFileOffset] = result;
            return result;
        }

        private Dictionary<string, object> ReadValues(int bufOff, GenericClass2 layout, Dictionary<uint, GenericClass2> classes)
        {
            var result = new Dictionary<string, object>(layout.Elements.Count);

            foreach (var field in layout.Elements)
            {
                int fieldOff = bufOff + field.Offset;

                if (field.IsList || field.Type == "String")
                    result[field.Name] = ReadHeapItem(fieldOff, field, classes);
                else if (field.InlineCount > 1)
                    result[field.Name] = ReadArray(fieldOff, field, field.InlineCount, classes);
                else
                    result[field.Name] = ReadPrimitive(fieldOff, field, classes);
            }
            return result;
        }

        private object ReadHeapItem(int bufOff, GenericField2 field, Dictionary<uint, GenericClass2> classes)
        {
            int count = R_I32(bufOff + 4);
            long rawVal = R_I64(bufOff + 8);

            if (rawVal == 0 || rawVal == -1 || count <= 0)
                return field.Type == "String" ? (object)"" : new object[0];

            long adjustedRawVal = rawVal << 4 >> 4;
            long absoluteTarget = DataOffset + bufOff + 8 + adjustedRawVal;

            if (absoluteTarget < DataOffset || absoluteTarget > DataOffset + DataSize)
                return field.Type == "String" ? (object)"" : new object[0];

            int targetOff = (int)(absoluteTarget - DataOffset);

            if (field.Type == "String") return R_NullStr(targetOff);

            if (field.ElementTypeHash != 0 && field.ElementSize > 0)
            {
                var elemField = new GenericField2
                {
                    TypeHash = field.ElementTypeHash,
                    Size = field.ElementSize,
                    Alignment = field.ElementAlignment
                };

                if (PrimitiveTypeMap.IsPrimitive(elemField.TypeHash))
                    elemField.Type = PrimitiveTypeMap.GetTypeName(elemField.TypeHash);
                else if (classes.TryGetValue(elemField.TypeHash, out GenericClass2 elemClass))
                    elemField.Type = elemClass.Name;
                else
                    elemField.Type = $"Class_0x{elemField.TypeHash:X8}";

                return ReadArray(targetOff, elemField, count, classes);
            }

            return ReadArray(targetOff, field, count, classes);
        }

        private object ReadPrimitive(int bufOff, GenericField2 field, Dictionary<uint, GenericClass2> classes)
        {
            if (bufOff < 0 || bufOff >= _patchedData.Length) return null;

            switch (field.TypeHash)
            {
                case 0x01: return _patchedData[bufOff] != 0;
                case 0x02: return (sbyte)_patchedData[bufOff];
                case 0x03: return _patchedData[bufOff];
                case 0x04: return R_I16(bufOff);
                case 0x05: return R_U16(bufOff);
                case 0x06: return R_I32(bufOff);
                case 0x07: return R_U32(bufOff);
                case 0x08: return R_I64(bufOff);
                case 0x09: return R_U64(bufOff);
                case 0x0A: return R_F32(bufOff);
                case 0x13: return R_F64(bufOff);
                case 0x23: return R_U64(bufOff).ToString("X16");

                case 0x0B: return new float[] { R_F32(bufOff), R_F32(bufOff + 4) };
                case 0x0C: return new float[] { R_F32(bufOff), R_F32(bufOff + 4), R_F32(bufOff + 8) };
                case 0x0D:
                case 0x0E: return new float[] { R_F32(bufOff), R_F32(bufOff + 4), R_F32(bufOff + 8), R_F32(bufOff + 12) };

                case 0x0F:
                    float[] m = new float[16];
                    for (int i = 0; i < 16; i++) m[i] = R_F32(bufOff + i * 4);
                    return m;

                case 0x12: // DataRef
                    {
                        long pVal = R_I64(bufOff);
                        if (pVal == 0 || pVal == -1) return null;
                        long pPos = DataOffset + bufOff;
                        return ReadObjectAt(pPos + (pVal << 4 >> 4) + 16, classes);
                    }

                default:
                    if (field.Type == "Guid") return R_Guid(bufOff);

                    if (field.Size == 8 && field.Alignment == 8 && !field.IsNative)
                    {
                        long refVal = R_I64(bufOff);
                        if (refVal == 0 || refVal == -1) return null;

                        long refPos = DataOffset + bufOff;
                        long target = refPos + (refVal << 4 >> 4);

                        if (field.Type == "String" || field.ElementTypeHash == 0x11)
                        {
                            int tOff = (int)(target - DataOffset);
                            uint cnt = R_U32(tOff + 4);
                            long ptr = R_I64(tOff + 8);
                            if (ptr != 0 && ptr != -1 && cnt > 0)
                            {
                                long strTarget = target + 8 + (ptr << 4 >> 4);
                                return R_NullStr((int)(strTarget - DataOffset));
                            }
                            return "";
                        }

                        return ReadObjectAt(target + 16, classes);
                    }
                    else
                    {
                        if (!classes.TryGetValue(field.TypeHash, out GenericClass2 nestedLayout)) return null;
                        return ReadValues(bufOff, nestedLayout, classes);
                    }
            }
        }

        private object ReadArray(int bufOff, GenericField2 field, int count, Dictionary<uint, GenericClass2> classes)
        {
            if (count <= 0 || count > 5_000_000) return new object[0];

            bool isReference = field.Size == 8 && field.Alignment == 8
                            && !PrimitiveTypeMap.IsPrimitive(field.TypeHash)
                            && field.TypeHash != 0;

            bool isStringArray = field.Type == "String[]"
                              || (field.ElementTypeHash != 0 && field.ElementTypeHash == 0x11);

            uint elemSize = field.Size;
            uint elemAlign = field.Alignment;

            if (elemSize == 0 && field.TypeHash != 0 && !PrimitiveTypeMap.IsPrimitive(field.TypeHash))
            {
                if (classes.TryGetValue(field.TypeHash, out GenericClass2 classLayout))
                {
                    elemSize = (uint)classLayout.Size;
                    elemAlign = (uint)classLayout.Alignment;
                }
            }

            if (elemSize == 0) elemSize = 1;
            if (elemAlign == 0) elemAlign = 1;

            uint stride = (elemSize + elemAlign - 1) & ~(elemAlign - 1);

            if (!isReference && !isStringArray && PrimitiveTypeMap.IsPrimitive(field.TypeHash))
            {
                bool canBlockCopy = !_isBigEndian && (stride == elemSize);
                int intStride = (int)stride;

                switch (field.TypeHash)
                {
                    case 0x01:  
                        var bArr = new bool[count];
                        for (int i = 0; i < count; i++) bArr[i] = _patchedData[bufOff + i * intStride] != 0;
                        return bArr;
                    case 0x02:  
                        var sbArr = new sbyte[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, sbArr, 0, count);
                        else for (int i = 0; i < count; i++) sbArr[i] = (sbyte)_patchedData[bufOff + i * intStride];
                        return sbArr;
                    case 0x03:  
                        var byArr = new byte[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, byArr, 0, count);
                        else for (int i = 0; i < count; i++) byArr[i] = _patchedData[bufOff + i * intStride];
                        return byArr;
                    case 0x04:  
                        var sArr = new short[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, sArr, 0, count * 2);
                        else for (int i = 0; i < count; i++) sArr[i] = R_I16(bufOff + i * intStride);
                        return sArr;
                    case 0x05:  
                        var usArr = new ushort[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, usArr, 0, count * 2);
                        else for (int i = 0; i < count; i++) usArr[i] = R_U16(bufOff + i * intStride);
                        return usArr;
                    case 0x06:  
                        var iArr = new int[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, iArr, 0, count * 4);
                        else for (int i = 0; i < count; i++) iArr[i] = R_I32(bufOff + i * intStride);
                        return iArr;
                    case 0x07:  
                        var uiArr = new uint[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, uiArr, 0, count * 4);
                        else for (int i = 0; i < count; i++) uiArr[i] = R_U32(bufOff + i * intStride);
                        return uiArr;
                    case 0x08:  
                        var lArr = new long[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, lArr, 0, count * 8);
                        else for (int i = 0; i < count; i++) lArr[i] = R_I64(bufOff + i * intStride);
                        return lArr;
                    case 0x09:  
                        var ulArr = new ulong[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, ulArr, 0, count * 8);
                        else for (int i = 0; i < count; i++) ulArr[i] = R_U64(bufOff + i * intStride);
                        return ulArr;
                    case 0x0A:  
                        var fArr = new float[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, fArr, 0, count * 4);
                        else for (int i = 0; i < count; i++) fArr[i] = R_F32(bufOff + i * intStride);
                        return fArr;
                    case 0x13:  
                        var dArr = new double[count];
                        if (canBlockCopy) Buffer.BlockCopy(_patchedData, bufOff, dArr, 0, count * 8);
                        else for (int i = 0; i < count; i++) dArr[i] = R_F64(bufOff + i * intStride);
                        return dArr;
                }
            }

            if (isStringArray)
            {
                var strArr = new string[count];
                for (int i = 0; i < count; i++)
                {
                    int offset = (int)(i * stride);
                    long refVal = R_I64(bufOff + offset);

                    if (refVal == 0 || refVal == -1)
                    {
                        strArr[i] = "";
                        continue;
                    }

                    long refPos = DataOffset + bufOff + offset;
                    long target = refPos + (refVal << 4 >> 4);
                    int tOff = (int)(target - DataOffset);
                    uint cnt = R_U32(tOff + 4);
                    long ptr = R_I64(tOff + 8);

                    if (ptr != 0 && ptr != -1 && cnt > 0)
                    {
                        long strTarget = target + 8 + (ptr << 4 >> 4);
                        strArr[i] = R_NullStr((int)(strTarget - DataOffset));
                    }
                    else
                    {
                        strArr[i] = "";
                    }
                }
                return strArr;
            }

            var arr = new object[count];
            for (int i = 0; i < count; i++)
            {
                int offset = (int)(i * stride);

                if (isReference)
                {
                    long refVal = R_I64(bufOff + offset);

                    if (refVal == 0 || refVal == -1)
                    {
                        arr[i] = null;
                        continue;
                    }

                    long refPos = DataOffset + bufOff + offset;
                    long target = refPos + (refVal << 4 >> 4);
                    arr[i] = ReadObjectAt(target + 16, classes);
                }
                else
                {
                    arr[i] = ReadPrimitive(bufOff + offset, field, classes);
                }
            }
            return arr;
        }
    }
}
