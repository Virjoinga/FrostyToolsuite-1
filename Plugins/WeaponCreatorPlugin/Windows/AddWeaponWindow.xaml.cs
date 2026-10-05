using Frosty.Controls;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static WeaponCreatorPlugin.Windows.WeaponSelectorWindow;

namespace WeaponCreatorPlugin.Windows
{
    public partial class AddWeaponWindow : FrostyDockableWindow
    {
        private string mBlueprintDirectory = "";
        private string mWeaponName = "";
        private string mBaseWeaponPath = "";
        private string mAntStateDirectory = "";

        public AddWeaponWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void varBPDirTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            mBlueprintDirectory = varBPDirTextBox.Text.TrimEnd('/');
        }

        private void varWepNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            mWeaponName = varWepNameTextBox.Text;
        }


        private void varAntStateDirTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            mAntStateDirectory = varAntStateDirTextBox.Text.Trim('/');
        }

        private void BrowseAntStateDir_Click(object sender, RoutedEventArgs e)
        {
            var selector = new AntStateDirSelectorWindow { Owner = this };
            if (selector.ShowDialog() == true && !string.IsNullOrWhiteSpace(selector.SelectedPath))
            {
                mAntStateDirectory = selector.SelectedPath;
                varAntStateDirTextBox.Text = selector.SelectedPath;
            }
        }

        private void varBaseWeaponTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // The textbox is read only and gets filled by the picker
        }

        private string VerifyFileName(string inFilename)
        {
            string filename = inFilename;
            if (filename.Contains("//")) filename = filename.Replace("//", "/");
            if (filename.Contains("\\")) filename = filename.Replace("\\", "/");
            return filename;
        }

        private void BrowseBlueprintDir_Click(object sender, RoutedEventArgs e)
        {
            var selector = new DirectorySelectorWindow { Owner = this };
            if (selector.ShowDialog() == true && !string.IsNullOrWhiteSpace(selector.SelectedPath))
            {
                varBPDirTextBox.Text = selector.SelectedPath;
                mBlueprintDirectory = selector.SelectedPath;
            }
        }

        private void BrowseBaseWeapon_Click(object sender, RoutedEventArgs e)
        {
            var selector = new WeaponSelectorWindow { Owner = this };
            if (selector.ShowDialog() == true && !string.IsNullOrWhiteSpace(selector.SelectedAssetPath))
            {
                mBaseWeaponPath = selector.SelectedAssetPath;
                varBaseWeaponTextBox.Text = selector.SelectedAssetPath;  // display full path in the read only box
            }
        }

        private void createButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(mWeaponName))
            {
                App.Logger.LogError("Weapon name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(mBlueprintDirectory))
            {
                App.Logger.LogError("Blueprint directory is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(mAntStateDirectory))
            {
                App.Logger.LogError("AntState directory is required.");
                return;
            }

            // base weapon
            EbxAssetEntry baseUnlock = null;
            EbxAssetEntry baseBpbEntry = null;
            if (!string.IsNullOrWhiteSpace(mBaseWeaponPath))
            {
                baseUnlock = App.AssetManager.GetEbxEntry(mBaseWeaponPath);
                if (baseUnlock != null)
                {
                    dynamic baseUnlockRoot = App.AssetManager.GetEbx(baseUnlock).RootObject;
                    string baseBpbRefName = (string)baseUnlockRoot.WeaponBlueprintBundleReference.Name;
                    baseBpbEntry = App.AssetManager.GetEbxEntry("Win32/" + baseBpbRefName);
                }
            }

            // Fallback BPB for AntState template and SuperBundleId when no base weapon is selected
            EbxAssetEntry templateBpbEntry = baseBpbEntry
                ?? App.AssetManager.GetEbxEntry("Win32/weapons/ai_allstarjump_bpb");
            if (templateBpbEntry == null)
            {
                App.Logger.LogError("Could not find a BPB to use as template.");
                return;
            }

            string newBpbName = VerifyFileName($"Win32/weapons/{mWeaponName}_bpb");
            EbxAssetEntry newBpb = CreateAsset(newBpbName, TypeLibrary.GetType("PVZCharacterWeaponBlueprintBundle"));
            string bundleName = newBpbName.ToLowerInvariant();

            int sourceBundleId = templateBpbEntry.Bundles.Count > 0
                ? templateBpbEntry.Bundles[0]
                : templateBpbEntry.AddedBundles[0];
            BundleEntry newBundleEntry = App.AssetManager.AddBundle(
                bundleName, BundleType.BlueprintBundle,
                App.AssetManager.GetBundleEntry(sourceBundleId).SuperBundleId);
            int bundleId = App.AssetManager.GetBundleId(newBundleEntry);
            newBpb.AddedBundles.Add(bundleId);

            // AntState using selected directory
            dynamic templateBpbRoot = App.AssetManager.GetEbx(templateBpbEntry).RootObject;
            EbxAssetEntry newAntState = null;

            if (templateBpbRoot.AntStateAssets != null && templateBpbRoot.AntStateAssets.Count > 0)
            {
                EbxAssetEntry antStateAsset = App.AssetManager.GetEbxEntry(templateBpbRoot.AntStateAssets[0].External.FileGuid);
                if (antStateAsset != null)
                {
                    dynamic antStateRoot = App.AssetManager.GetEbx(antStateAsset).RootObject;

                    string antStateName = VerifyFileName(
                        $"animations/antanimations/gameplay/weapons/{mAntStateDirectory}/{mWeaponName}__3p_win32_antstate");
                    newAntState = CreateAsset(antStateName, TypeLibrary.GetType("AntStateAsset"));
                    newAntState.AddedBundles.Clear();
                    newAntState.AddedBundles.Add(bundleId);
                    dynamic newAntRoot = App.AssetManager.GetEbx(newAntState).RootObject;

                    if (antStateRoot.ChunkSize > 0)
                    {
                        ChunkAssetEntry chunk = App.AssetManager.GetChunkEntry(antStateRoot.StreamingGuid);
                        ChunkAssetEntry newChunk = DuplicateChunk(chunk);
                        newChunk.AddedBundles.Clear();
                        newChunk.AddedBundles.Add(bundleId);
                        newAntRoot.StreamingGuid = newChunk.Id;
                        newAntRoot.ChunkSize = (int)App.AssetManager.GetChunk(newChunk).Length;
                        App.AssetManager.ModifyEbx(newAntState.Name, App.AssetManager.GetEbx(newAntState));
                    }
                }
            }
            else
            {
                App.Logger.Log("Base weapon missing ant state asset, no ant state asset was created.");
            }

            // PVZCharacterWeaponBlueprint
            // duplicate from base weapon if one was selected if not then leave blank
            string weaponBPAssetName = $"{mBlueprintDirectory}/{mWeaponName}";
            EbxAssetEntry newWeaponBP;
            if (baseBpbEntry != null)
            {
                try
                {
                    Guid baseBlueprintGuid = templateBpbRoot.Blueprint.External.FileGuid;
                    EbxAssetEntry baseBlueprintEntry = baseBlueprintGuid != Guid.Empty
                        ? App.AssetManager.GetEbxEntry(baseBlueprintGuid)
                        : null;
                    newWeaponBP = baseBlueprintEntry != null
                        ? DuplicateAsset(baseBlueprintEntry, weaponBPAssetName, false)
                        : CreateAsset(weaponBPAssetName, TypeLibrary.GetType("PVZCharacterWeaponBlueprint"));
                }
                catch
                {
                    newWeaponBP = CreateAsset(weaponBPAssetName, TypeLibrary.GetType("PVZCharacterWeaponBlueprint"));
                }
            }
            else
            {
                // No base weapon selected intentionally blank
                newWeaponBP = CreateAsset(weaponBPAssetName, TypeLibrary.GetType("PVZCharacterWeaponBlueprint"));
            }
            newWeaponBP.AddedBundles.Clear();
            newWeaponBP.AddedBundles.Add(bundleId);

            // Wire up BPB
            EbxAsset newBpbAsset = App.AssetManager.GetEbx(newBpb);
            dynamic newBpbRoot = newBpbAsset.RootObject;
            newBpbRoot.Blueprint = new PointerRef(new EbxImportReference
            {
                ClassGuid = App.AssetManager.GetEbx(newWeaponBP).RootInstanceGuid,
                FileGuid = newWeaponBP.Guid
            });
            newBpbAsset.AddDependency(newWeaponBP.Guid);

            if (newAntState != null)
            {
                newBpbRoot.AntStateAssets.Add(new PointerRef(new EbxImportReference
                {
                    ClassGuid = App.AssetManager.GetEbx(newAntState).RootInstanceGuid,
                    FileGuid = newAntState.Guid
                }));
                newBpbAsset.AddDependency(newAntState.Guid);
            }

            App.AssetManager.ModifyEbx(newBpb.Name, newBpbAsset);

            // Unlock asset
            // Fallback unlock structure when no base weapon selected
            if (baseUnlock == null)
                baseUnlock = App.AssetManager.GetEbxEntry("Gameplay/Weapons/AI/Zombie/AllStar/AI_AllStarJump/U_AI_AllStarJump");
            if (baseUnlock == null)
            {
                App.Logger.LogError("Could not find a base unlock to duplicate.");
                return;
            }

            EbxAssetEntry newUnlock = DuplicateAsset(baseUnlock, $"{mBlueprintDirectory}/U_{mWeaponName}", false);

            BundleEntry charBundle = App.AssetManager.EnumerateBundles()
                .FirstOrDefault(b => b.Name.Equals(
                    "Win32/gameplay/kits/bundling/characterssharedbundleasset",
                    StringComparison.OrdinalIgnoreCase));
            if (charBundle == null)
            {
                App.Logger.LogError("Could not find characterssharedbundleasset bundle!");
                return;
            }
            int unlockBundleId = App.AssetManager.GetBundleId(charBundle);
            newUnlock.AddedBundles.Clear();
            newUnlock.AddedBundles.Add(unlockBundleId);

            EbxAsset newUnlockAsset = App.AssetManager.GetEbx(newUnlock);
            dynamic newUnlockRoot = newUnlockAsset.RootObject;
            newUnlockRoot.WeaponBlueprintBundleReference.Name = bundleName.Replace("win32/", string.Empty);
            App.AssetManager.ModifyEbx(newUnlock.Name, newUnlockAsset);

            // AllWeaponAssets
            if (addToAllWeaponAssetsCheckBox.IsChecked == true)
            {
                try
                {
                    EbxAssetEntry allWeaponsEntry = App.AssetManager.GetEbxEntry("Gameplay/Weapons/Bundling/AllWeaponAssets");
                    if (allWeaponsEntry != null)
                    {
                        EbxAsset allWeaponsAsset = App.AssetManager.GetEbx(allWeaponsEntry);
                        dynamic allWeaponsRoot = allWeaponsAsset.RootObject;

                        bool alreadyAdded = false;
                        foreach (dynamic member in allWeaponsRoot.Assets)
                        {
                            if (member.External.FileGuid == newUnlock.Guid) { alreadyAdded = true; break; }
                        }

                        if (!alreadyAdded)
                        {
                            allWeaponsRoot.Assets.Add(new PointerRef(new EbxImportReference
                            {
                                FileGuid = newUnlock.Guid,
                                ClassGuid = newUnlockAsset.RootInstanceGuid
                            }));
                            allWeaponsAsset.AddDependency(newUnlock.Guid);
                            App.AssetManager.ModifyEbx(allWeaponsEntry.Name, allWeaponsAsset);
                            App.Logger.Log("Added {0} to AllWeaponAssets", newUnlock.Name);
                        }
                    }
                    else
                    {
                        App.Logger.LogWarning("AllWeaponAssets not found – unlock was not added to the global weapon list.");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Failed to add unlock to AllWeaponAssets: {ex.Message}");
                }
            }

            if (addToNetworkRegistryCheckBox.IsChecked == true)
            {
                try
                {
                    EbxAssetEntry netRegEntry = App.AssetManager.GetEbxEntry(
                        "gameplay/kits/bundling/characterssharedbundleasset_networkregistry_Win32");
                    if (netRegEntry != null)
                    {
                        EbxAsset netRegAsset = App.AssetManager.GetEbx(netRegEntry);
                        dynamic netRegRoot = netRegAsset.RootObject;

                        bool alreadyAdded = false;
                        foreach (dynamic member in netRegRoot.Objects)
                        {
                            if (member.External.FileGuid == newUnlock.Guid)
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }

                        if (!alreadyAdded)
                        {
                            netRegRoot.Objects.Add(new PointerRef(new EbxImportReference
                            {
                                FileGuid = newUnlock.Guid,
                                ClassGuid = newUnlockAsset.RootInstanceGuid
                            }));
                            netRegAsset.AddDependency(newUnlock.Guid);
                            App.AssetManager.ModifyEbx(netRegEntry.Name, netRegAsset);
                            App.Logger.Log("Added {0} to Characters Shared Network Registry", newUnlock.Name);
                        }
                    }
                    else
                    {
                        App.Logger.LogWarning("characterssharedbundleasset_networkregistry_Win32 not found.");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"Failed to add to Characters Shared Network Registry: {ex.Message}");
                }
            }

            RefreshEditorDataExplorer();
            App.Logger.Log("Successfully created weapon {0} in bundle {1}.", newWeaponBP.Name, bundleName);
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private EbxAssetEntry CreateAsset(string newName, Type newType)
        {
            EbxAsset newAsset = new EbxAsset(TypeLibrary.CreateObject(newType.Name));
            newAsset.SetFileGuid(Guid.NewGuid());
            dynamic obj = newAsset.RootObject;
            obj.Name = newName;
            AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
            obj.SetInstanceGuid(guid);
            EbxAssetEntry newEntry = App.AssetManager.AddEbx(newName, newAsset);
            newEntry.ModifiedEntry.DependentAssets.AddRange(newAsset.Dependencies);
            return newEntry;
        }

        private EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType = null)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);
            EbxAsset newAsset;
            if (createNew)
            {
                newAsset = new EbxAsset(TypeLibrary.CreateObject(newType.Name));
            }
            else
            {
                using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream(), EbxWriteFlags.DoNotSort | EbxWriteFlags.IncludeTransient))
                {
                    writer.WriteAsset(asset);
                    byte[] buf = writer.ToByteArray();
                    using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(buf)))
                        newAsset = reader.ReadAsset<EbxAsset>();
                }
            }
            newAsset.SetFileGuid(Guid.NewGuid());
            dynamic obj = newAsset.RootObject;
            obj.Name = newName;
            AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
            obj.SetInstanceGuid(guid);
            EbxAssetEntry newEntry = App.AssetManager.AddEbx(newName, newAsset);
            newEntry.AddedBundles.AddRange(entry.EnumerateBundles());
            newEntry.ModifiedEntry.DependentAssets.AddRange(newAsset.Dependencies);
            return newEntry;
        }

        private ResAssetEntry DuplicateRes(ResAssetEntry entry, string name, ResourceType resType)
        {
            if (App.AssetManager.GetResEntry(name) == null)
            {
                using (NativeReader reader = new NativeReader(App.AssetManager.GetRes(entry)))
                    return App.AssetManager.AddRes(name, resType, entry.ResMeta, reader.ReadToEnd(), entry.EnumerateBundles().ToArray());
            }
            return null;
        }

        private ChunkAssetEntry DuplicateChunk(ChunkAssetEntry entry, Texture texture = null)
        {
            byte[] random = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                while (true)
                {
                    rng.GetBytes(random);
                    random[15] |= 1;
                    if (App.AssetManager.GetChunkEntry(new Guid(random)) == null)
                        break;
                }
            }
            Guid newGuid;
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(entry)))
            {
                newGuid = App.AssetManager.AddChunk(reader.ReadToEnd(), new Guid(random), texture, entry.EnumerateBundles().ToArray());
            }
            return App.AssetManager.GetChunkEntry(newGuid);
        }

        private static void RefreshEditorDataExplorer()
        {
            try
            {
                var editorWindow = Frosty.Core.App.EditorWindow;
                if (editorWindow != null)
                {
                    editorWindow.DataExplorer.ItemsSource = App.AssetManager.EnumerateEbx();
                    editorWindow.DataExplorer.RefreshItems();
                }
            }
            catch { }
        }
    }

    public class FolderNode : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public ObservableCollection<FolderNode> Children { get; set; } = new ObservableCollection<FolderNode>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class DirectorySelectorWindow : FrostyWindow
    {
        private TreeView _treeView;
        private TextBox _searchBox;
        private ObservableCollection<FolderNode> _rootNodes = new ObservableCollection<FolderNode>();
        private List<string> _allDirs;
        public string SelectedPath { get; private set; }

        public DirectorySelectorWindow()
        {
            Title = "  Select Blueprint Directory";
            Width = 500; Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _allDirs = App.AssetManager.EnumerateEbx()
                .Select(entry => entry.Path.TrimStart('/'))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            _searchBox = new TextBox
            {
                Margin = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["ListBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"],
                BorderThickness = new Thickness(1)
            };
            _searchBox.TextChanged += (s, e) => BuildTree(_searchBox.Text);
            Grid.SetRow(_searchBox, 0); grid.Children.Add(_searchBox);

            _treeView = new TreeView
            {
                Margin = new Thickness(5),
                Background = (Brush)Application.Current.Resources["ListBackground"],
                BorderThickness = new Thickness(0),
                ItemsSource = _rootNodes
            };
            var template = new HierarchicalDataTemplate(typeof(FolderNode)) { ItemsSource = new Binding("Children") };
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.MarginProperty, new Thickness(0, 1, 0, 1));
            var imgFactory = new FrameworkElementFactory(typeof(Image));
            imgFactory.SetValue(Image.SourceProperty, new BitmapImage(new Uri("pack://application:,,,/FrostyEditor;component/Images/CloseFolder.png")));
            imgFactory.SetValue(Image.WidthProperty, 16.0);
            imgFactory.SetValue(Image.HeightProperty, 16.0);
            imgFactory.SetValue(Image.MarginProperty, new Thickness(0, 0, 4, 0));
            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            txtFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            txtFactory.SetValue(TextBlock.ForegroundProperty, (Brush)Application.Current.Resources["FontColor"]);
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(imgFactory); factory.AppendChild(txtFactory);
            template.VisualTree = factory;
            _treeView.ItemTemplate = template;
            var style = new Style(typeof(TreeViewItem), (Style)Application.Current.FindResource(typeof(TreeViewItem)));
            style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
            _treeView.ItemContainerStyle = style;
            _treeView.SelectedItemChanged += (s, e) =>
            {
                if (e.NewValue is FolderNode node) node.IsSelected = true;
                if (e.OldValue is FolderNode oldNode) oldNode.IsSelected = false;
            };
            Grid.SetRow(_treeView, 1); grid.Children.Add(_treeView);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var selectBtn = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 26,
                Margin = new Thickness(0, 0, 15, 0),
                IsEnabled = false,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            selectBtn.Click += (s, e) => { SelectedPath = ((FolderNode)_treeView.SelectedItem)?.FullPath; DialogResult = true; Close(); };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 26,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            _treeView.SelectedItemChanged += (s, e) => { selectBtn.IsEnabled = _treeView.SelectedItem is FolderNode; };
            btnPanel.Children.Add(selectBtn); btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

            Content = grid;
            BuildTree("");
        }

        private void BuildTree(string query)
        {
            _rootNodes.Clear();
            query = query?.ToLower() ?? "";
            var rootDict = new Dictionary<string, FolderNode>();
            foreach (var dir in _allDirs)
            {
                if (!string.IsNullOrWhiteSpace(query) && !dir.ToLower().Contains(query)) continue;
                var parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string cumulativePath = "";
                FolderNode parent = null;
                foreach (var part in parts)
                {
                    cumulativePath += part + "/";
                    if (!rootDict.TryGetValue(cumulativePath, out FolderNode node))
                    {
                        node = new FolderNode { Name = part, FullPath = cumulativePath.TrimEnd('/') };
                        rootDict[cumulativePath] = node;
                        if (parent == null) _rootNodes.Add(node); else parent.Children.Add(node);
                    }
                    parent = node;
                }
            }
        }
    }

    public class WeaponSelectorWindow : FrostyWindow
    {
        private ListView listView;
        private TextBox _searchBox;
        private ObservableCollection<EbxAssetEntry> _allWeapons;
        public string SelectedAssetPath { get; private set; }

        public WeaponSelectorWindow()
        {
            Title = "  Select Base Weapon";
            Width = 600; Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _allWeapons = new ObservableCollection<EbxAssetEntry>(
                App.AssetManager.EnumerateEbx("PVZCharacterWeaponAsset").OrderBy(e => e.Name));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            _searchBox = new TextBox
            {
                Margin = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["ListBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"],
                BorderThickness = new Thickness(1)
            };
            _searchBox.TextChanged += (s, e) => FilterTree(_searchBox.Text);
            Grid.SetRow(_searchBox, 0); grid.Children.Add(_searchBox);

            listView = new ListView
            {
                Margin = new Thickness(5),
                Background = (Brush)Application.Current.Resources["ListBackground"],
                BorderThickness = new Thickness(0),
                ItemsSource = _allWeapons
            };
            var view = new GridView();
            view.Columns.Add(new GridViewColumn { Header = "Weapon", Width = 580, DisplayMemberBinding = new Binding("Name") });
            listView.View = view;

            Grid.SetRow(listView, 1); grid.Children.Add(listView);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var selectBtn = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 26,
                Margin = new Thickness(0, 0, 15, 0),
                IsEnabled = false,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            selectBtn.Click += (s, e) =>
            {
                var selected = listView.SelectedItem as EbxAssetEntry;
                if (selected != null)
                {
                    SelectedAssetPath = selected.Name;
                    DialogResult = true;
                    Close();
                }
            };
            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 26,
                Background = (Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (Brush)Application.Current.Resources["FontColor"],
                BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            listView.SelectionChanged += (s, e) => { selectBtn.IsEnabled = listView.SelectedItem != null; };
            btnPanel.Children.Add(selectBtn); btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

            Content = grid;
        }

        private void FilterTree(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                listView.ItemsSource = _allWeapons;
                return;
            }
            query = query.ToLower();
            listView.ItemsSource = _allWeapons.Where(w => w.Name.ToLower().Contains(query)).ToList();
        }
        public class AntStateDirSelectorWindow : FrostyWindow
        {
            private const string RootPath = "animations/antanimations/gameplay/weapons";
            private TreeView _treeView;
            private TextBox _searchBox;
            private ObservableCollection<FolderNode> _rootNodes = new ObservableCollection<FolderNode>();
            private List<string> _allDirs;
            public string SelectedPath { get; private set; }

            public AntStateDirSelectorWindow()
            {
                Title = "  Select AntState Directory";
                Width = 500; Height = 600;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                _allDirs = App.AssetManager.EnumerateEbx()
                    .Select(e => e.Path.TrimStart('/').ToLower())
                    .Where(p => p.StartsWith(RootPath))
                    .Select(p => p.Substring(RootPath.Length).TrimStart('/'))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

                _searchBox = new TextBox
                {
                    Margin = new Thickness(5),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = (Brush)Application.Current.Resources["ListBackground"],
                    Foreground = (Brush)Application.Current.Resources["FontColor"],
                    BorderBrush = (Brush)Application.Current.Resources["ControlBackground"],
                    BorderThickness = new Thickness(1)
                };
                _searchBox.TextChanged += (s, e) => BuildTree(_searchBox.Text);
                Grid.SetRow(_searchBox, 0); grid.Children.Add(_searchBox);

                _treeView = new TreeView
                {
                    Margin = new Thickness(5),
                    Background = (Brush)Application.Current.Resources["ListBackground"],
                    BorderThickness = new Thickness(0),
                    ItemsSource = _rootNodes
                };
                var template = new HierarchicalDataTemplate(typeof(FolderNode)) { ItemsSource = new Binding("Children") };
                var factory = new FrameworkElementFactory(typeof(StackPanel));
                factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                factory.SetValue(StackPanel.MarginProperty, new Thickness(0, 1, 0, 1));
                var imgFactory = new FrameworkElementFactory(typeof(Image));
                imgFactory.SetValue(Image.SourceProperty, new BitmapImage(new Uri("pack://application:,,,/FrostyEditor;component/Images/CloseFolder.png")));
                imgFactory.SetValue(Image.WidthProperty, 16.0);
                imgFactory.SetValue(Image.HeightProperty, 16.0);
                imgFactory.SetValue(Image.MarginProperty, new Thickness(0, 0, 4, 0));
                var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
                txtFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
                txtFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
                txtFactory.SetValue(TextBlock.ForegroundProperty, (Brush)Application.Current.Resources["FontColor"]);
                txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                factory.AppendChild(imgFactory); factory.AppendChild(txtFactory);
                template.VisualTree = factory;
                _treeView.ItemTemplate = template;
                var style = new Style(typeof(TreeViewItem), (Style)Application.Current.FindResource(typeof(TreeViewItem)));
                style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }));
                _treeView.ItemContainerStyle = style;
                Grid.SetRow(_treeView, 1); grid.Children.Add(_treeView);

                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var selectBtn = new Button
                {
                    Content = "Select",
                    Width = 80,
                    Height = 26,
                    Margin = new Thickness(0, 0, 15, 0),
                    IsEnabled = false,
                    Background = (Brush)Application.Current.Resources["ControlBackground"],
                    Foreground = (Brush)Application.Current.Resources["FontColor"],
                    BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
                };
                var cancelBtn = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 26,
                    Background = (Brush)Application.Current.Resources["ControlBackground"],
                    Foreground = (Brush)Application.Current.Resources["FontColor"],
                    BorderBrush = (Brush)Application.Current.Resources["ControlBackground"]
                };
                selectBtn.Click += (s, e) =>
                {
                    SelectedPath = ((FolderNode)_treeView.SelectedItem)?.FullPath;
                    DialogResult = true;
                    Close();
                };
                cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
                _treeView.SelectedItemChanged += (s, e) => { selectBtn.IsEnabled = _treeView.SelectedItem is FolderNode; };
                btnPanel.Children.Add(selectBtn); btnPanel.Children.Add(cancelBtn);
                Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

                Content = grid;
                BuildTree("");
            }

            private void BuildTree(string query)
            {
                _rootNodes.Clear();
                query = query?.ToLower() ?? "";
                var rootDict = new Dictionary<string, FolderNode>();
                foreach (var dir in _allDirs)
                {
                    if (!string.IsNullOrWhiteSpace(query) && !dir.ToLower().Contains(query)) continue;
                    var parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    string cumulativePath = "";
                    FolderNode parent = null;
                    foreach (var part in parts)
                    {
                        cumulativePath += part + "/";
                        if (!rootDict.TryGetValue(cumulativePath, out FolderNode node))
                        {
                            node = new FolderNode { Name = part, FullPath = cumulativePath.TrimEnd('/') };
                            rootDict[cumulativePath] = node;
                            if (parent == null) _rootNodes.Add(node); else parent.Children.Add(node);
                        }
                        parent = node;
                    }
                }
            }
        }
    }
}