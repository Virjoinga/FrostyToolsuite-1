using AssetBankPlugin.Ant;
using AssetBankPlugin.Export;
using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using FrostySdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using AssetBankPlugin.Formats;

namespace AssetBankPlugin
{
    public class AntAnimationSetAssetDefinition : AssetDefinition
    {
        public override void GetSupportedExportTypes(List<AssetExportType> exportTypes)
        {
            exportTypes.Add(new AssetExportType("gltf", "GL Transfer format"));
            exportTypes.Add(new AssetExportType("xml", "XML Animation Keyframe Dump"));
            exportTypes.Add(new AssetExportType("smd", "Source StudioMdl Data"));

            base.GetSupportedExportTypes(exportTypes);
        }

        public override bool Export(EbxAssetEntry entry, string path, string filterType)
        {
            var opt = new AnimationOptions();
                opt.Load();
            // Get the Ebx.
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            dynamic antSetAsset = (dynamic)asset.RootObject;
            // Get the Chunk.
            Stream s;
            int bundleId = 0;
            if (antSetAsset.AssetBankResource != (ulong)0)
            {
                ResAssetEntry res = App.AssetManager.GetResEntry(antSetAsset.AssetBankResource);
                s = App.AssetManager.GetRes(res);
            }
            else
            {
                App.Logger.Log("Asset does not reference a bank");
                return false;
            }
            IEnumerable<BundleEntry> bundles;
            string cachePath = $"Caches/{ProfilesLibrary.ProfileName}_antstate.cache";
            string internalCachePath = $"Caches/{ProfilesLibrary.ProfileName}_antref.cache";

            // If we're not using the cache or it doesn't exist yet, then get every bundle.
            if (opt.UseCache)
            {
                if (File.Exists(cachePath) && File.Exists(internalCachePath))
                {
                    Cache.ReadState(cachePath);
                    Cache.ReadMap(internalCachePath);
                }
                else
                {
                    // First time setup, read all bundles and store them in the antstatecache.
                    bundles = App.AssetManager.EnumerateBundles();
                    foreach (var bundle in bundles)
                    {
                        LoadAntStateFromBundle(bundle);
                    }
                    Cache.WriteState(cachePath);
                    Cache.WriteMap(internalCachePath);
                }
            }
            else
            {
                bundles = App.AssetManager.EnumerateBundles();
                foreach (var bundle in bundles)
                {
                    LoadAntStateFromBundle(bundle);
                }
            }


            using (var r = new NativeReader(s))
            {
                var bank = new Bank(r, 0);

                

                EbxAssetEntry skelEntry = App.AssetManager.GetEbxEntry(antSetAsset.SkeletonAsset.External.FileGuid);
                var skelEbx = App.AssetManager.GetEbx(skelEntry);
                dynamic skel = (dynamic)skelEbx.RootObject;

                var skeleton = SkeletonAssetExport.ConvertToInternal(skel);
                foreach (var dataName in bank.DataNames)
                {
                    var dat = AntRefTable.Get(dataName.Value);
                    if (dat is AnimationAsset anim)
                    {
                        anim.Name = dataName.Key;
                        anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);
                        var intern = anim.ConvertToInternal();

                        if (filterType == "cast")
                        {
                            new AnimationExporterCAST().Export(intern, skeleton, Path.GetDirectoryName(path));
                        }
                        else
                        {
                            new AnimationExporterSEANIM().Export(intern, skeleton, Path.GetDirectoryName(path));
                        }
                    }
                }
            }

            MessageBox.Show($"Exported {entry.Name} for {ProfilesLibrary.ProfileName}", "Test");

            return true;
        }
        public static void LoadAntStateFromBundle(BundleEntry bundle)
        {
            var resources = App.AssetManager.EnumerateRes(bundle).Where(x => x.Type == "AssetBank");
            foreach (var res in resources)
            {
                Console.WriteLine(res.DisplayName);
                var antBank = App.AssetManager.GetRes(res);
                var antReader = new NativeReader(antBank);
                _ = new Bank(antReader, App.AssetManager.GetBundleId(bundle));
                antBank.Dispose();
                antReader.Dispose();
            }
        }
    }
}
