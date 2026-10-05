using AssetBankPlugin.Ant;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Import;
using Assimp;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;

namespace AssetBankPlugin
{
    public partial class AntStateAssetEditor
    {
        public byte[] BankBytes => _bankBytes;

        private bool _isBankBytesDirty = false;

        public bool BankIsBigEndian => _bankIsBigEndian;

        public void UpdateBankBytes(byte[] newBytes)
        {
            _bankBytes = newBytes;
            try
            {
                if (_bankResEntry != null)
                    App.AssetManager.ModifyRes(_bankResEntry.Name, _bankBytes);
                else if (_bankChunkEntry != null)
                    App.AssetManager.ModifyChunk(_bankChunkEntry.Id, _bankBytes);
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor] Failed to save updated bank bytes: {ex.Message}");
            }
        }

        private static Dictionary<Guid, AntAsset> BuildAnimToControllerMap()
        {
            var map = new Dictionary<Guid, AntAsset>();
            foreach (var asset in AntRefTable.Refs.Values)
            {
                if (asset.AssetType == "ClipControllerAsset")
                {
                    var animVal = asset.GetProperty<Guid>("Anim");
                    var animsVal = asset.GetPropertyArray<Guid>("Anims");
                    if (animVal != Guid.Empty) map[animVal] = asset;
                    if (animsVal != null)
                        foreach (var g in animsVal)
                            if (g != Guid.Empty) map[g] = asset;
                }
                else if (asset.AssetType == "ClipControllerData")
                {
                    var animVal = asset.GetProperty<Guid>("Anim");
                    if (animVal != Guid.Empty) map[animVal] = asset;
                }
            }
            return map;
        }

        private const uint ClipControllerTypeHash = 0x7DAF6AAC;
        private const uint ActorControllerTypeHash = 0xBA67EA60;
        private const uint SequenceAnimTrackTypeHash = 0x4A29AFED;
        private const uint SequenceAnimationTypeHash = 0x58BA350F;

        private void ModifyResWithFastCompression(ResAssetEntry entry, byte[] data, CompressionType compression = CompressionType.None)
        {
            byte[] compressed = Utils.CompressFile(data, compressionOverride: compression);

            if (entry.ModifiedEntry == null)
                entry.ModifiedEntry = new ModifiedAssetEntry();

            entry.ModifiedEntry.Data = compressed;
            entry.ModifiedEntry.OriginalSize = data.Length;
            entry.ModifiedEntry.Sha1 = ComputeSha1(compressed);
            entry.IsDirty = true;
        }

        private static Sha1 ComputeSha1(byte[] buffer)
        {
            using (var sha = System.Security.Cryptography.SHA1.Create())
                return new Sha1(sha.ComputeHash(buffer));
        }

        private void SaveAllUnsaved()
        {
            if (_bankBytes == null)
            {
                FrostyMessageBox.Show("Bank not loaded.", "Save");
                return;
            }

            // Main UI Thread
            List<AntAssetViewModel> unsavedVms = null;
            List<AntAsset> unsavedAssets = null;
            bool hasChangesToSave = false;

            Application.Current.Dispatcher.Invoke(() =>
            {
                unsavedVms = _masterGroups.SelectMany(g => g.Assets)
                                          .Where(vm => vm.IsUnsaved)
                                          .ToList();

                hasChangesToSave = unsavedVms.Count > 0 || _isBankBytesDirty || AssetModified;

                if (hasChangesToSave)
                {
                    unsavedAssets = new List<AntAsset>(unsavedVms.Count);
                    foreach (var vm in unsavedVms)
                    {
                        if (vm.AssetInstance is AntAsset asset)
                        {
                            if (vm.IsModified)
                            {
                                ApplyUiEditsToAsset(vm);
                            }
                            unsavedAssets.Add(asset);
                        }
                    }
                }
            });

            if (!hasChangesToSave)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    FrostyMessageBox.Show("No unsaved changes.", "Save"));
                return;
            }

            FrostyTaskWindow.Show("Saving Assets", "", (task) =>
            {
                bool big = _bankIsBigEndian;
                var classes = _bank.Classes2;
                int savedCount = 0;
                int total = unsavedAssets.Count;
                var succeededVms = new List<AntAssetViewModel>(total);

                for (int i = 0; i < total; i++)
                {
                    var vm = unsavedVms[i];
                    var asset = unsavedAssets[i];

                    if (task != null && total > 0)
                    {
                        double progress = ((double)i / total) * 80.0;
                        task.Update($"Serializing {asset.Name} ({i + 1}/{total})...", progress);
                    }

                    try
                    {
                        byte[] newSection = DynamicDat2Serializer.Serialize(asset, classes, big);
                        uint typeHash = DynamicDat2Serializer.GetTypeHashForAsset(asset, classes);
                        ulong assetKey = GuidToKey(asset.ID);

                        int keyOffset = 16;
                        if (classes.TryGetValue(typeHash, out var layout))
                        {
                            var keyField = layout.Elements.FirstOrDefault(f => f.Name == "__key" || f.Name == "__guid");
                            if (keyField != null) keyOffset = keyField.Offset;
                        }

                        int secStart = FindDat2SectionStart(_bankBytes, typeHash, assetKey, keyFieldOffset: keyOffset, big);
                        if (secStart < 0)
                        {
                            App.Logger.LogError($"[AntStateEditor] Could not locate binary for '{asset.Name}' (0x{typeHash:X8}).");
                            continue;
                        }

                        _bankBytes = SpliceSection(_bankBytes, secStart, newSection, big);
                        savedCount++;

                        succeededVms.Add(vm);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogError($"[AntStateEditor] Serialize failed for '{asset.Name}': {ex.Message}\n{ex.StackTrace}");
                        Application.Current.Dispatcher.Invoke(() =>
                            FrostyMessageBox.Show($"Failed to serialize '{asset.Name}': {ex.Message}", "Save Failed"));
                    }
                }

                if (task != null)
                {
                    task.Update("Compressing and writing bank bytes...", 90.0);
                }

                try
                {
                    var opt = new AnimationOptions();
                    opt.Load();
                    CompressionType selectedCompression = opt.GetSelectedCompressionType();

                    if (_bankResEntry != null)
                        ModifyResWithFastCompression(_bankResEntry, _bankBytes, selectedCompression);
                    else if (_bankChunkEntry != null)
                        App.AssetManager.ModifyChunk(_bankChunkEntry.Id, _bankBytes, selectedCompression);
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"[AntStateEditor] Failed to write bank bytes: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() =>
                        FrostyMessageBox.Show($"Failed to write bank: {ex.Message}", "Save Failed"));
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var vm in succeededVms)
                    {
                        vm.IsUnsaved = false;
                    }

                    if (AssetEntry != null)
                    {
                        AssetEntry.IsDirty = true;

                        if (_bankResEntry != null)
                        {
                            _bankResEntry.IsDirty = true;
                            AssetEntry.LinkAsset(_bankResEntry);
                        }
                        if (_bankChunkEntry != null)
                        {
                            _bankChunkEntry.IsDirty = true;
                            AssetEntry.LinkAsset(_bankChunkEntry);
                        }
                    }

                    _isBankBytesDirty = false;
                    AssetModified = true;
                });

                if (savedCount > 0)
                    App.Logger.Log($"[AntStateEditor] Saved {savedCount} asset(s) to bank.");
                else
                    App.Logger.Log($"[AntStateEditor] Bank updated successfully.");
            });
        }

        private void ApplyUiEditsToAsset(AntAssetViewModel assetVm)
        {
            var asset = (AntAsset)assetVm.AssetInstance;
            if (asset.RawData == null)
                asset.RawData = new Dictionary<string, object>();

            foreach (var prop in assetVm.Properties)
            {
                CommitPropertyEditsRecursive(prop, asset.RawData);
            }

            asset.SetData(asset.RawData);
        }

        private void CommitPropertyEditsRecursive(AntPropertyViewModel prop, object parentContainer)
        {
            bool isCollection = prop.ValueObj is System.Collections.IEnumerable && !(prop.ValueObj is string);

            if ((prop.WasEdited && !isCollection) || prop.IsStructureModified)
            {
                object parsedVal = prop.IsStructureModified ? prop.ValueObj : ParseEditedValue(prop.EditedValue, prop.TypeStr);

                if (parentContainer is Dictionary<string, object> dict)
                {
                    dict[prop.Name] = parsedVal;
                }
                else if (parentContainer is IList<object> listObj)
                {
                    int idx = ParseIndex(prop.Name);
                    if (idx >= 0 && idx < listObj.Count)
                        listObj[idx] = parsedVal;
                }
                else if (parentContainer is object[] arrObj)
                {
                    int idx = ParseIndex(prop.Name);
                    if (idx >= 0 && idx < arrObj.Length)
                        arrObj[idx] = parsedVal;
                }
                else if (parentContainer is System.Collections.IList listRaw)
                {
                    int idx = ParseIndex(prop.Name);
                    if (idx >= 0 && idx < listRaw.Count)
                    {
                        try { listRaw[idx] = parsedVal; } catch { }
                    }
                }

                prop.ValueStr = prop.IsStructureModified ? prop.GetString(parsedVal) : prop.EditedValue;
                prop.ValueObj = parsedVal;
                prop.IsStructureModified = false;
            }

            if (prop.Children != null && prop.Children.Count > 0)
            {
                foreach (var child in prop.Children)
                {
                    CommitPropertyEditsRecursive(child, prop.ValueObj);
                }
            }
        }

        private int ParseIndex(string name)
        {
            if (name.StartsWith("[") && name.EndsWith("]"))
            {
                if (int.TryParse(name.Substring(1, name.Length - 2), out int res))
                    return res;
            }
            return -1;
        }

        private object ParseEditedValue(string valueStr, string typeStr)
        {
            switch (typeStr)
            {
                case "Boolean": return bool.Parse(valueStr);
                case "SByte": return sbyte.Parse(valueStr);
                case "Byte": return byte.Parse(valueStr);
                case "Int16": return short.Parse(valueStr);
                case "UInt16": return ushort.Parse(valueStr);
                case "Int32": return int.Parse(valueStr);
                case "UInt32": return uint.Parse(valueStr);
                case "Int64": return long.Parse(valueStr);
                case "UInt64": return ulong.Parse(valueStr);
                case "Single": return float.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
                case "Double": return double.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
                case "Guid": return Guid.Parse(valueStr);
                default: return valueStr;
            }
        }

        internal static object DeepCopyRawData(object obj)
        {
            if (obj == null) return null;

            if (obj is Dictionary<string, object> dict)
            {
                var newDict = new Dictionary<string, object>(dict.Count);
                foreach (var kvp in dict)
                {
                    newDict[kvp.Key] = DeepCopyRawData(kvp.Value);
                }
                return newDict;
            }

            if (obj is List<object> listObj)
            {
                var newList = new List<object>(listObj.Count);
                foreach (var item in listObj)
                {
                    newList.Add(DeepCopyRawData(item));
                }
                return newList;
            }

            if (obj is object[] arrObj)
            {
                var newArr = new object[arrObj.Length];
                for (int i = 0; i < arrObj.Length; i++)
                {
                    newArr[i] = DeepCopyRawData(arrObj[i]);
                }
                return newArr;
            }

            if (obj is float[] floatArr)
            {
                var newArr = new float[floatArr.Length];
                Buffer.BlockCopy(floatArr, 0, newArr, 0, floatArr.Length * 4);
                return newArr;
            }

            if (obj is byte[] byteArr)
            {
                var newArr = new byte[byteArr.Length];
                Buffer.BlockCopy(byteArr, 0, newArr, 0, byteArr.Length);
                return newArr;
            }

            if (obj is uint[] uintArr)
            {
                var newArr = new uint[uintArr.Length];
                Buffer.BlockCopy(uintArr, 0, newArr, 0, uintArr.Length * 4);
                return newArr;
            }

            if (obj is ushort[] ushortArr)
            {
                var newArr = new ushort[ushortArr.Length];
                Buffer.BlockCopy(ushortArr, 0, newArr, 0, ushortArr.Length * 2);
                return newArr;
            }

            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                var newList = new List<object>();
                foreach (var item in enumerable)
                {
                    newList.Add(DeepCopyRawData(item));
                }
                return newList;
            }

            return obj;
        }

        private static ulong GenerateRandom64BitKey()
        {
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[8];
                rng.GetBytes(bytes);
                return BitConverter.ToUInt64(bytes, 0);
            }
        }

        public void DuplicateAsset(AntAssetViewModel sourceVm)
        {
            if (sourceVm?.AssetInstance == null || !(sourceVm.AssetInstance is AntAsset sourceAsset))
                return;
            if (_bankBytes == null)
                return;

            bool big = _bankIsBigEndian;
            var classes = _bank.Classes2;

            try
            {
                var newRawData = (Dictionary<string, object>)DeepCopyRawData(sourceAsset.RawData);

                ulong newKey = GenerateRandom64BitKey();
                Guid newGuid = KeyToGuid(newKey);

                string suggestedName = MakeCopyName(sourceAsset.Name);

                var renameWin = new DuplicateRenameWindow(suggestedName, _bank.DataNames.Keys) { Owner = Window.GetWindow(this) };
                if (renameWin.ShowDialog() != true)
                {
                    return; // Action cancelled
                }

                string newName = renameWin.NewName;

                newRawData["__name"] = newName;
                newRawData["__guid"] = newGuid;
                newRawData["__key"] = newKey;

                Type assetType = sourceAsset.GetType();
                AntAsset newAsset = (AntAsset)Activator.CreateInstance(assetType);
                newAsset.AssetType = sourceAsset.AssetType;
                newAsset.Bank = sourceAsset.Bank;
                newAsset.RawData = newRawData;
                newAsset.SetData(newRawData);

                byte[] newSection = DynamicDat2Serializer.Serialize(newAsset, classes, big);

                _bankBytes = AppendSection(_bankBytes, newSection);

                _bankBytes = BankHeaderPatcher.PatchHeaderForKeys(_bankBytes, new[] { newKey }, big);

                _bank.DataNames[newName] = newGuid;
                if (Cache.AntStateBundleIndices.TryGetValue(sourceAsset.ID, out int bIdx))
                {
                    Cache.AntStateBundleIndices[newGuid] = bIdx;
                }

                AntRefTable.Add(newAsset);

                var newVm = new AntAssetViewModel
                {
                    Name = newName,
                    Id = newGuid,
                    AssetInstance = newAsset,
                    ParentEditor = this,
                    IsNew = true,
                    IsUnsaved = true,
                };

                var group = _masterGroups.FirstOrDefault(g => g.TypeName == newAsset.AssetType);
                if (group != null)
                {
                    int idx = group.Assets.IndexOf(sourceVm);
                    if (idx < 0) idx = group.Assets.Count - 1;
                    group.Assets.Insert(idx + 1, newVm);
                    _selectedAsset = newVm;
                }

                var filteredGroup = _filteredGroups.FirstOrDefault(g => g.TypeName == newAsset.AssetType);
                if (filteredGroup == null)
                {
                    filteredGroup = new AntTypeGroup { TypeName = newAsset.AssetType };
                    _filteredGroups.Add(filteredGroup);
                    filteredGroup.Assets.Add(newVm);
                }
                else if (filteredGroup != group)
                {
                    int filteredIdx = filteredGroup.Assets.IndexOf(sourceVm);
                    filteredGroup.Assets.Insert(filteredIdx >= 0 ? filteredIdx + 1 : filteredGroup.Assets.Count, newVm);
                }

                App.Logger.Log($"[AntStateEditor] Duplicated asset '{sourceAsset.Name}' -> '{newName}' dynamically (key: {newKey:X16}).");
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor] Dynamic duplication failed: {ex.Message}\n{ex.StackTrace}");
                FrostyMessageBox.Show($"Failed to duplicate asset: {ex.Message}", "Duplication Failed");
            }
        }

        public void ImportAnimation(AntAssetViewModel sourceVm)
        {
            if (!(sourceVm?.AssetInstance is VbrAnimationAsset template)) return;
            if (_bankBytes == null) return;

            if (template.OrderedChannels == null || template.OrderedChannels.Count == 0)
                template.Channels = template.GetChannels(template.ChannelToDofAsset);

            if (template.OrderedChannels != null && template.OrderedChannels.Count > 0)
            {
                App.Logger.Log("[DBG] OrderedChannels count=" + template.OrderedChannels.Count);
                int dbgLimit = Math.Min(8, template.OrderedChannels.Count);
                for (int dbgI = 0; dbgI < dbgLimit; dbgI++)
                {
                    var dbgCh = template.OrderedChannels[dbgI];
                    App.Logger.Log("[DBG] ch[" + dbgI + "] name='" + dbgCh.Name + "' type=" + dbgCh.Type);
                }
            }
            else
            {
                App.Logger.Log("[DBG] OrderedChannels is NULL or EMPTY after GetChannels");
            }

            var ofd = new OpenFileDialog
            {
                Title = "Import Animation",
                Filter = "Supported Animations (*.fbx;*.dae;*.gltf;*.glb)|*.fbx;*.dae;*.gltf;*.glb|All Files (*.*)|*.*",
                DefaultExt = ".fbx",
            };
            if (ofd.ShowDialog() != true) return;

            string filePath = ofd.FileName;

            FrostyTaskWindow.Show("Importing Animation", "", (task) =>
            {
                try
                {
                    byte[] newBank = null;

                    // All supported animations are loaded via Assimp
                    var ctx = new AssimpContext();
                    Scene scene = ctx.ImportFile(filePath, PostProcessSteps.None);

                    newBank = AnimationImporter.Import(
                        scene, _bankBytes, template, _bank.Classes2, _bankIsBigEndian);

                    if (newBank != null)
                    {
                        // Store in memory only
                        _bankBytes = newBank;

                        App.Logger.Log(
                            "[AntStateEditor] Imported animation into '" + template.Name +
                            "' from '" + System.IO.Path.GetFileName(filePath) + "'.");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            sourceVm.MarkModified();
                            sourceVm.ResetProperties(); // Updates UI properties

                            // Force a full reload with the templates active name
                            _currentPreviewAsset = template;
                            _currentPreviewName = template.Name;
                            _ = LoadPreviewAsync(template, _currentPreviewName);

                            FrostyMessageBox.Show(
                                "Animation imported successfully.\nClick Save to commit changes.",
                                "Import Complete");
                        });
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError(
                        "[AntStateEditor] Import failed: " + ex.Message + "\n" + ex.StackTrace);
                    Application.Current.Dispatcher.Invoke(() =>
                        FrostyMessageBox.Show("Import failed: " + ex.Message, "Import Failed"));
                }
            });
        }

        public void EditAnimAssets(AntAssetViewModel sourceVm)
        {
            if (sourceVm?.AssetInstance == null || !(sourceVm.AssetInstance is AntAsset ant) || ant.AssetType != "ActorControllerAsset") return;
            if (_bankBytes == null) return;

            bool big = _bankIsBigEndian;
            ulong key = GuidToKey(sourceVm.Id);

            int secStart = FindDat2SectionStart(_bankBytes, ActorControllerTypeHash, key, keyFieldOffset: 48, big);
            if (secStart < 0)
            {
                FrostyMessageBox.Show("Could not find ActorControllerAsset section.", "Error");
                return;
            }

            int tracksCount = (int)ReadU32(_bankBytes, secStart + 64, big);
            if (tracksCount <= 0) { FrostyMessageBox.Show("No tracks found in this asset.", "Edit Anim Assets"); return; }

            var entries = new List<AnimAssetEntry>();
            for (int t = 0; t < tracksCount; t++)
            {
                int tracksPtrField = secStart + 68;
                long d1 = Decode60Bit(ReadU64(_bankBytes, tracksPtrField, big));
                int tHeap = (int)(tracksPtrField + d1);

                int trackRef = tHeap + t * 8;
                if (trackRef + 8 > _bankBytes.Length) break;
                long d2 = Decode60Bit(ReadU64(_bankBytes, trackRef, big));
                int trackStart = (int)(trackRef + d2);

                if (trackStart + 52 + 4 > _bankBytes.Length) break;
                int animsCount = (int)ReadU32(_bankBytes, trackStart + 52, big);

                for (int a = 0; a < animsCount; a++)
                {
                    int animsPtrField = trackStart + 56;
                    if (animsPtrField + 8 > _bankBytes.Length) break;
                    long d3 = Decode60Bit(ReadU64(_bankBytes, animsPtrField, big));
                    int aHeap = (int)(animsPtrField + d3);

                    int animRef = aHeap + a * 8;
                    if (animRef + 8 > _bankBytes.Length) break;
                    long d4 = Decode60Bit(ReadU64(_bankBytes, animRef, big));
                    int animStart = (int)(animRef + d4);

                    int assetOffset = animStart + 32;
                    if (assetOffset + 8 > _bankBytes.Length) break;
                    ulong curAssetKey = ReadU64(_bankBytes, assetOffset, big);

                    entries.Add(new AnimAssetEntry
                    {
                        TrackIndex = t,
                        AnimIndex = a,
                        AssetOffset = assetOffset,
                        CurrentKey = curAssetKey,
                    });
                }
            }

            if (entries.Count == 0)
            {
                FrostyMessageBox.Show("No animation entries found in track list.", "Edit Anim Assets");
                return;
            }

            var win = new EditAnimAssetsWindow(entries) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            bool anyChanged = false;
            foreach (var e in entries)
            {
                if (!e.WasEdited) continue;
                PatchU64InPlace(_bankBytes, e.AssetOffset, e.NewKey, big);
                anyChanged = true;
                App.Logger.Log($"[AntStateEditor] Patched Track[{e.TrackIndex}] Anim[{e.AnimIndex}] Asset -> {e.NewKey:X16}");
            }

            if (!anyChanged) return;

            try
            {
                var opt = new AnimationOptions();
                opt.Load();
                CompressionType selectedCompression = opt.GetSelectedCompressionType();

                if (_bankResEntry != null)
                    ModifyResWithFastCompression(_bankResEntry, _bankBytes, selectedCompression);
                else if (_bankChunkEntry != null)
                    App.AssetManager.ModifyChunk(_bankChunkEntry.Id, _bankBytes, selectedCompression);
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[AntStateEditor] Failed to write bank bytes: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                    FrostyMessageBox.Show($"Failed to write bank: {ex.Message}", "Save Failed"));
                return;
            }
        }

        private static Dictionary<string, object> GetOriginalAssetRawData(ResAssetEntry resEntry, ChunkAssetEntry chunkEntry, int bundleId, Guid assetId)
        {
            byte[] origBytes = null;
            if (resEntry != null)
            {
                var mod = resEntry.ModifiedEntry;
                resEntry.ModifiedEntry = null;
                using (var origStream = App.AssetManager.GetRes(resEntry))
                {
                    if (origStream != null) { using (var ms = new MemoryStream()) { origStream.CopyTo(ms); origBytes = ms.ToArray(); } }
                }
                resEntry.ModifiedEntry = mod;
            }
            else if (chunkEntry != null)
            {
                var mod = chunkEntry.ModifiedEntry;
                chunkEntry.ModifiedEntry = null;
                using (var origStream = App.AssetManager.GetChunk(chunkEntry))
                {
                    if (origStream != null) { using (var ms = new MemoryStream()) { origStream.CopyTo(ms); origBytes = ms.ToArray(); } }
                }
                chunkEntry.ModifiedEntry = mod;
            }

            if (origBytes == null) return null;

            Dictionary<string, object> rawData = null;
            using (var reader = new NativeReader(new MemoryStream(origBytes)))
            {
                var activeRefs = AntRefTable.Refs;
                var activeInternalRefs = AntRefTable.InternalRefs;
                var tempRefs = new System.Collections.Concurrent.ConcurrentDictionary<Guid, AntAsset>();
                var tempInternalRefs = new System.Collections.Concurrent.ConcurrentDictionary<Guid, Guid>();

                AntRefTable.Refs = tempRefs;
                AntRefTable.InternalRefs = tempInternalRefs;

                var origBank = new Bank(reader, bundleId, isHashMode: false);
                var asset = AntRefTable.Get(assetId);
                if (asset != null)
                {
                    rawData = (Dictionary<string, object>)DeepCopyRawData(asset.RawData);
                }

                tempRefs.Clear();
                tempInternalRefs.Clear();
                AntRefTable.Refs = activeRefs;
                AntRefTable.InternalRefs = activeInternalRefs;
            }

            return rawData;
        }

        public void RevertAsset(AntAssetViewModel vm)
        {
            if (vm == null) return;

            if (vm.IsNew)
            {
                if (FrostyMessageBox.Show($"Are you sure you want to delete the duplicated asset '{vm.Name}'?", "Delete Asset", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;

                if (vm.AssetInstance is AntAsset asset)
                {
                    AntRefTable.Refs.TryRemove(vm.Id, out _);
                    _bank?.DataNames.Remove(vm.Name);

                    var group = _masterGroups.FirstOrDefault(g => g.TypeName == asset.AssetType);
                    if (group != null) group.Assets.Remove(vm);

                    var filteredGroup = _filteredGroups.FirstOrDefault(g => g.TypeName == asset.AssetType);
                    if (filteredGroup != null) filteredGroup.Assets.Remove(vm);

                    if (group != null && group.Assets.Count == 0) _masterGroups.Remove(group);
                    if (filteredGroup != null && filteredGroup.Assets.Count == 0) _filteredGroups.Remove(filteredGroup);

                    bool big = _bankIsBigEndian;
                    var classes = _bank.Classes2;
                    uint typeHash = DynamicDat2Serializer.GetTypeHashForAsset(asset, classes);
                    ulong assetKey = GuidToKey(asset.ID);

                    int keyOffset = 16;
                    if (classes.TryGetValue(typeHash, out var layout))
                    {
                        var keyField = layout.Elements.FirstOrDefault(f => f.Name == "__key" || f.Name == "__guid");
                        if (keyField != null) keyOffset = keyField.Offset;
                    }

                    int secStart = FindDat2SectionStart(_bankBytes, typeHash, assetKey, keyFieldOffset: keyOffset, big);
                    if (secStart >= 0)
                    {
                        int sz = (int)ReadU32(_bankBytes, secStart + 8, big);
                        byte[] result = new byte[_bankBytes.Length - sz];
                        Buffer.BlockCopy(_bankBytes, 0, result, 0, secStart);
                        Buffer.BlockCopy(_bankBytes, secStart + sz, result, secStart, _bankBytes.Length - secStart - sz);
                        _bankBytes = result;
                        _bankBytes = BankHeaderPatcher.PatchHeaderForKeys(_bankBytes, Array.Empty<ulong>(), big, new[] { assetKey });
                    }

                    _isBankBytesDirty = true;
                    AssetModified = true;
                    App.Logger.Log($"[AntStateEditor] Deleted duplicated asset '{vm.Name}'. Click 'Save' to commit changes.");
                }
            }
            else
            {
                if (FrostyMessageBox.Show($"Are you sure you want to revert '{vm.Name}' to its original base-game state?", "Revert Asset", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;

                if (vm.AssetInstance is AntAsset asset)
                {
                    int bundleId = _bankResEntry != null ? _bankResEntry.Bundles[0] : _bankChunkEntry.Bundles[0];
                    var originalRawData = GetOriginalAssetRawData(_bankResEntry, _bankChunkEntry, bundleId, vm.Id);

                    if (originalRawData == null)
                    {
                        FrostyMessageBox.Show("Failed to retrieve original asset data.", "Error");
                        return;
                    }

                    asset.RawData = originalRawData;
                    asset.SetData(originalRawData);

                    vm.ResetProperties();

                    vm.IsModified = false;
                    vm.IsUnsaved = true;
                    _isBankBytesDirty = true;
                    AssetModified = true;

                    if (asset is AnimationAsset animAsset && _currentPreviewAsset == animAsset)
                    {
                        _ = LoadPreviewAsync(animAsset, _currentPreviewName);
                    }

                    App.Logger.Log($"[AntStateEditor] Reverted asset '{vm.Name}' back to original. Click 'Save' to write back.");
                }
            }
        }

        internal class AnimAssetEntry
        {
            public int TrackIndex;
            public int AnimIndex;
            public int AssetOffset;
            public ulong CurrentKey;
            public ulong NewKey;
            public bool WasEdited;

            public string CurrentGuid => KeyToGuid(CurrentKey).ToString();
        }

        public static ulong GuidToKey(Guid g) => BitConverter.ToUInt64(g.ToByteArray(), 0);

        public static Guid KeyToGuid(ulong key)
        {
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(key).CopyTo(bytes, 0);
            return new Guid(bytes);
        }

        private static int FindDat2SectionStart(byte[] bank, uint typeHash, ulong assetKey, int keyFieldOffset, bool big)
        {
            int keyAbsOff = 44 + keyFieldOffset;
            int pos = 0;
            while (pos + 12 <= bank.Length)
            {
                if (bank[pos] == 'G' && bank[pos + 1] == 'D' && bank[pos + 2] == '.' &&
                    bank[pos + 3] == 'D' && bank[pos + 4] == 'A' && bank[pos + 5] == 'T' && bank[pos + 6] == '2')
                {
                    uint sz = ReadU32(bank, pos + 8, big);
                    if (pos + 32 <= bank.Length)
                    {
                        uint secHash = ReadU32(bank, pos + 28, big);
                        if (secHash == typeHash && pos + keyAbsOff + 8 <= bank.Length)
                            if (ReadU64(bank, pos + keyAbsOff, big) == assetKey)
                                return pos;
                    }
                    pos += (int)Math.Max(sz, 1u);
                }
                else { pos++; }
            }
            return -1;
        }

        private static byte[] SpliceSection(byte[] bank, int sectionStart, byte[] newSection, bool big)
        {
            int oldSz = (int)ReadU32(bank, sectionStart + 8, big);
            byte[] result = new byte[bank.Length - oldSz + newSection.Length];
            Buffer.BlockCopy(bank, 0, result, 0, sectionStart);
            Buffer.BlockCopy(newSection, 0, result, sectionStart, newSection.Length);
            Buffer.BlockCopy(bank, sectionStart + oldSz, result, sectionStart + newSection.Length, bank.Length - sectionStart - oldSz);
            return result;
        }

        private static string MakeCopyName(string original)
        {
            if (!original.Contains("_copy"))
                return original + "_copy";

            int copyIdx = original.LastIndexOf("_copy", StringComparison.Ordinal);
            string suffix = original.Substring(copyIdx + 5);
            if (int.TryParse(suffix, out int n))
                return original.Substring(0, copyIdx + 5) + (n + 1);
            return original + "2";
        }

        private static byte[] AppendSection(byte[] bank, byte[] section)
        {
            byte[] result = new byte[bank.Length + section.Length];
            Buffer.BlockCopy(bank, 0, result, 0, bank.Length);
            Buffer.BlockCopy(section, 0, result, bank.Length, section.Length);
            return result;
        }

        private static bool DetectBigEndian(byte[] bank)
        {
            for (int i = 0; i + 8 <= bank.Length; i++)
                if (bank[i] == 'G' && bank[i + 1] == 'D' && bank[i + 2] == '.') return bank[i + 7] == 'b';
            return false;
        }

        private static uint ReadU32(byte[] b, int o, bool big)
            => big ? (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3])
                   : (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

        private static ulong ReadU64(byte[] b, int o, bool big)
            => big ? ReadU64BE(b, o) : ReadU64LE(b, o);

        private static float ReadF32(byte[] b, int o, bool big)
        {
            byte[] tmp = big ? new byte[] { b[o + 3], b[o + 2], b[o + 1], b[o] } : new byte[] { b[o], b[o + 1], b[o + 2], b[o + 3] };
            return BitConverter.ToSingle(tmp, 0);
        }

        private static ulong ReadU64LE(byte[] b, int o)
            => (ulong)b[o] | ((ulong)b[o + 1] << 8) | ((ulong)b[o + 2] << 16) | ((ulong)b[o + 3] << 24)
             | ((ulong)b[o + 4] << 32) | ((ulong)b[o + 5] << 40) | ((ulong)b[o + 6] << 48) | ((ulong)b[o + 7] << 56);

        private static ulong ReadU64BE(byte[] b, int o)
            => ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40) | ((ulong)b[o + 3] << 32)
             | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16) | ((ulong)b[o + 6] << 8) | (ulong)b[o + 7];

        private static long Decode60Bit(ulong rawEncoded)
        {
            long signed = (long)rawEncoded;
            return signed << 4 >> 4;
        }

        private static void PatchU64InPlace(byte[] bank, int offset, ulong value, bool big)
        {
            if (big)
            {
                ulong v = value;
                v = ((v & 0xFF) << 56) | (((v >> 8) & 0xFF) << 48) | (((v >> 16) & 0xFF) << 40) | (((v >> 24) & 0xFF) << 32)
                  | (((v >> 32) & 0xFF) << 24) | (((v >> 40) & 0xFF) << 16) | (((v >> 48) & 0xFF) << 8) | ((v >> 56) & 0xFF);
                value = v;
            }
            for (int i = 0; i < 8; i++) bank[offset + i] = (byte)(value >> (i * 8));
        }
    }
}
