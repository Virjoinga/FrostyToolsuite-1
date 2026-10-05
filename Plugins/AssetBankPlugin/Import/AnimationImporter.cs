using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Assimp;
using AssetBankPlugin.Ant;
using AssetBankPlugin.Ant.VBR;
using AssetBankPlugin.Formats.GenericData2;
using Frosty.Core;

namespace AssetBankPlugin.Import
{
    public static class AnimationImporter
    {
        public static byte[] Import(
            Scene scene,
            byte[] bankBytes,
            VbrAnimationAsset template,
            Dictionary<uint, GenericClass2> classes,
            bool bigEndian,
            float maxRotErrPct = 0f,
            float maxTransErrPct = 0f,
            float maxTrajErrPct = 0f)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (bankBytes == null || bankBytes.Length == 0) throw new ArgumentNullException(nameof(bankBytes));
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (classes == null) throw new ArgumentNullException(nameof(classes));

            if (!scene.HasAnimations)
                throw new InvalidOperationException("The imported file contains no animations.");

            float quality = Config.Get<float>("AnimationImportQuality", 1.0f, ConfigScope.Game);

            // Map: Quality 1.0 is 0.0f Error (Max Quality)
            //      Quality 0.0 is 1.0f Error (Min Quality) (lol)
            float errorPct = Math.Max(0f, Math.Min(1f, 1.0f - quality));

            VbrAnimationAsset newAsset = VbrCompressor.Compress(
                scene, template, bigEndian,
                errorPct, errorPct, errorPct);

            return ProcessSplicing(newAsset, bankBytes, template, classes, bigEndian);
        }
        private static byte[] ProcessSplicing(
            VbrAnimationAsset newAsset,
            byte[] bankBytes,
            VbrAnimationAsset template,
            Dictionary<uint, GenericClass2> classes,
            bool bigEndian)
        {
            newAsset.Name = template.Name;
            newAsset.ID = template.ID;

            if (newAsset.RawData == null)
                newAsset.RawData = new Dictionary<string, object>();

            newAsset.RawData["__name"] = template.Name;
            newAsset.RawData["__guid"] = template.ID;
            newAsset.RawData["__key"] = GuidToKey(template.ID);

            byte[] newSection = DynamicDat2Serializer.Serialize(newAsset, classes, bigEndian);

            uint typeHash = DynamicDat2Serializer.GetTypeHashForAsset(newAsset, classes);
            ulong assetKey = GuidToKey(template.ID);

            int keyFieldOffset = 16;
            if (classes.TryGetValue(typeHash, out GenericClass2 layout))
            {
                var keyField = layout.Elements.FirstOrDefault(
                    f => f.Name == "__key" || f.Name == "__guid");
                if (keyField != null) keyFieldOffset = keyField.Offset;
            }

            int secStart = FindDat2SectionStart(
                bankBytes, typeHash, assetKey, keyFieldOffset, bigEndian);

            if (secStart < 0)
                throw new InvalidOperationException(
                    $"Cannot locate the binary section for '{template.Name}' (type 0x{typeHash:X8}) " +
                    "in the bank. Confirm the duplicate was saved before importing.");

            byte[] splicedBank = SpliceSection(bankBytes, secStart, newSection, bigEndian);
            byte[] patchedBank = BankHeaderPatcher.PatchHeaderForKeys(splicedBank, Array.Empty<ulong>(), bigEndian);

            template.Data = newAsset.Data;
            template.ConstantPalette = newAsset.ConstantPalette;
            template.FrameBlockSizes = newAsset.FrameBlockSizes;
            template.PaletteIndexes = newAsset.PaletteIndexes;
            template.ConstChanMap = newAsset.ConstChanMap;
            template.QuaternionCount = newAsset.QuaternionCount;
            template.Vector3Count = newAsset.Vector3Count;
            template.NumFloat = newAsset.NumFloat;
            template.ConstQuaternionCount = newAsset.ConstQuaternionCount;
            template.ConstVector3Count = newAsset.ConstVector3Count;
            template.ConstFloatCount = newAsset.ConstFloatCount;
            template.QuatMin = newAsset.QuatMin;
            template.QuatMax = newAsset.QuatMax;
            template.TrajMin = newAsset.TrajMin;
            template.TrajMax = newAsset.TrajMax;
            template.Vec3Min = newAsset.Vec3Min;
            template.Vec3Max = newAsset.Vec3Max;
            template.FloatMin = newAsset.FloatMin;
            template.FloatMax = newAsset.FloatMax;
            template.Dct = newAsset.Dct;
            template.KeyTimeSize = newAsset.KeyTimeSize;
            template.ConstChanMapSize = newAsset.ConstChanMapSize;
            template.ConstPaletteSize = newAsset.ConstPaletteSize;
            template.Flags = newAsset.Flags;
            template.EndFrame = newAsset.EndFrame;
            template.NumKeys = newAsset.NumKeys;

            if (newAsset.RawData != null)
            {
                template.RawData = new Dictionary<string, object>(newAsset.RawData);
                template.RawData["__name"] = template.Name;
                template.RawData["__guid"] = template.ID;
                template.RawData["__key"] = GuidToKey(template.ID);

                template.SetData(template.RawData);
            }

            if (template.FrameBlockSizes == null)
                template.FrameBlockSizes = newAsset.FrameBlockSizes ?? new ushort[0];

            var decompCacheField = typeof(VbrAnimationAsset).GetField(
                "DecompressedData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (decompCacheField != null)
            {
                var cacheList = decompCacheField.GetValue(template) as List<Vector4>;
                cacheList?.Clear();
            }

            return patchedBank;
        }

        private static ulong GuidToKey(Guid g)
            => BitConverter.ToUInt64(g.ToByteArray(), 0);

        private static int FindDat2SectionStart(
            byte[] bank, uint typeHash, ulong assetKey, int keyFieldOffset, bool big)
        {
            int keyAbsOff = 44 + keyFieldOffset;

            int pos = 0;
            while (pos + 12 <= bank.Length)
            {
                if (bank[pos] == 'G' && bank[pos + 1] == 'D' && bank[pos + 2] == '.' &&
                    bank[pos + 3] == 'D' && bank[pos + 4] == 'A' && bank[pos + 5] == 'T' &&
                    bank[pos + 6] == '2')
                {
                    uint sz = ReadU32(bank, pos + 8, big);

                    if (pos + 32 <= bank.Length)
                    {
                        uint secHash = ReadU32(bank, pos + 28, big);
                        if (secHash == typeHash && pos + keyAbsOff + 8 <= bank.Length &&
                            ReadU64(bank, pos + keyAbsOff, big) == assetKey)
                            return pos;
                    }

                    pos += (int)Math.Max(sz, 1u);
                }
                else
                {
                    pos++;
                }
            }

            return -1;
        }

        private static byte[] SpliceSection(
            byte[] bank, int sectionStart, byte[] newSection, bool big)
        {
            int oldSz = (int)ReadU32(bank, sectionStart + 8, big);
            int tail = bank.Length - sectionStart - oldSz;

            byte[] result = new byte[bank.Length - oldSz + newSection.Length];
            Buffer.BlockCopy(bank, 0, result, 0, sectionStart);
            Buffer.BlockCopy(newSection, 0, result, sectionStart, newSection.Length);
            Buffer.BlockCopy(bank, sectionStart + oldSz, result, sectionStart + newSection.Length, tail);
            return result;
        }

        private static uint ReadU32(byte[] b, int o, bool big)
            => big
                ? (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3])
                : (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

        private static ulong ReadU64(byte[] b, int o, bool big)
            => big ? ReadU64BE(b, o) : ReadU64LE(b, o);

        private static ulong ReadU64LE(byte[] b, int o)
            => (ulong)b[o]
             | ((ulong)b[o + 1] << 8) | ((ulong)b[o + 2] << 16) | ((ulong)b[o + 3] << 24)
             | ((ulong)b[o + 4] << 32) | ((ulong)b[o + 5] << 40) | ((ulong)b[o + 6] << 48)
             | ((ulong)b[o + 7] << 56);

        private static ulong ReadU64BE(byte[] b, int o)
            => ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40)
             | ((ulong)b[o + 3] << 32) | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16)
             | ((ulong)b[o + 6] << 8) | (ulong)b[o + 7];
    }
}
