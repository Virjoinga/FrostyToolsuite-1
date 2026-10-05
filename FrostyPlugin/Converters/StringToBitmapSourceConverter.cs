using FrostySdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Frosty.Core.Converters
{
    public class StringToBitmapSourceConverter : IValueConverter
    {
        public static readonly ImageSource CopySource = LoadImage("Copy.png", false);
        public static readonly ImageSource PasteSource = LoadImage("Paste.png", false);

        private static readonly ImageSource BlankSource = LoadImage("BlankFileType.png");
        private static readonly ImageSource ImageTypeSource = LoadImage("ImageFileType.png");
        private static readonly ImageSource SoundWaveSource = LoadImage("SoundFileType.png");
        private static readonly ImageSource BlueprintSource = LoadImage("BlueprintFileType.png");
        private static readonly ImageSource SubWorldSource = LoadImage("SubWorldFileType.png");
        private static readonly ImageSource ShaderSource = LoadImage("ShaderFileType.png");
        private static readonly ImageSource ShaderPresetSource = LoadImage("ShaderPresetFileType.png");
        private static readonly ImageSource SpreadsheetSource = LoadImage("SpreadsheetFileType.png");
        private static readonly ImageSource StatSource = LoadImage("StatFileType.png");
        private static readonly ImageSource SkeletonSource = LoadImage("SkeletonFileType.png");
        private static readonly ImageSource EncryptedSource = LoadImage("EncryptedFileType.png");
        private static readonly ImageSource MovieTextureSource = LoadImage("MovieTextureFileType.png");
        private static readonly ImageSource ArchiveSource = LoadImage("ArchiveFileType.png");
        private static readonly ImageSource BlueprintBundleSource = LoadImage("BlueprintBundleFileType.png");
        private static readonly ImageSource EmitterSource = LoadImage("EmitterFileType.png");
        private static readonly ImageSource HavokSource = LoadImage("HavokFileType.png");
        private static readonly ImageSource LogicPrefabSource = LoadImage("LogicPrefabFileType.png");
        private static readonly ImageSource InternalSource = LoadImage("InternalFileType.png");
        private static readonly ImageSource ObjectVariationSource = LoadImage("ObjectVariationFileType.png");

        private static ImageSource LoadImage(string fileName, bool isAsset = true)
        {
            string folder = isAsset ? "Assets/" : string.Empty;
            return new ImageSourceConverter().ConvertFromString($"pack://application:,,,/FrostyCore;component/Images/{folder}{fileName}") as ImageSource;
        }

        private static readonly Dictionary<string, ImageSource> SpecificAssetIconTypes = new()
        {
            ["EncryptedAsset"] = EncryptedSource,
            ["ShaderGraph"] = ShaderSource,
            ["SurfaceShaderPreset"] = ShaderPresetSource,
            ["DifficultyWeaponTableData"] = SpreadsheetSource,
            ["DifficultyNPCTableData"] = SpreadsheetSource,
        };

        private static readonly Dictionary<string, ImageSource> IconCache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;

            if (str == null)
                return BlankSource;

            if (IconCache.TryGetValue(str, out ImageSource cached))
                return cached;

            ImageSource result = LoadIcon(str);
            IconCache.Add(str, result);

            return result;
        }

        private static ImageSource LoadIcon(string str)
        {
            var definition = App.PluginManager.GetAssetDefinition(str);
            if (definition != null)
                return definition.GetIcon();

            if (SpecificAssetIconTypes.TryGetValue(str, out ImageSource exact))
                return exact;

            if (TypeLibrary.IsSubClassOf(str, "HavokAsset") || TypeLibrary.IsSubClassOf(str, "PhysicsAsset"))
                return HavokSource;

            if (TypeLibrary.IsSubClassOf(str, "EmitterGraphBaseAsset") ||
                TypeLibrary.IsSubClassOf(str, "EmitterBaseAsset") ||
                TypeLibrary.IsSubClassOf(str, "EmitterAsset"))
                return EmitterSource;

            if (TypeLibrary.IsSubClassOf(str, "ZeroLatencyImpulseResponseAsset"))
                return SoundWaveSource;

            if (TypeLibrary.IsSubClassOf(str, "BWBaseStat") || TypeLibrary.IsSubClassOf(str, "BWAggregatedStat"))
                return StatSource;

            if (TypeLibrary.IsSubClassOf(str, "SubWorldData"))
                return SubWorldSource;

            if (TypeLibrary.IsSubClassOf(str, "LogicPrefabBlueprint"))
                return LogicPrefabSource;

            if (TypeLibrary.IsSubClassOf(str, "Blueprint"))
                return BlueprintSource;

            if (TypeLibrary.IsSubClassOf(str, "SkeletonAsset"))
                return SkeletonSource;

            if (TypeLibrary.IsSubClassOf(str, "BlueprintBundle"))
                return BlueprintBundleSource;

            if (TypeLibrary.IsSubClassOf(str, "ObjectVariation"))
                return ObjectVariationSource;

            if (TypeLibrary.IsSubClassOf(str, "DataContainer") && !TypeLibrary.IsSubClassOf(str, "Asset"))
                return InternalSource;

            if (TypeLibrary.IsSubClassOf(str, "MovieTextureAsset") || TypeLibrary.IsSubClassOf(str, "MovieTexture2Asset"))
                return MovieTextureSource;

            return BlankSource;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return "";
        }
    }
}
