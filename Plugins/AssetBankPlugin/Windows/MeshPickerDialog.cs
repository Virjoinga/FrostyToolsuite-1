using Frosty.Core;
using System.Linq;
using System.Windows;

namespace AssetBankPlugin
{
    public class MeshPickerDialog : AssetPickerDialog
    {
        public MeshPickerDialog()
        {
            Title = "  Select Mesh Asset";
            Width = 720;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var skinned = App.AssetManager.EnumerateEbx("SkinnedMeshAsset");
            var rigid = App.AssetManager.EnumerateEbx("RigidMeshAsset");
            var entries = skinned.Concat(rigid).OrderBy(x => x.Name);

            ConfigureFlatList(
                assetType: "MeshAsset",
                watermark: "Search mesh assets by name or path...",
                entries: entries,
                badgeColor: "#007ACC",
                confirmLabel: "Select",
                footerHint: "Double-click or press Select to confirm");
        }
    }
}
