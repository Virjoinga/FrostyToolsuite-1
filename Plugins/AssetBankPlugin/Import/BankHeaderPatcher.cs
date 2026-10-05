using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetBankPlugin.Import
{
    public static class BankHeaderPatcher
    {
        private struct KeyAssetPair
        {
            public long Key;         
            public ulong Ptr;
        }

        public static byte[] PatchHeaderForKeys(byte[] bank, ulong[] newKeys, bool fallbackBigEndian, ulong[] keysToRemove = null)
        {
            if (bank == null || bank.Length < 128)
                throw new ArgumentException("Bank bytes are too small to contain a valid header.");

            int metaOffset = -1;
            bool headerBig = false;

            for (int i = 0; i <= 64; i += 4)
            {
                uint ptBE = ReadU32(bank, i + 4, true);
                if (ptBE <= 4)
                {
                    uint mb = ReadU32(bank, i, true);
                    uint ac = ReadU32(bank, i + 8, true);
                    uint ab = ReadU32(bank, i + 32, true);
                    if (mb > 0 && mb < bank.Length && ac < 500000 && ab > 0 && ab <= bank.Length)
                    {
                        metaOffset = i; headerBig = true; break;
                    }
                }

                uint ptLE = ReadU32(bank, i + 4, false);
                if (ptLE <= 4)
                {
                    uint mb = ReadU32(bank, i, false);
                    uint ac = ReadU32(bank, i + 8, false);
                    uint ab = ReadU32(bank, i + 32, false);
                    if (mb > 0 && mb < bank.Length && ac < 500000 && ab > 0 && ab <= bank.Length)
                    {
                        metaOffset = i; headerBig = false; break;
                    }
                }
            }

            if (metaOffset == -1)
                throw new Exception("Could not locate valid PackageMeta header.");

            uint originalMetaBytes = ReadU32(bank, metaOffset, headerBig);
            uint packageType = ReadU32(bank, metaOffset + 4, headerBig);
            uint assetCount = ReadU32(bank, metaOffset + 8, headerBig);
            uint exportCount = ReadU32(bank, metaOffset + 12, headerBig);
            uint importCount = ReadU32(bank, metaOffset + 16, headerBig);
            uint importEntryCount = ReadU32(bank, metaOffset + 20, headerBig);
            uint keyMapCount = ReadU32(bank, metaOffset + 24, headerBig);
            uint maxInternalResolveCount = ReadU32(bank, metaOffset + 28, headerBig);
            uint originalAssetBytes = ReadU32(bank, metaOffset + 32, headerBig);

            long pos = metaOffset + 36;

            bool tocBig = false;
            if (assetCount >= 2)
            {
                long k1_le = (long)ReadU64(bank, (int)pos, false);
                long k2_le = (long)ReadU64(bank, (int)pos + 16, false);
                long k1_be = (long)ReadU64(bank, (int)pos, true);
                long k2_be = (long)ReadU64(bank, (int)pos + 16, true);

                if (k1_le <= k2_le && k1_be > k2_be) tocBig = false;
                else if (k1_be <= k2_be && k1_le > k2_le) tocBig = true;
            }

            List<KeyAssetPair> assetKeys = new List<KeyAssetPair>((int)assetCount + newKeys.Length);
            for (int i = 0; i < assetCount; i++)
            {
                long key = (long)ReadU64(bank, (int)pos, tocBig);
                ulong ptr = ReadU64(bank, (int)pos + 8, tocBig);

                if (keysToRemove == null || !keysToRemove.Contains((ulong)key))
                {
                    assetKeys.Add(new KeyAssetPair { Key = key, Ptr = ptr });
                }
                pos += 16;
            }

            List<long> exportKeys = new List<long>((int)exportCount + newKeys.Length);
            for (int i = 0; i < exportCount; i++)
            {
                long key = (long)ReadU64(bank, (int)pos, tocBig);

                if (keysToRemove == null || !keysToRemove.Contains((ulong)key))
                {
                    exportKeys.Add(key);
                }
                pos += 8;
            }

            long importAssetsSize = (long)importCount * 16;
            byte[] importAssetsData = new byte[importAssetsSize];
            Buffer.BlockCopy(bank, (int)pos, importAssetsData, 0, (int)importAssetsSize);
            pos += importAssetsSize;

            long importEntriesSize = (long)importEntryCount * 16;
            byte[] importEntriesData = new byte[importEntriesSize];
            Buffer.BlockCopy(bank, (int)pos, importEntriesData, 0, (int)importEntriesSize);
            pos += importEntriesSize;

            long keyMapSize = (long)keyMapCount * 16;
            byte[] assetKeyMapData = new byte[keyMapSize];
            Buffer.BlockCopy(bank, (int)pos, assetKeyMapData, 0, (int)keyMapSize);
            pos += keyMapSize;

            long dataBlobStart = pos;
            long dataBlobSize = bank.Length - dataBlobStart;
            byte[] dataBlob = new byte[dataBlobSize];
            Buffer.BlockCopy(bank, (int)dataBlobStart, dataBlob, 0, (int)dataBlobSize);

            if (dataBlob.Length >= 12 &&
                dataBlob[0] == 'G' && dataBlob[1] == 'D' && dataBlob[2] == '.' &&
                dataBlob[3] == 'S' && dataBlob[4] == 'T' && dataBlob[5] == 'R' && dataBlob[6] == 'M')
            {
                bool strmBig = (dataBlob[7] == 'b');
                uint newStrmSize = (uint)dataBlobSize;
                WriteU32(dataBlob, 8, newStrmSize, strmBig);
            }

            foreach (ulong newKey in newKeys)
            {
                assetKeys.Add(new KeyAssetPair { Key = (long)newKey, Ptr = 0 });
                exportKeys.Add((long)newKey);
            }
            assetKeys.Sort((x, y) => x.Key.CompareTo(y.Key));        
            exportKeys.Sort();        

            uint newAssetCount = (uint)assetKeys.Count;
            long newAssetKeysSize = newAssetCount * 16;

            uint newExportCount = (uint)exportKeys.Count;
            long newExportSize = newExportCount * 8;

            long originalCalculatedSum = 36 + ((long)assetCount * 16) + ((long)exportCount * 8) + importAssetsSize + importEntriesSize + keyMapSize;
            long metaBytesDelta = originalMetaBytes - originalCalculatedSum;

            long newCalculatedSum = 36 + newAssetKeysSize + newExportSize + importAssetsSize + importEntriesSize + keyMapSize;
            uint newMetaBytes = (uint)(newCalculatedSum + metaBytesDelta);
            uint newAssetBytes = (uint)dataBlobSize;

            long newTotalSize = metaOffset + newMetaBytes + newAssetBytes;
            byte[] newBank = new byte[newTotalSize];

            Buffer.BlockCopy(bank, 0, newBank, 0, metaOffset);

            WriteU32(newBank, metaOffset, newMetaBytes, headerBig);
            WriteU32(newBank, metaOffset + 4, packageType, headerBig);
            WriteU32(newBank, metaOffset + 8, newAssetCount, headerBig);
            WriteU32(newBank, metaOffset + 12, newExportCount, headerBig);
            WriteU32(newBank, metaOffset + 16, importCount, headerBig);
            WriteU32(newBank, metaOffset + 20, importEntryCount, headerBig);
            WriteU32(newBank, metaOffset + 24, keyMapCount, headerBig);
            WriteU32(newBank, metaOffset + 28, maxInternalResolveCount, headerBig);
            WriteU32(newBank, metaOffset + 32, newAssetBytes, headerBig);

            int writePos = metaOffset + 36;
            foreach (var pair in assetKeys)
            {
                WriteU64(newBank, writePos, (ulong)pair.Key, tocBig);
                WriteU64(newBank, writePos + 8, pair.Ptr, tocBig);
                writePos += 16;
            }

            foreach (long eKey in exportKeys)
            {
                WriteU64(newBank, writePos, (ulong)eKey, tocBig);
                writePos += 8;
            }

            Buffer.BlockCopy(importAssetsData, 0, newBank, writePos, importAssetsData.Length);
            writePos += importAssetsData.Length;

            Buffer.BlockCopy(importEntriesData, 0, newBank, writePos, importEntriesData.Length);
            writePos += importEntriesData.Length;

            Buffer.BlockCopy(assetKeyMapData, 0, newBank, writePos, assetKeyMapData.Length);
            writePos += assetKeyMapData.Length;

            Buffer.BlockCopy(dataBlob, 0, newBank, writePos, dataBlob.Length);

            return newBank;
        }

        private static uint ReadU32(byte[] b, int o, bool big)
            => big ? (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3])
                   : (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

        private static ulong ReadU64(byte[] b, int o, bool big)
            => big ? ReadU64BE(b, o) : ReadU64LE(b, o);

        private static ulong ReadU64LE(byte[] b, int o)
            => (ulong)b[o] | ((ulong)b[o + 1] << 8) | ((ulong)b[o + 2] << 16) | ((ulong)b[o + 3] << 24)
             | ((ulong)b[o + 4] << 32) | ((ulong)b[o + 5] << 40) | ((ulong)b[o + 6] << 48) | ((ulong)b[o + 7] << 56);

        private static ulong ReadU64BE(byte[] b, int o)
            => ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40) | ((ulong)b[o + 3] << 32)
             | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16) | ((ulong)b[o + 6] << 8) | (ulong)b[o + 7];

        private static void WriteU32(byte[] b, int o, uint v, bool big)
        {
            if (big) v = Swap(v);
            for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (i * 8));
        }

        private static void WriteU64(byte[] b, int o, ulong v, bool big)
        {
            if (big) v = Swap(v);
            for (int i = 0; i < 8; i++) b[o + i] = (byte)(v >> (i * 8));
        }

        private static uint Swap(uint v) => ((v & 0xFF) << 24) | (((v >> 8) & 0xFF) << 16) | (((v >> 16) & 0xFF) << 8) | ((v >> 24) & 0xFF);
        private static ulong Swap(ulong v) => ((v & 0xFF) << 56) | (((v >> 8) & 0xFF) << 48) | (((v >> 16) & 0xFF) << 40) | (((v >> 24) & 0xFF) << 32) | (((v >> 32) & 0xFF) << 24) | (((v >> 40) & 0xFF) << 16) | (((v >> 48) & 0xFF) << 8) | ((v >> 56) & 0xFF);
    }
}
