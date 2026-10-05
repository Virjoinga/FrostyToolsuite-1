using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using AssetBankPlugin.Formats;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace AssetBankPlugin
{
    public class AntStateAssetDefinition : AssetDefinition
    {
        // Tracker to prevent loading the same level context twice.
        private static HashSet<string> _loadedContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _loadLock = new object();

        // This loads all AssetBank bundles that share the same folder prefix as the current asset.
        // It ensures that cross‑referenced AnimationAssets (e.g. boss <-> level) are in AntRefTable.
        public static void LoadContextualBanks(EbxAssetEntry entry)
        {
            if (entry == null || entry.Bundles.Count == 0) return;

            string bundleName = App.AssetManager.GetBundleEntry(entry.Bundles[0]).Name;
            string prefix = bundleName.Contains("/")
                ? bundleName.Substring(0, bundleName.LastIndexOf('/') + 1)
                : bundleName;

            lock (_loadLock)
            {
                if (_loadedContexts.Contains(prefix)) return;
                var matchingBundles = App.AssetManager.EnumerateBundles()
                    .Where(b => b.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                foreach (var bundle in matchingBundles)
                {
                    LoadAntStateFromBundle(bundle);
                }
                _loadedContexts.Add(prefix);
            }
        }

        public override void GetSupportedExportTypes(List<AssetExportType> exportTypes)
        {
            // Adding Cast at the top makes it the default selection in the save dialog.
            exportTypes.Add(new AssetExportType("cast", "Cast Asset Container"));
            exportTypes.Add(new AssetExportType("seanim", "SEAnim Animation File"));
            exportTypes.Add(new AssetExportType("gltf", "GL Transfer format"));
            exportTypes.Add(new AssetExportType("xml", "XML Animation Keyframe Dump"));
            exportTypes.Add(new AssetExportType("smd", "Source StudioMdl Data"));
            exportTypes.Add(new AssetExportType("txt", "Metadata Text Dump"));
            base.GetSupportedExportTypes(exportTypes);
        }

        public override FrostyAssetEditor GetEditor(ILogger logger) => new AntStateAssetEditor(logger);

        public override bool Export(EbxAssetEntry entry, string path, string filterType)
        {
            var opt = new AnimationOptions();
            opt.Load();

            if (string.IsNullOrEmpty(opt.ExportSkeletonAsset) && filterType != "txt")
            {
                FrostyMessageBox.Show("Please set an Export Skeleton in Options first.", "Missing Skeleton");
                return false;
            }

            string exportDirectory = Path.GetDirectoryName(path);
            int exportedCount = 0;
            int totalAnimations = 0;

            EbxAsset asset = App.AssetManager.GetEbx(entry);
            dynamic antStateAsset = asset.RootObject;
            Stream bankStream;
            int bundleId = 0;

            if (antStateAsset.StreamingGuid == Guid.Empty)
            {
                var res = App.AssetManager.GetResEntry(entry.Name);
                bundleId = res.Bundles[0];
                bankStream = App.AssetManager.GetRes(res);
            }
            else
            {
                var chunk = App.AssetManager.GetChunkEntry(antStateAsset.StreamingGuid);
                bundleId = chunk.Bundles[0];
                bankStream = App.AssetManager.GetChunk(chunk);
            }

            Bank bank;
            using (var reader = new NativeReader(bankStream))
            {
                bank = new Bank(reader, bundleId);
            }
            totalAnimations = bank.DataNames.Count;

            // Handle Metadata Text Dump export type
            if (filterType == "txt")
            {
                StringBuilder sb = new StringBuilder();
                foreach (var dataName in bank.DataNames)
                {
                    AntAsset antAsset = AntRefTable.Get(dataName.Value);
                    if (antAsset != null)
                    {
                        sb.AppendLine($"[Asset]: {dataName.Key}");
                        sb.AppendLine($"[Type]: {antAsset.GetType().Name}");
                        sb.AppendLine($"[ID]: {antAsset.ID}");
                        DumpDictionary(sb, antAsset.RawData, 1);
                        sb.AppendLine(new string('=', 60));
                    }
                }
                File.WriteAllText(path, sb.ToString());
                FrostyMessageBox.Show($"Text dump saved to:\n{path}", "Export Complete");
                return true;
            }

            FrostyTaskWindow.Show("Exporting Animations", "", (task) =>
            {
                try
                {
                    InternalSkeleton skeleton = null;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LoadContextualBanks(entry);
                        var skelEntry = App.AssetManager.GetEbxEntry(opt.ExportSkeletonAsset);
                        skeleton = SkeletonAssetExport.ConvertToInternal(App.AssetManager.GetEbx(skelEntry).RootObject);
                    });

                    int count = 0;
                    // Inside AntStateAssetDefinition.Export loop:
                    foreach (var dataName in bank.DataNames)
                    {
                        double progress = ((double)count / totalAnimations) * 100.0;
                        task.Update(dataName.Key, progress);

                        Dictionary<string, BoneChannelType> channels = null;
                        AnimationAsset anim = null;
                        string finalName = dataName.Key; // Default fallback

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var dat = AntRefTable.Get(dataName.Value);
                            if (dat is AnimationAsset foundAnim)
                            {
                                anim = foundAnim;

                                // FIX: Perform the name lookup here
                                var controller = AntRefTable.Refs.Values.FirstOrDefault(a =>
                                {
                                    if (a.AssetType == "ClipControllerAsset")
                                    {
                                        var targetAnimId = a.GetProperty<Guid>("Anim"); // <-- Resolved
                                        var anims = a.GetPropertyArray<Guid>("Anims");
                                        return targetAnimId == dataName.Value || (anims != null && Array.IndexOf(anims, dataName.Value) != -1);
                                    }
                                    if (a.AssetType == "ClipControllerData")
                                    {
                                        return a.GetProperty<Guid>("Anim") == dataName.Value;
                                    }
                                    return false;
                                });

                                if (controller != null)
                                {
                                    finalName = controller.Name;
                                }

                                anim.Name = finalName;
                                channels = anim.GetChannels(anim.ChannelToDofAsset);
                            }
                        });

                        if (anim != null && channels != null && channels.Count > 0)
                        {
                            anim.Channels = channels;
                            var intern = anim.ConvertToInternal();

                            if (intern != null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (filterType == "cast")
                                    {
                                        new AnimationExporterCAST().Export(intern, skeleton, exportDirectory);
                                    }
                                    else
                                    {
                                        new AnimationExporterSEANIM().Export(intern, skeleton, exportDirectory);
                                    }
                                });
                                exportedCount++;
                            }
                        }
                        count++;
                    }

                    App.Logger.Log($"[Export] Successfully exported {exportedCount} animations from {entry.Name}");
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"[Export] Failed: {ex.Message}");
                }
            });

            // finish message
            string finishMessage = $"Successfully exported {exportedCount} animations\nfrom '{entry.Filename}'\n\nDestination:\n{exportDirectory}";
            FrostyMessageBox.Show(finishMessage, "Export Complete");

            return true;
        }

        public static void LoadAntStateFromBundle(BundleEntry bundle)
        {
            // AssetBank resource types (may vary slightly between games)
            var resources = App.AssetManager.EnumerateRes(bundle)
                .Where(r => r.ResType == 0x51A3C853 || r.ResType == 0xEC1B7BF4);

            foreach (var res in resources)
            {
                using (var antBank = App.AssetManager.GetRes(res))
                using (var antReader = new NativeReader(antBank))
                {
                    _ = new Bank(antReader, App.AssetManager.GetBundleId(bundle));
                }
            }
        }

        // Helper method for recursive text dumping
        private void DumpDictionary(StringBuilder sb, Dictionary<string, object> data, int indent)
        {
            if (data == null) return;
            string space = new string(' ', indent * 4);
            foreach (var kvp in data)
            {
                // Ignore massive animation buffers to prevent OutOfMemoryException
                if (kvp.Key.Equals("Data", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals("ConstantPalette", StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value is Array arr)
                    {
                        sb.AppendLine($"{space}{kvp.Key} (Array, Count: {arr.Length}) [Content Omitted]");
                    }
                    else
                    {
                        sb.AppendLine($"{space}{kvp.Key}: {kvp.Value} [Content Omitted]");
                    }
                    continue;
                }

                if (kvp.Value is Dictionary<string, object> nested)
                {
                    sb.AppendLine($"{space}{kvp.Key} (Block):");
                    DumpDictionary(sb, nested, indent + 1);
                }
                else if (kvp.Value is Array arr)
                {
                    sb.AppendLine($"{space}{kvp.Key} (Array, Count: {arr.Length}):");
                    for (int i = 0; i < arr.Length; i++)
                    {
                        object elem = arr.GetValue(i);
                        if (elem is Dictionary<string, object> d)
                            DumpDictionary(sb, d, indent + 1);
                        else
                            sb.AppendLine($"{space}  [{i}]: {elem}");
                    }
                }
                else
                {
                    sb.AppendLine($"{space}{kvp.Key}: {kvp.Value}");
                }
            }
        }

        // Format: pack://application:,,,/AssemblyName;component/Folder/File.png
        protected static ImageSource antIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/AssetBankPlugin;component/Images/AntStateAssetFileType.png") as ImageSource;

        // This tells Frosty which icon to show in the Data Explorer
        public override ImageSource GetIcon()
        {
            return antIcon;
        }
    }
}