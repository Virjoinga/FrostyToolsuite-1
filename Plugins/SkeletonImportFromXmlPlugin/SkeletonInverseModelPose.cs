using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using System;
using System.Windows.Media;

namespace SkeletonImportFromXmlPlugin
{
    public class SkeletonInverseModelPoseContextMenuItem : DataExplorerContextMenuExtension
    {
        public override string ContextItemName => "Calculate InverseModelPose from ModelPose";
        public override ImageSource Icon => null;
        
        public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
        {
            EbxAssetEntry entry = App.SelectedAsset as EbxAssetEntry;
            if (entry == null || entry.Type != "SkeletonAsset")
            {
                App.Logger.LogWarning("You must select a SkeletonAsset to use this feature.");
                return;
            }

            FrostyTaskWindow.Show("Calculating InverseModelPose", "", (task) =>
            {
                try
                {
                    EbxAsset asset = App.AssetManager.GetEbx(entry);
                    dynamic skeletonAsset = asset.RootObject;

                    dynamic modelPose = skeletonAsset.ModelPose;
                    if (modelPose == null || modelPose.Count == 0)
                    {
                        App.Logger.LogWarning("ModelPose is empty. Nothing to calculate.");
                        return;
                    }

                    dynamic inverseModelPose = skeletonAsset.InverseModelPose;
                    if (inverseModelPose != null && inverseModelPose.Count > 0)
                    {
                        App.Logger.LogWarning("InverseModelPose is already populated. Aborting to avoid overwriting.");
                        return;
                    }

                    int boneCount = modelPose.Count;

                    if (inverseModelPose == null)
                    {
                        Type listType = modelPose.GetType();
                        skeletonAsset.InverseModelPose = Activator.CreateInstance(listType);
                        inverseModelPose = skeletonAsset.InverseModelPose;
                    }

                    for (int i = 0; i < boneCount; i++)
                    {
                        dynamic transform = modelPose[i];
                        dynamic invTransform = InvertLinearTransform(transform);
                        inverseModelPose.Add(invTransform);
                    }

                    App.AssetManager.ModifyEbx(entry.Name, asset);
                    App.Logger.Log($"Successfully calculated InverseModelPose for {boneCount} bones in {entry.Name}");
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Error calculating InverseModelPose: {ex.Message}");
                }
            });
        });

        private static object InvertLinearTransform(dynamic transform)
        {
            Type ltType = transform.GetType();
            dynamic inv = Activator.CreateInstance(ltType);

            float rx = (float)transform.right.x, ry = (float)transform.right.y, rz = (float)transform.right.z;
            float ux = (float)transform.up.x, uy = (float)transform.up.y, uz = (float)transform.up.z;
            float fx = (float)transform.forward.x, fy = (float)transform.forward.y, fz = (float)transform.forward.z;
            float tx = (float)transform.trans.x, ty = (float)transform.trans.y, tz = (float)transform.trans.z;

            inv.right.x = rx; inv.right.y = ux; inv.right.z = fx;
            inv.up.x = ry; inv.up.y = uy; inv.up.z = fy;
            inv.forward.x = rz; inv.forward.y = uz; inv.forward.z = fz;
            inv.trans.x = -(tx * rx + ty * ry + tz * rz);
            inv.trans.y = -(tx * ux + ty * uy + tz * uz);
            inv.trans.z = -(tx * fx + ty * fy + tz * fz);

            return inv;
        }
    }
}