using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PvZBundleManagerPlugin.Ports.Classes.AssetHandlers.Common
{
    public class SchematicsAssetHandler : BaseAssetHandler
    {
        public override string AssetType => "SchematicsAsset";

        public SchematicsAssetHandler()
        {
        }

        public override bool AddToBundle(EbxAssetEntry entry, BundleEntry bentry)
        {
            if (!base.AddToBundle(entry, bentry))
                return false;

            int bundleId = App.AssetManager.GetBundleId(bentry);
            string classInfoAssetName = entry.Name.Replace("_schematics", "");
            EbxAssetEntry classInfoEntry = App.AssetManager.GetEbxEntry(classInfoAssetName);
            if (classInfoEntry == null)
            {
                App.Logger.Log($"Could not find \"{classInfoAssetName}\" ClassInfo asset");
                return false;
            }
            classInfoEntry.BetterAddToBundle(bundleId);
            entry.LinkAsset(classInfoEntry);

            return true;
        }

        public override EbxAssetEntry Create(string name, EbxAssetEntry basedOnEntry = null, Type newType = null)
        {
            EbxAssetEntry newAstEntry = base.Create(name, basedOnEntry, newType);
            EbxAsset newAst = App.AssetManager.GetEbx(newAstEntry);
            App.AssetManager.ModifyEbx(newAstEntry.Name, newAst);
            return newAstEntry;
        }

        public override bool RemoveFromBundle(EbxAssetEntry entry, BundleEntry bundle)
        {
            if (!base.RemoveFromBundle(entry, bundle))
                return false;

            int bundleId = App.AssetManager.GetBundleId(bundle);
            string classInfoAssetName = entry.Name.Replace("_schematics", "");
            EbxAssetEntry classInfoEntry = App.AssetManager.GetEbxEntry(classInfoAssetName);
            if (classInfoEntry == null)
            {
                App.Logger.Log($"Could not find \"{classInfoAssetName}\" ClassInfo asset");
                return false;
            }
            classInfoEntry.RemoveFromBundle(bundleId);
            entry.LinkAsset(classInfoEntry);

            return true;
        }
    }
}
