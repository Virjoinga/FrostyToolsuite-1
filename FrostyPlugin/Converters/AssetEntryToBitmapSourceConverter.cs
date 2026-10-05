using Frosty.Core.Windows;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Frosty.Core.Converters
{
    public class AssetEntryAndSizeToBitmapSourceConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            AssetEntry entry = values[0] as AssetEntry;

            double width = (values[1] != DependencyProperty.UnsetValue) ? (double)values[1] : double.PositiveInfinity;
            double height = (values[2] != DependencyProperty.UnsetValue) ? (double)values[2] : double.PositiveInfinity;

            if (entry != null)
            {
                string sourceName = entry.Type;

                var definition = App.PluginManager.GetAssetDefinition(sourceName);
                return definition != null ? definition.GetIcon(entry, width, height) : new StringToBitmapSourceConverter().Convert(sourceName, targetType, parameter, culture);
            }
            return new StringToBitmapSourceConverter().Convert(values[0] as string, targetType, parameter, culture);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AssetEntryToBitmapSourceConverter : IValueConverter
    {
        private static readonly Dictionary<string, object> IconCache = [];
        private static readonly StringToBitmapSourceConverter CoreConverter = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string sourceName;
            if (value is AssetEntry entry)
                sourceName = entry.Type;
            else if (value is AssetInstanceInfo info)
                sourceName = info.Type;
            else
                sourceName = value as string;

            if (IconCache.TryGetValue(sourceName, out var icon))
                return icon;

            var definition = App.PluginManager.GetAssetDefinition(sourceName);
            object assetIcon = definition != null
                ? definition.GetIcon()
                : CoreConverter.Convert(sourceName, targetType, parameter, culture);

            IconCache.Add(sourceName, assetIcon);
            return assetIcon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
