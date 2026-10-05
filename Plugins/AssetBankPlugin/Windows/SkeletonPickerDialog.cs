using Frosty.Core;
using System.Linq;
using System.Windows;

namespace AssetBankPlugin
{
    public class SkeletonPickerDialog : AssetPickerDialog
    {
        public SkeletonPickerDialog()
        {
            Title = "  Select Preview Skeleton";
            Width = 720;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var entries = App.AssetManager
                             .EnumerateEbx("SkeletonAsset")
                             .OrderBy(x => x.Name);

            ConfigureFlatList(
                assetType: "SkeletonAsset",
                watermark: "Search skeleton assets by name or path…",
                entries: entries,
                badgeColor: "#1A6B38",              
                confirmLabel: "Select",
                footerHint: "Double-click or press Select to confirm");
        }
    }
}