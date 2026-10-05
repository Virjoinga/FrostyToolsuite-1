using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Converters;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ReferencesPlugin;

[TemplatePart(Name = "PART_RefExplorerToTextBlock", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_RefExplorerFromTextBlock", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_RefExplorerToListView", Type = typeof(FrostyAssetListView))]
[TemplatePart(Name = "PART_RefExplorerFromListView", Type = typeof(FrostyAssetListView))]
public class ReferenceTabItem : FrostyTabItem
{
    private FrostyAssetListView toList, fromList;
    private TextBlock toText, fromText;
    private Guid currentGuid = Guid.Empty;

    static ReferenceTabItem() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ReferenceTabItem), new FrameworkPropertyMetadata(typeof(ReferenceTabItem)));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        T Get<T>(string name) where T : DependencyObject => GetTemplateChild(name) as T;

        // Bind References Asset List View Elements
        toList = Get<FrostyAssetListView>("PART_RefExplorerToListView");
        fromList = Get<FrostyAssetListView>("PART_RefExplorerFromListView");

        // Bind References Textblock Elements
        toText = Get<TextBlock>("PART_RefExplorerToTextBlock");
        fromText = Get<TextBlock>("PART_RefExplorerFromTextBlock");

        void BindMenu(string name, FrostyAssetListView list, Action<AssetEntry> action)
        {
            if (Get<MenuItem>(name) is { } m)
                m.Click += (_, _) => { if (list?.SelectedItem is AssetEntry entry) action(entry); };
        }

        // Open Asset
        BindMenu("PART_RefExplorerToOpenItem", toList, e => App.EditorWindow.OpenAsset(e));
        BindMenu("PART_RefExplorerFromOpenItem", fromList, e => App.EditorWindow.OpenAsset(e));

        // Find Item in Data Explorer
        BindMenu("PART_RefExplorerToFindItem", toList, e => App.EditorWindow.DataExplorer.SelectAsset(e));
        BindMenu("PART_RefExplorerFromFindItem", fromList, e => App.EditorWindow.DataExplorer.SelectAsset(e));

        void OnDoubleClick(object s, RoutedEventArgs e)
        {
            if (s is FrostyAssetListView { SelectedItem: EbxAssetEntry entry })
                App.EditorWindow.OpenAsset(entry);
        }

        // Double Click Asset to Open
        if (toList is not null) toList.SelectedAssetDoubleClick += OnDoubleClick;
        if (fromList is not null) fromList.SelectedAssetDoubleClick += OnDoubleClick;

        Loaded += (_, _) => RefreshReferences(App.SelectedAsset);
        App.EditorWindow.DataExplorer.SelectionChanged += (_, _) => RefreshReferences(App.SelectedAsset);
    }

    private void RefreshReferences(EbxAssetEntry entry)
    {
        if (entry == null)
        {
            currentGuid = Guid.Empty;
            fromText.Text = "";
            toText.Text = "No asset selected";
            fromList.ItemsSource = toList.ItemsSource = null;
            return;
        }

        if (entry.Guid == currentGuid) return;
        currentGuid = entry.Guid;

        fromText.Text = $"References from {entry.Filename}";
        toText.Text = $"References to {entry.Filename}";

        // Collect outgoing references
        fromList.ItemsSource = entry.EnumerateDependencies()
            .Select(App.AssetManager.GetEbxEntry)
            .Where(d => d != null).ToList();

        // Collect incoming references
        toList.ItemsSource = App.AssetManager.EnumerateEbx()
            .Where(sub => sub.ContainsDependency(entry.Guid)).ToList();
    }
}

public class FastAssetIconConverter : IValueConverter
{
    private static readonly Dictionary<string, ImageSource> IconCache = [];
    private static readonly StringToBitmapSourceConverter CoreConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AssetEntry entry) return null;
        if (IconCache.TryGetValue(entry.Type, out var icon)) return icon;

        return IconCache[entry.Type] = (ImageSource)(
            App.PluginManager.GetAssetDefinition(entry.Type)?.GetIcon() ??
            CoreConverter.Convert(entry.Type, targetType, parameter, culture));
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}