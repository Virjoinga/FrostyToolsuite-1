using AssetBankPlugin.Ant;
using AssetBankPlugin.Ant.VBR;
using AssetBankPlugin.Export;
using AssetBankPlugin.Formats;
using AssetBankPlugin.Render;
using AssetBankPlugin.Windows;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Screens;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using MeshSetPlugin.Render;
using MeshSetPlugin.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static AssetBankPlugin.AntStateAssetEditor;

namespace AssetBankPlugin
{
    public class AntTypeGroup
    {
        public string TypeName { get; set; }
        public int ItemCount => Assets.Count;
        public ObservableCollection<AntAssetViewModel> Assets { get; set; } = new ObservableCollection<AntAssetViewModel>();
    }

    public class AntAssetViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public object AssetInstance { get; set; }
        public AntStateAssetEditor ParentEditor { get; set; }

        private bool _isNew;
        public bool IsNew
        {
            get => _isNew;
            set { _isNew = value; OnPropertyChanged(nameof(IsNew)); OnPropertyChanged(nameof(NameFontWeight)); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(RevertVisibility)); }
        }

        private bool _isModified;
        public bool IsModified
        {
            get => _isModified;
            set { _isModified = value; OnPropertyChanged(nameof(IsModified)); OnPropertyChanged(nameof(NameFontWeight)); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(RevertVisibility)); }
        }

        private bool _isUnsaved;
        public bool IsUnsaved
        {
            get => _isUnsaved;
            set { _isUnsaved = value; OnPropertyChanged(nameof(IsUnsaved)); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(RevertVisibility)); }
        }

        public string DisplayName => IsUnsaved ? Name + " *" : Name;

        public System.Windows.FontWeight NameFontWeight
            => (IsNew || IsModified) ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;

        public void MarkModified()
        {
            IsModified = true;
            IsUnsaved = true;
        }

        private bool _isSelectedForExport;
        public bool IsSelectedForExport
        {
            get => _isSelectedForExport;
            set
            {
                _isSelectedForExport = value;
                OnPropertyChanged(nameof(IsSelectedForExport));
                ParentEditor?.NotifyExportStatus();
            }
        }

        private bool _showCheckbox;
        public Visibility CheckboxVisibility => (_showCheckbox && AssetInstance is AnimationAsset) ? Visibility.Visible : Visibility.Collapsed;

        public void SetBulkMode(bool show)
        {
            _showCheckbox = show;
            if (!show) IsSelectedForExport = false;
            OnPropertyChanged(nameof(CheckboxVisibility));
        }

        public ICommand CopyGuidCommand => new RelayCommand((o) => Clipboard.SetText(Id.ToString()));
        public Visibility ExportVisibility => (AssetInstance is AnimationAsset) ? Visibility.Visible : Visibility.Collapsed;
        public ICommand ExportCommand => new RelayCommand((o) => ExportAsset());

        public Visibility ImportAnimVisibility => (AssetInstance is VbrAnimationAsset) ? Visibility.Visible : Visibility.Collapsed;
        public ICommand ImportAnimationCommand => new RelayCommand((o) => ParentEditor?.ImportAnimation(this));

        public Visibility DuplicateVisibility => (AssetInstance is AntAsset) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowTreeVisibility => (AssetInstance is AntAsset) ? Visibility.Visible : Visibility.Collapsed;
        public ICommand DuplicateCommand => new RelayCommand((o) => ParentEditor?.DuplicateAsset(this));
        public ICommand ShowTreeCommand => new RelayCommand((o) => ParentEditor?.ShowAssetTree(this));

        public Visibility EditAnimAssetsVisibility => (AssetInstance is AntAsset ant && ant.AssetType == "ActorControllerAsset") ? Visibility.Visible : Visibility.Collapsed;
        public ICommand EditAnimAssetsCommand => new RelayCommand((o) => ParentEditor?.EditAnimAssets(this));

        public Visibility RevertVisibility => (ParentEditor != null && (IsModified || IsNew || IsUnsaved)) ? Visibility.Visible : Visibility.Collapsed;
        public ICommand RevertCommand => new RelayCommand((o) => ParentEditor?.RevertAsset(this));

        public Visibility TestCompressorVisibility => (AssetInstance is VbrAnimationAsset) ? Visibility.Visible : Visibility.Collapsed;
        public ICommand TestCompressorCommand => new RelayCommand((o) => ParentEditor?.TestCompressor(this));

        public Visibility ExportRigMeshVisibility => (AssetInstance is RigAsset) ? Visibility.Visible : Visibility.Collapsed;
        public ICommand ExportRigMeshCommand => new RelayCommand((o) => ParentEditor?.ExportRigWithMesh(this));

        private void ExportAsset()
        {
            if (!(AssetInstance is AnimationAsset anim)) return;
            if (ParentEditor == null || ParentEditor.BankBytes == null) return;

            // debugshit
            bool useRigPickerMenu = false;

            RigAsset selectedBankRig = null;
            AnimationOptions opt = null;

            if (useRigPickerMenu)
            {
                var rigsInBank = new List<RigAsset>();
                foreach (var group in ParentEditor._masterGroups)
                {
                    if (group.TypeName == "RigAsset")
                    {
                        foreach (var vm in group.Assets)
                        {
                            if (vm.AssetInstance is RigAsset rig)
                                rigsInBank.Add(rig);
                        }
                    }
                }

                if (rigsInBank.Count == 0)
                {
                    FrostyMessageBox.Show("No RigAssets found in this bank to extract a skeleton from.", "Export Failed");
                    return;
                }

                var picker = new BankRigPickerDialog(rigsInBank) { Owner = Window.GetWindow(ParentEditor) };
                if (picker.ShowDialog() != true || picker.SelectedRig == null)
                {
                    return;
                }
                selectedBankRig = picker.SelectedRig;
            }
            else
            {
                opt = new AnimationOptions();
                opt.Load();
                if (string.IsNullOrEmpty(opt.ExportSkeletonAsset))
                {
                    FrostyMessageBox.Show("Please set an Export Skeleton in Options first.", "Missing Skeleton");
                    return;
                }
            }

            string cleanName = Name;
            int idx = cleanName.LastIndexOf(" (");
            if (idx > 0 && cleanName.EndsWith(")"))
                cleanName = cleanName.Substring(0, idx);

            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Animation", "*.cast (Cast)|*.cast|*.seanim (SEAnim)|*.seanim", "Cast", cleanName);
            if (sfd.ShowDialog())
            {
                string exportDirectory = Path.GetDirectoryName(sfd.FileName);
                string extension = Path.GetExtension(sfd.FileName).ToLower();
                string assetName = cleanName;

                FrostyTaskWindow.Show("Exporting Animation", "", (task) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            InternalSkeleton skeleton = null;

                            if (useRigPickerMenu && selectedBankRig != null)
                            {
                                skeleton = AntRigExporter.BuildInternalSkeletonFromRig(selectedBankRig);
                            }
                            else
                            {
                                var skelEntry = App.AssetManager.GetEbxEntry(opt.ExportSkeletonAsset);
                                var skelEbx = App.AssetManager.GetEbx(skelEntry);
                                skeleton = SkeletonAssetExport.ConvertToInternal(skelEbx.RootObject);
                            }

                            if (skeleton == null)
                            {
                                App.Logger.LogError($"[AntStateEditor] Failed to construct skeleton for animation export.");
                                return;
                            }

                            anim.Name = assetName;
                            anim.Channels = anim.GetChannels(anim.ChannelToDofAsset);
                            var intern = anim.ConvertToInternal();

                            if (intern != null)
                            {
                                if (extension == ".cast")
                                {
                                    new AnimationExporterCAST().Export(intern, skeleton, exportDirectory);
                                }
                                else
                                {
                                    new AnimationExporterSEANIM().Export(intern, skeleton, exportDirectory);
                                }
                                App.Logger.Log($"[AntStateEditor] Successfully exported {assetName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogError($"[AntStateEditor] Failed to export {assetName}: {ex.Message}");
                        }
                    });
                });
            }
        }

        private ObservableCollection<AntPropertyViewModel> _properties;
        public ObservableCollection<AntPropertyViewModel> Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = new ObservableCollection<AntPropertyViewModel>();
                    if (AssetInstance != null)
                    {
                        if (AssetInstance is GenericAntAsset genAsset && genAsset.RawData != null)
                        {
                            foreach (var kvp in genAsset.RawData)
                            {
                                if (kvp.Key == "__name" || kvp.Key == "__guid" || kvp.Key == "__key") continue;

                                var vm = new AntPropertyViewModel(kvp.Key, kvp.Value, null) { IsEditable = true };
                                vm.ModifiedCallback = MarkModified;
                                _properties.Add(vm);
                            }
                        }
                        else
                        {
                            Type type = AssetInstance.GetType();

                            bool editable = true;

                            foreach (var p in ReflectionHelper.GetAllProperties(type))
                            {
                                if (p.Name == "Name" || p.Name == "ID" || p.Name == "Bank" || p.Name == "AssetType" || p.Name == "RawData") continue;
                                try
                                {
                                    var vm = new AntPropertyViewModel(p.Name, p.GetValue(AssetInstance), null);
                                    if (editable) { vm.IsEditable = true; vm.ModifiedCallback = MarkModified; }
                                    _properties.Add(vm);
                                }
                                catch { }
                            }

                            foreach (var f in ReflectionHelper.GetAllFields(type))
                            {
                                if (f.Name.Contains("<") || f.Name.Contains("k__BackingField")) continue;
                                try
                                {
                                    var vm = new AntPropertyViewModel(f.Name, f.GetValue(AssetInstance), null);
                                    if (editable) { vm.IsEditable = true; vm.ModifiedCallback = MarkModified; }
                                    _properties.Add(vm);
                                }
                                catch { }
                            }
                        }
                    }
                }
                return _properties;
            }
        }

        public void ResetProperties()
        {
            _properties = null;
            OnPropertyChanged(nameof(Properties));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class AntPropertyViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string TypeStr { get; set; }
        public string ValueStr { get; set; }
        public object ValueObj { get; set; }

        public bool IsEditable { get; set; }
        public bool IsStructureModified { get; set; }
        public AntPropertyViewModel Parent { get; }
        private string _editedValue;

        public Action ModifiedCallback { get; set; }

        public string EditedValue
        {
            get => _editedValue ?? ValueStr;
            set
            {
                if (_editedValue == value) return;
                _editedValue = value;
                ModifiedCallback?.Invoke();
            }
        }
        public bool WasEdited => IsEditable && _editedValue != null && _editedValue != ValueStr;
        public Visibility EditableVisibility => IsEditable ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ReadOnlyVisibility => IsEditable ? Visibility.Collapsed : Visibility.Visible;

        public bool IsArrayElement => Parent != null && (Parent.ValueObj is Array || Parent.ValueObj is IList);
        public Visibility ArrayElementVisibility => IsArrayElement ? Visibility.Visible : Visibility.Collapsed;

        public ICommand InsertBeforeCommand => new RelayCommand((o) => Parent?.InsertArrayElement(this, 0));
        public ICommand InsertAfterCommand => new RelayCommand((o) => Parent?.InsertArrayElement(this, 1));
        public ICommand DeleteCommand => new RelayCommand((o) => Parent?.DeleteArrayElement(this));

        public ICommand CopyValueCommand => new RelayCommand((o) =>
        {
            if (!string.IsNullOrEmpty(ValueStr))
            {
                try { Clipboard.SetText(ValueStr); } catch { }
            }
        });

        public AntPropertyViewModel(string name, object val, AntPropertyViewModel parent = null)
        {
            Name = name;
            ValueObj = val;
            Parent = parent;
            TypeStr = val != null ? GetFriendlyTypeName(val.GetType()) : "Null";
            ValueStr = GetString(val);
        }

        public void InsertArrayElement(AntPropertyViewModel child, int offset)
        {
            if (ValueObj == null) return;

            int index = Children.IndexOf(child);
            if (index < 0) return;

            int insertIndex = index + offset;

            object newElement = null;
            if (child.ValueObj != null)
            {
                newElement = AntStateAssetEditor.DeepCopyRawData(child.ValueObj);
            }
            else
            {
                Type elemType = typeof(object);
                if (ValueObj is Array arr)
                    elemType = arr.GetType().GetElementType();
                else if (ValueObj.GetType().IsGenericType && ValueObj.GetType().GetGenericTypeDefinition() == typeof(List<>))
                    elemType = ValueObj.GetType().GetGenericArguments()[0];

                if (elemType == typeof(string)) newElement = "";
                else if (elemType == typeof(Guid)) newElement = Guid.Empty;
                else if (elemType.IsValueType) newElement = Activator.CreateInstance(elemType);
                else newElement = new Dictionary<string, object>();
            }

            if (ValueObj is Array oldArr)
            {
                Type elemType = oldArr.GetType().GetElementType();
                var newArr = Array.CreateInstance(elemType, oldArr.Length + 1);
                for (int i = 0, j = 0; i < oldArr.Length + 1; i++)
                {
                    if (i == insertIndex)
                        newArr.SetValue(newElement, i);
                    else
                        newArr.SetValue(oldArr.GetValue(j++), i);
                }
                ValueObj = newArr;
            }
            else if (ValueObj is IList list)
            {
                list.Insert(insertIndex, newElement);
            }

            IsStructureModified = true;
            RebuildArrayChildren();
            ModifiedCallback?.Invoke();
        }

        public void DeleteArrayElement(AntPropertyViewModel child)
        {
            if (ValueObj == null) return;

            int index = Children.IndexOf(child);
            if (index < 0) return;

            if (ValueObj is Array oldArr)
            {
                Type elemType = oldArr.GetType().GetElementType();
                var newArr = Array.CreateInstance(elemType, oldArr.Length - 1);
                for (int i = 0, j = 0; i < oldArr.Length; i++)
                {
                    if (i == index) continue;
                    newArr.SetValue(oldArr.GetValue(i), j++);
                }
                ValueObj = newArr;
            }
            else if (ValueObj is IList list)
            {
                list.RemoveAt(index);
            }

            IsStructureModified = true;
            RebuildArrayChildren();
            ModifiedCallback?.Invoke();
        }

        private void RebuildArrayChildren()
        {
            if (_children == null) return;
            _children.Clear();

            if (ValueObj is IEnumerable enumerable)
            {
                int i = 0;
                foreach (var item in enumerable)
                {
                    var vm = new AntPropertyViewModel($"[{i++}]", item, this) { IsEditable = IsEditable };
                    vm.ModifiedCallback = ModifiedCallback;
                    _children.Add(vm);
                }
            }
            ValueStr = GetString(ValueObj);
            _editedValue = ValueStr;
            OnPropertyChanged(nameof(Children));
            OnPropertyChanged(nameof(ValueStr));
            OnPropertyChanged(nameof(EditedValue));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private string GetFriendlyTypeName(Type t)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return $"List of {t.GetGenericArguments()[0].Name}";
            return t.Name;
        }

        public string GetString(object obj)
        {
            if (obj == null) return "null";
            Type t = obj.GetType();

            if (obj is string s)
            {
                if (s.Length == 16 && ulong.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out ulong keyVal))
                {
                    return KeyToGuid(keyVal).ToString();
                }
                return s;
            }

            if (obj is ulong ul && (Name.Contains("Anim") || Name.Contains("Target") || Name.Contains("Key") || Name.Contains("Asset") || Name.Contains("TagCollectionSet")))
            {
                return KeyToGuid(ul).ToString();
            }

            if (t.IsPrimitive || t == typeof(Guid) || t.IsEnum)
                return obj.ToString();

            if (obj is Dictionary<string, object>)
                return "[Struct]";

            if (obj is IEnumerable enumerable)
            {
                int count = 0;
                var enumerator = enumerable.GetEnumerator();
                while (enumerator.MoveNext()) count++;
                return $"[{count} Items]";
            }

            return $"[{t.Name}]";
        }

        public static Guid KeyToGuid(ulong key)
        {
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(key).CopyTo(bytes, 0);
            return new Guid(bytes);
        }

        private ObservableCollection<AntPropertyViewModel> _children;
        public ObservableCollection<AntPropertyViewModel> Children
        {
            get
            {
                if (_children == null)
                {
                    _children = new ObservableCollection<AntPropertyViewModel>();
                    if (ValueObj != null)
                    {
                        Type t = ValueObj.GetType();
                        if (!(t.IsPrimitive || t == typeof(string) || t == typeof(Guid) || t.IsEnum))
                        {
                            if (ValueObj is Dictionary<string, object> dict)
                            {
                                foreach (var kvp in dict)
                                {
                                    if (kvp.Key == "__typeHash" || kvp.Key == "__guid" || kvp.Key == "__key" || kvp.Key == "__name") continue;
                                    var vm = new AntPropertyViewModel(kvp.Key, kvp.Value, this) { IsEditable = true };
                                    vm.ModifiedCallback = ModifiedCallback;
                                    _children.Add(vm);
                                }
                            }
                            else if (ValueObj is IEnumerable enumerable)
                            {
                                int i = 0;
                                foreach (var item in enumerable)
                                {
                                    var vm = new AntPropertyViewModel($"[{i++}]", item, this) { IsEditable = true };
                                    vm.ModifiedCallback = ModifiedCallback;
                                    _children.Add(vm);
                                }
                            }
                            else
                            {
                                foreach (var p in ReflectionHelper.GetAllProperties(t))
                                {
                                    try
                                    {
                                        var vm = new AntPropertyViewModel(p.Name, p.GetValue(ValueObj), this) { IsEditable = true };
                                        vm.ModifiedCallback = ModifiedCallback;
                                        _children.Add(vm);
                                    }
                                    catch { }
                                }
                                foreach (var f in ReflectionHelper.GetAllFields(t))
                                {
                                    if (f.Name.Contains("<") || f.Name.Contains("k__BackingField")) continue;
                                    try
                                    {
                                        var vm = new AntPropertyViewModel(f.Name, f.GetValue(ValueObj), this) { IsEditable = true };
                                        vm.ModifiedCallback = ModifiedCallback;
                                        _children.Add(vm);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
                return _children;
            }
        }
    }

    public class LoadedMeshViewModel : INotifyPropertyChanged
    {
        private readonly AntStateAssetEditor _editor;

        public int InternalIndex { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                _editor?.OnMeshVisibilityChanged();
            }
        }

        public ICommand UseSelectedCommand => new RelayCommand(_ => _editor?.ReplaceMeshFromExplorer(InternalIndex));
        public ICommand RemoveCommand => new RelayCommand(_ => _editor?.RemoveMeshAt(InternalIndex));

        public LoadedMeshViewModel(AntStateAssetEditor editor) => _editor = editor;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public static class ReflectionHelper
    {
        public static IEnumerable<FieldInfo> GetAllFields(Type t)
        {
            if (t == null) return Enumerable.Empty<FieldInfo>();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            return t.GetFields(flags).Concat(GetAllFields(t.BaseType));
        }

        public static IEnumerable<PropertyInfo> GetAllProperties(Type t)
        {
            if (t == null) return Enumerable.Empty<PropertyInfo>();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            return t.GetProperties(flags).Concat(GetAllProperties(t.BaseType));
        }
    }

    [TemplatePart(Name = "PART_SearchBox", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_FilterMenu", Type = typeof(AssetFilterMenu))]
    [TemplatePart(Name = "PART_AssetTreeView", Type = typeof(TreeView))]
    [TemplatePart(Name = "PART_LoadingOverlay", Type = typeof(Border))]
    [TemplatePart(Name = "PART_LoadingText", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_BulkToggle", Type = typeof(System.Windows.Controls.Primitives.ToggleButton))]
    [TemplatePart(Name = "PART_ExportBulkButton", Type = typeof(Button))]
    [TemplatePart(Name = "PART_AddRefButton", Type = typeof(Button))]
    [TemplatePart(Name = "PART_Viewport", Type = typeof(FrostyViewport))]
    [TemplatePart(Name = "PART_SkeletonPath", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_SkeletonSetDefault", Type = typeof(Button))]
    [TemplatePart(Name = "PART_SkeletonOverrideBtn", Type = typeof(System.Windows.Controls.Primitives.ToggleButton))]
    [TemplatePart(Name = "PART_MeshPath", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_MeshLoad", Type = typeof(Button))]
    [TemplatePart(Name = "PART_MeshUseSelected", Type = typeof(Button))]
    [TemplatePart(Name = "PART_RestartBtn", Type = typeof(Button))]
    [TemplatePart(Name = "PART_PlayPauseBtn", Type = typeof(System.Windows.Controls.Primitives.ToggleButton))]
    [TemplatePart(Name = "PART_TimelineSlider", Type = typeof(Slider))]
    [TemplatePart(Name = "PART_FrameLabel", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_TotalFramesLabel", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_SpeedSlider", Type = typeof(Slider))]
    [TemplatePart(Name = "PART_SpeedLabel", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_LodCombo", Type = typeof(ComboBox))]
    [TemplatePart(Name = "PART_RenderModeCombo", Type = typeof(ComboBox))]
    [TemplatePart(Name = "PART_MeshListPopup", Type = typeof(System.Windows.Controls.Primitives.Popup))]
    [TemplatePart(Name = "PART_SaveButton", Type = typeof(Button))]
    public partial class AntStateAssetEditor : FrostyAssetEditor, INotifyPropertyChanged
    {
        private TextBox m_searchBox;
        private AssetFilterMenu m_filterMenu;
        private TreeView m_assetTreeView;
        private Border m_loadingOverlay;
        private TextBlock m_loadingText;
        private System.Windows.Controls.Primitives.ToggleButton m_bulkToggle;

        private Bank _bank;
        private byte[] _bankBytes;
        private ResAssetEntry _bankResEntry;
        private ChunkAssetEntry _bankChunkEntry;
        private bool _bankIsBigEndian;

        private FrostyViewport m_viewport;
        private readonly AntAnimPreviewScreen m_screen = new AntAnimPreviewScreen();
        private TextBox m_meshPathBox;
        private bool _meshLoaded = false;
        private string _currentSkeletonPath = "";
        private bool _suppressSkeletonToggle = false;
        private bool _skeletonOverrideActive = false;

        private Button m_restartBtn;
        private System.Windows.Controls.Primitives.ToggleButton m_playPauseBtn;
        private Slider m_timelineSlider;
        private TextBlock m_frameLabel;
        private TextBlock m_totalFramesLabel;
        private Slider m_speedSlider;
        private TextBlock m_speedLabel;
        private ComboBox m_lodCombo;
        private ComboBox m_renderModeCombo;

        private System.Windows.Controls.Primitives.Popup _meshListPopup;

        private sealed class LoadedMeshEntry
        {
            public string DisplayName;
            public MeshSet MeshSet;
            public MeshMaterialCollection Materials;
            public MeshRenderSkeleton PerMeshSkeleton;
            public EbxAssetEntry SourceEntry;
            public PointerRef VariationRef;
            public string BpbName;
        }
        private readonly List<LoadedMeshEntry> _loadedMeshData = new List<LoadedMeshEntry>();
        public ObservableCollection<LoadedMeshViewModel> LoadedMeshes { get; }
            = new ObservableCollection<LoadedMeshViewModel>();

        private AnimationAsset _currentPreviewAsset;
        private string _currentPreviewName;
        private bool _isProgrammaticSliderUpdate = false;

        internal List<AntTypeGroup> _masterGroups = new List<AntTypeGroup>();
        private ObservableCollection<AntTypeGroup> _filteredGroups = new ObservableCollection<AntTypeGroup>();

        private AntAssetViewModel _selectedAsset;
        private bool _isBulkMode = false;
        private AntAssetViewModel _shiftAnchor = null;

        public bool IsExportEnabled
        {
            get
            {
                if (_isBulkMode)
                    return _masterGroups.SelectMany(g => g.Assets).Any(a => a.IsSelectedForExport);

                return _selectedAsset != null && _selectedAsset.AssetInstance is AnimationAsset;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyExportStatus() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExportEnabled)));

        static AntStateAssetEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AntStateAssetEditor), new FrameworkPropertyMetadata(typeof(AntStateAssetEditor)));
        }

        public AntStateAssetEditor(ILogger inLogger) : base(inLogger) { }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            m_searchBox = GetTemplateChild("PART_SearchBox") as TextBox;
            m_filterMenu = GetTemplateChild("PART_FilterMenu") as AssetFilterMenu;
            var filterBtn = GetTemplateChild("PART_FilterButton") as System.Windows.Controls.Primitives.ToggleButton;
            var filterPopup = GetTemplateChild("PART_FilterPopup") as System.Windows.Controls.Primitives.Popup;
            m_assetTreeView = GetTemplateChild("PART_AssetTreeView") as TreeView;
            m_loadingOverlay = GetTemplateChild("PART_LoadingOverlay") as Border;
            m_loadingText = GetTemplateChild("PART_LoadingText") as TextBlock;
            m_bulkToggle = GetTemplateChild("PART_BulkToggle") as System.Windows.Controls.Primitives.ToggleButton;
            var exportBulkBtn = GetTemplateChild("PART_ExportBulkButton") as Button;
            var addRefBtn = GetTemplateChild("PART_AddRefButton") as Button;

            m_viewport = GetTemplateChild("PART_Viewport") as FrostyViewport;
            m_meshPathBox = GetTemplateChild("PART_MeshPath") as TextBox;
            var meshLoadBtn = GetTemplateChild("PART_MeshLoad") as Button;
            var skelOverride = GetTemplateChild("PART_SkeletonOverrideBtn") as System.Windows.Controls.Primitives.ToggleButton;
            var meshUseSel = GetTemplateChild("PART_MeshUseSelected") as Button;

            var opt0 = new AnimationOptions();
            opt0.Load();
            _currentSkeletonPath = opt0.ExportSkeletonAsset ?? "";

            if (filterBtn != null && filterPopup != null)
            {
                DateTime lastPopupClose = DateTime.MinValue;

                filterPopup.Closed += (s, e) =>
                {
                    lastPopupClose = DateTime.Now;
                    filterBtn.IsChecked = false;
                };

                filterBtn.Click += (s, e) =>
                {
                    if ((DateTime.Now - lastPopupClose).TotalMilliseconds < 150)
                    {
                        filterBtn.IsChecked = false;
                        filterPopup.IsOpen = false;
                    }
                    else
                    {
                        filterPopup.IsOpen = filterBtn.IsChecked == true;
                    }
                };
            }

            if (skelOverride != null)
            {
                skelOverride.Checked += async (s, e) =>
                {
                    if (_suppressSkeletonToggle) return;
                    var picker = new SkeletonPickerDialog { Owner = Window.GetWindow(this) };
                    if (picker.ShowDialog() == true && picker.SelectedEntry != null)
                    {
                        _currentSkeletonPath = picker.SelectedEntry.Name;
                        _skeletonOverrideActive = true;
                        var opt = new AnimationOptions();
                        opt.Load();
                        opt.ExportSkeletonAsset = _currentSkeletonPath;
                        opt.Save();

                        if (_currentPreviewAsset != null)
                            await LoadPreviewAsync(_currentPreviewAsset, _currentPreviewName);

                        if (m_meshPathBox != null && !string.IsNullOrEmpty(m_meshPathBox.Text))
                            await LoadMeshAsync(m_meshPathBox.Text.Trim());
                    }
                    else
                    {
                        _suppressSkeletonToggle = true;
                        skelOverride.IsChecked = false;
                        _suppressSkeletonToggle = false;
                    }
                };

                skelOverride.Unchecked += async (s, e) =>
                {
                    if (_suppressSkeletonToggle) return;
                    _skeletonOverrideActive = false;
                    _currentSkeletonPath = "";

                    if (_currentPreviewAsset != null)
                        await LoadPreviewAsync(_currentPreviewAsset, _currentPreviewName);

                    if (m_meshPathBox != null && !string.IsNullOrEmpty(m_meshPathBox.Text))
                        await LoadMeshAsync(m_meshPathBox.Text.Trim());
                };
            }

            if (meshLoadBtn != null)
                meshLoadBtn.Click += (s, e) => _ = LoadMeshAsync(m_meshPathBox?.Text?.Trim() ?? "");

            if (meshUseSel != null)
                meshUseSel.Click += (s, e) =>
                {
                    try
                    {
                        dynamic mainWin = App.EditorWindow;
                        EbxAssetEntry entry = mainWin?.DataExplorer?.SelectedAsset as EbxAssetEntry;
                        if (entry != null && m_meshPathBox != null)
                        {
                            m_meshPathBox.Text = entry.Name;
                            App.Logger.Log($"[AntStateEditor] Mesh path set to: {entry.Name}");
                        }
                        else
                        {
                            App.Logger.LogWarning("[AntStateEditor] No asset selected in data explorer.");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogError($"[AntStateEditor] Could not read data explorer selection: {ex.Message}");
                    }
                };

            if (m_bulkToggle != null)
            {
                m_bulkToggle.Checked += (s, e) => { _isBulkMode = true; ToggleBulkMode(true); NotifyExportStatus(); };
                m_bulkToggle.Unchecked += (s, e) => { _isBulkMode = false; ToggleBulkMode(false); NotifyExportStatus(); };
            }

            if (exportBulkBtn != null) exportBulkBtn.Click += (s, e) => ExportSelected();
            if (addRefBtn != null) addRefBtn.Click += (s, e) => OpenReferenceSelector();

            var saveBtn = GetTemplateChild("PART_SaveButton") as Button;
            if (saveBtn != null)
                saveBtn.Click += (s, e) => SaveAllUnsaved();

            if (m_assetTreeView != null)
            {
                m_assetTreeView.ItemsSource = _filteredGroups;
                m_assetTreeView.SelectedItemChanged += (s, e) =>
                {
                    _selectedAsset = e.NewValue as AntAssetViewModel;
                    NotifyExportStatus();

                    if (e.NewValue is AntAssetViewModel vm && vm.AssetInstance is AnimationAsset animAsset)
                    {
                        string cleanName = vm.Name;
                        int parenIdx = cleanName.LastIndexOf(" (");
                        if (parenIdx > 0 && cleanName.EndsWith(")"))
                            cleanName = cleanName.Substring(0, parenIdx);

                        _currentPreviewAsset = animAsset;
                        _currentPreviewName = cleanName;
                        _ = LoadPreviewAsync(animAsset, cleanName);
                    }
                };
                m_assetTreeView.PreviewMouseLeftButtonDown += OnTreeViewPreviewMouseDown;
            }

            if (m_searchBox != null)
                m_searchBox.TextChanged += (s, e) => ApplySearchFilter(m_searchBox.Text);

            if (m_filterMenu != null)
            {
                m_filterMenu.FilterChanged += (s, e) => ApplySearchFilter(m_searchBox?.Text);
            }

            if (m_viewport != null)
                m_viewport.Screen = m_screen;

            _meshListPopup = GetTemplateChild("PART_MeshListPopup") as System.Windows.Controls.Primitives.Popup;
            if (_meshListPopup != null)
                _meshListPopup.DataContext = this;

            var skeletonBtn = GetTemplateChild("PART_PreviewSkeletonBtn") as ToggleButton;
            if (skeletonBtn != null)
            {
                skeletonBtn.Checked += (s, e) => m_screen.ShowSkeleton = true;
                skeletonBtn.Unchecked += (s, e) => m_screen.ShowSkeleton = false;
            }

            m_restartBtn = GetTemplateChild("PART_RestartBtn") as Button;
            m_playPauseBtn = GetTemplateChild("PART_PlayPauseBtn") as System.Windows.Controls.Primitives.ToggleButton;
            m_timelineSlider = GetTemplateChild("PART_TimelineSlider") as Slider;
            m_frameLabel = GetTemplateChild("PART_FrameLabel") as TextBlock;
            m_totalFramesLabel = GetTemplateChild("PART_TotalFramesLabel") as TextBlock;
            m_speedSlider = GetTemplateChild("PART_SpeedSlider") as Slider;
            m_speedLabel = GetTemplateChild("PART_SpeedLabel") as TextBlock;
            m_lodCombo = GetTemplateChild("PART_LodCombo") as ComboBox;
            m_renderModeCombo = GetTemplateChild("PART_RenderModeCombo") as ComboBox;

            if (m_restartBtn != null)
                m_restartBtn.Click += (s, e) => m_screen.Restart();

            if (m_playPauseBtn != null)
            {
                m_playPauseBtn.Checked += (s, e) => m_screen.IsPaused = false;
                m_playPauseBtn.Unchecked += (s, e) => m_screen.IsPaused = true;
            }

            if (m_speedSlider != null)
            {
                m_speedSlider.ValueChanged += (s, e) =>
                {
                    m_screen.SpeedMultiplier = e.NewValue;
                    if (m_speedLabel != null) m_speedLabel.Text = $"{e.NewValue:F1}x";
                };
                m_speedSlider.PreviewMouseWheel += (s, e) =>
                {
                    double delta = e.Delta > 0 ? 0.1 : -0.1;
                    double newVal = Math.Max(m_speedSlider.Minimum, Math.Min(m_speedSlider.Maximum, m_speedSlider.Value + delta));
                    m_speedSlider.Value = newVal;
                    e.Handled = true;
                };
            }

            if (m_lodCombo != null)
            {
                m_lodCombo.ItemsSource = new[] { "0", "1", "2", "3", "4", "5" };
                m_lodCombo.SelectedIndex = 0;
                m_lodCombo.SelectionChanged += (s, e) => m_screen.CurrentLOD = m_lodCombo.SelectedIndex;
            }

            if (m_renderModeCombo != null)
            {
                m_renderModeCombo.SelectedIndex = 0;
                m_renderModeCombo.SelectionChanged += (s, e) =>
                    m_screen.RenderMode = (DebugRenderMode)m_renderModeCombo.SelectedIndex;
            }

            m_screen.FrameChanged += frame =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isProgrammaticSliderUpdate = true;
                    if (m_timelineSlider != null) m_timelineSlider.Value = frame;
                    if (m_frameLabel != null) m_frameLabel.Text = frame.ToString();
                    _isProgrammaticSliderUpdate = false;
                }));

            if (m_timelineSlider != null)
            {
                m_timelineSlider.ValueChanged += (s, e) =>
                {
                    if (_isProgrammaticSliderUpdate) return;
                    m_screen.SeekToFrame((int)e.NewValue);
                };
                m_timelineSlider.PreviewMouseWheel += (s, e) =>
                {
                    if (_isProgrammaticSliderUpdate) return;
                    int target = (int)Math.Max(0, Math.Min(m_screen.TotalFrames - 1, m_timelineSlider.Value + (e.Delta > 0 ? 1 : -1)));
                    m_screen.SeekToFrame(target);
                    e.Handled = true;
                };
            }

            _ = LoadAsync();
        }

        public override void Closed()
        {
            if (m_viewport != null)
            {
                try
                {
                    m_viewport.Shutdown();
                }
                catch (Exception ex)
                {
                    App.Logger.LogWarning($"[AntStateEditor] Viewport shutdown: {ex.Message}");
                }
            }

            if (m_assetTreeView != null)
            {
                m_assetTreeView.ItemsSource = null;
                m_assetTreeView.Items.Clear();
            }

            if (_masterGroups != null)
            {
                foreach (var group in _masterGroups)
                    group.Assets?.Clear();
                _masterGroups.Clear();
            }

            _filteredGroups?.Clear();

            if (LoadedMeshes != null)
                LoadedMeshes.Clear();

            _loadedMeshData?.Clear();

            _selectedAsset = null;
            _currentPreviewAsset = null;
            _shiftAnchor = null;

            _bankBytes = null;
            _bank = null;

            AntRefTable.Clear();

            GC.Collect();

            base.Closed();
        }

        public void ShowAssetTree(AntAssetViewModel sourceVm)
        {
            if (sourceVm?.AssetInstance == null || !(sourceVm.AssetInstance is AntAsset root)) return;

            var allVms = _masterGroups.SelectMany(g => g.Assets).ToList();

            var window = new AssetBankPlugin.Windows.TreePreviewer(root, sourceVm.Id, allVms)
            {
                Owner = Window.GetWindow(this),
            };
            window.AssetDoubleClicked = asset => SelectAndRevealAsset(asset);
            window.Show();
        }

        private void SelectAndRevealAsset(AntAsset asset)
        {
            AntAssetViewModel targetVm = null;
            foreach (var g in _masterGroups)
            {
                foreach (var vm in g.Assets)
                {
                    if (vm.AssetInstance != asset) continue;
                    targetVm = vm;
                    break;
                }
                if (targetVm != null) break;
            }
            if (targetVm == null) return;

            var searchText = asset.ID.ToString();
            if (m_searchBox != null)
                m_searchBox.Text = searchText;
            ApplySearchFilter(searchText);

            _selectedAsset = targetVm;

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => ExpandAndSelectInTree(targetVm, asset)));
        }

        private void ExpandAndSelectInTree(AntAssetViewModel targetVm, AntAsset asset)
        {
            if (m_assetTreeView == null) return;

            AntTypeGroup targetGroup = null;
            foreach (var g in _filteredGroups)
            {
                foreach (var vm in g.Assets)
                {
                    if (vm.AssetInstance != asset) { continue; }
                    targetGroup = g;
                    break;
                }
                if (targetGroup != null) break;
            }
            if (targetGroup == null) return;

            var groupItem = m_assetTreeView.ItemContainerGenerator
                                .ContainerFromItem(targetGroup)
                                as System.Windows.Controls.TreeViewItem;
            if (groupItem == null) return;

            groupItem.IsExpanded = true;

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    groupItem.UpdateLayout();

                    var assetItem = groupItem.ItemContainerGenerator
                                        .ContainerFromItem(targetVm)
                                        as System.Windows.Controls.TreeViewItem;
                    if (assetItem == null) return;

                    assetItem.IsSelected = true;
                    assetItem.BringIntoView();
                    Window.GetWindow(this)?.Activate();
                }));
        }

        private void OpenReferenceSelector()
        {
            var selectionWindow = new BankPickerDialog { Owner = Window.GetWindow(this) };

            if (selectionWindow.ShowDialog() == true)
            {
                var selectedBanks = selectionWindow.SelectedEntries.ToList();
                if (selectedBanks.Count == 0) return;

                FrostyTaskWindow.Show("Loading Reference Banks", "", (task) =>
                {
                    int progress = 0;
                    foreach (var entry in selectedBanks)
                    {
                        task.Update($"Caching {entry.Name}...", ((double)progress / selectedBanks.Count) * 100.0);
                        try
                        {
                            EbxAsset asset = App.AssetManager.GetEbx(entry);
                            dynamic antStateAsset = asset.RootObject;
                            Stream s = null;
                            int bundleId = entry.Bundles.Count > 0 ? entry.Bundles[0] : 0;

                            if (antStateAsset.StreamingGuid == Guid.Empty)
                            {
                                var res = App.AssetManager.GetResEntry(entry.Name);
                                if (res != null) s = App.AssetManager.GetRes(res);
                            }
                            else
                            {
                                var chunk = App.AssetManager.GetChunkEntry(antStateAsset.StreamingGuid);
                                if (chunk != null) { bundleId = chunk.Bundles[0]; s = App.AssetManager.GetChunk(chunk); }
                            }

                            if (s != null)
                            {
                                using (var reader = new NativeReader(s))
                                    _ = new Bank(reader, bundleId);
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogError($"[AntStateEditor] Failed to load reference bank {entry.Name}: {ex.Message}");
                        }
                        progress++;
                    }
                });

                App.Logger.Log($"[AntStateEditor] Successfully loaded {selectedBanks.Count} reference banks into cache.");
            }
        }

        internal void TestCompressor(AntAssetViewModel vm)
        {
            if (!(vm.AssetInstance is VbrAnimationAsset original))
                return;

            try
            {
                List<Vector4> rawFrames = original.Decompress();

                int qCount = original.QuaternionCount;
                int vCount = original.Vector3Count;
                int fCount = original.NumFloat;
                int frameCount = original.NumKeys;
                int dofCount = qCount + vCount + fCount;

                int expected = frameCount * dofCount;
                if (rawFrames.Count < expected)
                {
                    App.Logger.LogError($"Decompressed data size mismatch. Expected {expected}, got {rawFrames.Count}");
                    return;
                }

                int totalQ = original.QuaternionCount + original.ConstQuaternionCount;
                int totalV = original.Vector3Count + original.ConstVector3Count;
                int totalF = original.NumFloat + original.ConstFloatCount;
                int totalCh = totalQ + totalV + totalF;

                bool[] isConstant = new bool[totalCh];
                if (original.ConstChanMap != null && original.ConstChanMap.Length > 0)
                {
                    bool curVal = false;
                    int chIdx = 0;
                    foreach (byte runLen in original.ConstChanMap)
                    {
                        for (int r = 0; r < runLen && chIdx < totalCh; r++)
                        {
                            isConstant[chIdx++] = curVal;
                        }
                        curVal = !curVal;
                    }
                }
                else
                {
                    int idx = 0;
                    for (int i = 0; i < totalQ; i++) isConstant[idx++] = (i >= original.QuaternionCount);
                    for (int i = 0; i < totalV; i++) isConstant[idx++] = (i >= original.Vector3Count);
                    for (int i = 0; i < totalF; i++) isConstant[idx++] = (i >= original.NumFloat);
                }

                List<int> constQuatChannels = new List<int>();
                List<int> animQuatChannels = new List<int>();
                for (int i = 0; i < totalQ; i++)
                {
                    if (isConstant[i]) constQuatChannels.Add(i);
                    else animQuatChannels.Add(i);
                }

                List<int> constVecChannels = new List<int>();
                List<int> animVecChannels = new List<int>();
                for (int i = totalQ; i < totalQ + totalV; i++)
                {
                    if (isConstant[i]) constVecChannels.Add(i - totalQ);
                    else animVecChannels.Add(i - totalQ);
                }

                List<int> constFloatChannels = new List<int>();
                List<int> animFloatChannels = new List<int>();
                for (int i = totalQ + totalV; i < totalCh; i++)
                {
                    if (isConstant[i]) constFloatChannels.Add(i - (totalQ + totalV));
                    else animFloatChannels.Add(i - (totalQ + totalV));
                }

                float[][][] quats = new float[totalQ][][];
                for (int q = 0; q < totalQ; q++) quats[q] = new float[frameCount][];

                float[][][] vecs = new float[totalV][][];
                for (int v = 0; v < totalV; v++) vecs[v] = new float[frameCount][];

                float[][] floats = new float[totalF][];
                for (int fl = 0; fl < totalF; fl++) floats[fl] = new float[frameCount];

                ushort[] paletteIndexes = original.PaletteIndexes ?? Array.Empty<ushort>();
                float[] constantPalette = original.ConstantPalette ?? Array.Empty<float>();

                int paletteIdx = 0;

                for (int i = 0; i < constQuatChannels.Count; i++)
                {
                    int qIdx = constQuatChannels[i];
                    Vector4 val = new Vector4(0, 0, 0, 1);
                    if (paletteIdx + 3 < paletteIndexes.Length)
                    {
                        int i0 = paletteIndexes[paletteIdx];
                        int i1 = paletteIndexes[paletteIdx + 1];
                        int i2 = paletteIndexes[paletteIdx + 2];
                        int i3 = paletteIndexes[paletteIdx + 3];
                        if (i0 < constantPalette.Length && i1 < constantPalette.Length &&
                            i2 < constantPalette.Length && i3 < constantPalette.Length)
                        {
                            val = new Vector4(
                                constantPalette[i0],
                                constantPalette[i1],
                                constantPalette[i2],
                                constantPalette[i3]
                            );
                            val = val * (original.QuatMax - original.QuatMin) + new Vector4(original.QuatMin);
                        }
                    }
                    paletteIdx += 4;

                    for (int f = 0; f < frameCount; f++)
                    {
                        quats[qIdx][f] = new float[4] { val.X, val.Y, val.Z, val.W };
                    }
                }

                for (int i = 0; i < constVecChannels.Count; i++)
                {
                    int vIdx = constVecChannels[i];
                    Vector3 val = new Vector3(0, 0, 0);
                    if (paletteIdx + 2 < paletteIndexes.Length)
                    {
                        int i0 = paletteIndexes[paletteIdx];
                        int i1 = paletteIndexes[paletteIdx + 1];
                        int i2 = paletteIndexes[paletteIdx + 2];
                        if (i0 < constantPalette.Length && i1 < constantPalette.Length &&
                            i2 < constantPalette.Length)
                        {
                            val = new Vector3(
                                constantPalette[i0],
                                constantPalette[i1],
                                constantPalette[i2]
                            );
                            val = val * (original.Vec3Max - original.Vec3Min) + new Vector3(original.Vec3Min);
                        }
                    }
                    paletteIdx += 3;

                    for (int f = 0; f < frameCount; f++)
                    {
                        vecs[vIdx][f] = new float[3] { val.X, val.Y, val.Z };
                    }
                }

                for (int i = 0; i < constFloatChannels.Count; i++)
                {
                    int fIdx = constFloatChannels[i];
                    float val = 0f;
                    if (paletteIdx < paletteIndexes.Length)
                    {
                        int i0 = paletteIndexes[paletteIdx];
                        if (i0 < constantPalette.Length)
                        {
                            val = constantPalette[i0];
                            val = val * (original.FloatMax - original.FloatMin) + original.FloatMin;
                        }
                    }
                    paletteIdx += 1;

                    for (int f = 0; f < frameCount; f++)
                    {
                        floats[fIdx][f] = val;
                    }
                }

                for (int f = 0; f < frameCount; f++)
                {
                    int frameOffset = f * dofCount;

                    for (int i = 0; i < animQuatChannels.Count; i++)
                    {
                        int qIdx = animQuatChannels[i];
                        int rawIdx = frameOffset + i;
                        Vector4 v = (rawIdx < rawFrames.Count) ? rawFrames[rawIdx] : new Vector4(0, 0, 0, 1);
                        quats[qIdx][f] = new float[4] { v.X, v.Y, v.Z, v.W };
                    }

                    for (int i = 0; i < animVecChannels.Count; i++)
                    {
                        int vIdx = animVecChannels[i];
                        int rawIdx = frameOffset + original.QuaternionCount + i;
                        Vector4 vv = (rawIdx < rawFrames.Count) ? rawFrames[rawIdx] : new Vector4(0, 0, 0, 0);
                        vecs[vIdx][f] = new float[3] { vv.X, vv.Y, vv.Z };
                    }

                    for (int i = 0; i < animFloatChannels.Count; i++)
                    {
                        int fIdx = animFloatChannels[i];
                        int rawIdx = frameOffset + original.QuaternionCount + original.Vector3Count + i;
                        Vector4 vf = (rawIdx < rawFrames.Count) ? rawFrames[rawIdx] : new Vector4(0, 0, 0, 0);
                        floats[fIdx][f] = vf.X;
                    }
                }

                bool cycle = (original.Flags & 1) != 0;

                VbrAnimationAsset recompressed = VbrCompressor.CompressFromData(
                    quats, vecs, floats, frameCount, cycle, original);

                if (recompressed.NumKeys == original.NumKeys)
                {
                    original.Data = recompressed.Data;
                    original.ConstantPalette = recompressed.ConstantPalette;
                    original.FrameBlockSizes = recompressed.FrameBlockSizes;
                    original.PaletteIndexes = recompressed.PaletteIndexes;
                    original.ConstChanMap = recompressed.ConstChanMap;

                    original.QuaternionCount = recompressed.QuaternionCount;
                    original.Vector3Count = recompressed.Vector3Count;
                    original.NumFloat = recompressed.NumFloat;
                    original.ConstQuaternionCount = recompressed.ConstQuaternionCount;
                    original.ConstVector3Count = recompressed.ConstVector3Count;
                    original.ConstFloatCount = recompressed.ConstFloatCount;
                    original.QuatMin = recompressed.QuatMin;
                    original.QuatMax = recompressed.QuatMax;
                    original.TrajMin = recompressed.TrajMin;
                    original.TrajMax = recompressed.TrajMax;
                    original.Vec3Min = recompressed.Vec3Min;
                    original.Vec3Max = recompressed.Vec3Max;
                    original.FloatMin = recompressed.FloatMin;
                    original.FloatMax = recompressed.FloatMax;
                    original.Dct = recompressed.Dct;
                    original.KeyTimeSize = recompressed.KeyTimeSize;
                    original.ConstChanMapSize = recompressed.ConstChanMapSize;
                    original.ConstPaletteSize = recompressed.ConstPaletteSize;
                    original.Flags = recompressed.Flags;
                    original.EndFrame = recompressed.EndFrame;

                    original.VectorOffsetScale = recompressed.VectorOffsetScale;
                    original.FloatOffsetScale = recompressed.FloatOffsetScale;
                    original.VectorOffsetSize = recompressed.VectorOffsetSize;
                    original.FloatOffsetSize = recompressed.FloatOffsetSize;
                    original.VectorOffsets = recompressed.VectorOffsets;

                    original.RawData = new Dictionary<string, object>(recompressed.RawData);
                    original.RawData["__name"] = original.Name;
                    original.RawData["__guid"] = original.ID;

                    byte[] guidBytes = original.ID.ToByteArray();
                    original.RawData["__key"] = BitConverter.ToUInt64(guidBytes, 0);

                    var cacheField = typeof(VbrAnimationAsset).GetField("DecompressedData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (cacheField != null)
                    {
                        var cacheList = cacheField.GetValue(original) as List<Vector4>;
                        cacheList?.Clear();
                    }

                    vm.MarkModified();
                    vm.ResetProperties();
                    _ = LoadPreviewAsync(original, _currentPreviewName);

                    App.Logger.Log($"[TestCompressor] Round-trip applied. Original size: {original.Data?.Length ?? 0} bytes, Recompressed size: {recompressed.Data?.Length ?? 0} bytes");
                }
                else
                {
                    App.Logger.LogError($"[TestCompressor] Round-trip failed: frame count mismatch. Original: {original.NumKeys}, New: {recompressed.NumKeys}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"[TestCompressor] Test failed: {ex.Message}");
            }
        }
        public void ExportRigWithMesh(AntAssetViewModel sourceVm)
        {
            if (!(sourceVm?.AssetInstance is RigAsset rig)) return;

            EbxAssetEntry initialMesh = null;
            dynamic mainWin = App.EditorWindow;
            EbxAssetEntry selectedMeshEntry = mainWin?.DataExplorer?.SelectedAsset as EbxAssetEntry;

            if (selectedMeshEntry != null && (selectedMeshEntry.Type == "SkinnedMeshAsset" || selectedMeshEntry.Type == "RigidMeshAsset"))
            {
                initialMesh = selectedMeshEntry;
            }
            else if (m_meshPathBox != null && !string.IsNullOrWhiteSpace(m_meshPathBox.Text))
            {
                initialMesh = App.AssetManager.GetEbxEntry(m_meshPathBox.Text);
            }

            var settingsWin = new AssetBankPlugin.Windows.RigExportSettingsWindow(initialMesh) { Owner = Window.GetWindow(this) };
            if (settingsWin.ShowDialog() != true || settingsWin.SelectedMesh == null)
            {
                return;
            }

            EbxAssetEntry targetMeshEntry = settingsWin.SelectedMesh;
            string fbxVersion = settingsWin.SelectedFbxVersion;
            string fbxScale = settingsWin.SelectedScale;
            bool exportSingleLod = settingsWin.ExportSingleLod;

            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Rig + Mesh", "*.fbx (Autodesk FBX)|*.fbx", "Mesh", rig.Name);
            if (!sfd.ShowDialog()) return;

            string exportPath = sfd.FileName;

            FrostyTaskWindow.Show("Exporting Rig + Mesh", "", (task) =>
            {
                string virtualSkelName = null;
                try
                {
                    InternalSkeleton internalSkel = AntRigExporter.BuildInternalSkeletonFromRig(rig);

                    if (internalSkel == null)
                    {
                        App.Logger.LogError($"[ExportRig] Failed to construct skeleton for Rig '{rig.Name}'.");
                        return;
                    }

                    if (internalSkel == null)
                    {
                        App.Logger.LogError($"[ExportRig] Failed to construct skeleton for Rig '{rig.Name}'.");
                        return;
                    }

                    dynamic dynamicSkelAsset = AntRigExporter.CreateEbxSkeletonFromInternal(internalSkel);

                    virtualSkelName = VirtualSkeletonManager.RegisterVirtualSkeleton(dynamicSkelAsset);

                    EbxAsset meshEbx = App.AssetManager.GetEbx(targetMeshEntry);
                    dynamic meshRoot = meshEbx.RootObject;
                    ulong resRid = meshRoot.MeshSetResource;
                    ResAssetEntry rEntry = App.AssetManager.GetResEntry(resRid);
                    MeshSet meshSet = App.AssetManager.GetResAs<MeshSet>(rEntry);

                    var exporter = new MeshSetPlugin.FBXExporter(task);
                    exporter.ExportFBX(meshRoot, exportPath, fbxVersion, fbxScale, false, exportSingleLod, false, virtualSkelName, "binary", meshSet);

                    App.Logger.Log($"[ExportRig] Successfully exported Rig '{rig.Name}' with Mesh '{targetMeshEntry.Name}' to {exportPath}");

                    Application.Current.Dispatcher.Invoke(() =>
                        FrostyMessageBox.Show($"Exported successfully to:\n{exportPath}", "Export Complete"));
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"[ExportRig] Failed to export Rig + Mesh: {ex.Message}\n{ex.StackTrace}");
                    Application.Current.Dispatcher.Invoke(() =>
                        FrostyMessageBox.Show($"Failed to export: {ex.Message}", "Export Failed"));
                }
                finally
                {
                    if (!string.IsNullOrEmpty(virtualSkelName))
                    {
                        VirtualSkeletonManager.UnregisterVirtualSkeleton(virtualSkelName);
                    }
                }
            });
        }

        public class DuplicateRenameWindow : FrostyWindow // remake in xaml later
        {
            public string NewName { get; private set; }
            private readonly IEnumerable<string> _existingNames;
            private TextBox m_nameBox;

            public DuplicateRenameWindow(string defaultName, IEnumerable<string> existingNames)
            {
                _existingNames = existingNames;
                NewName = defaultName;

                Title = "  Duplicate Asset";
                Width = 420;
                Height = 150;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;

                Grid mainGrid = new Grid { Margin = new Thickness(15) };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // TextBox
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Spacer
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                TextBlock label = new TextBlock
                {
                    Text = "Enter a unique name for the duplicated asset:",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 11
                };
                Grid.SetRow(label, 0);
                mainGrid.Children.Add(label);

                m_nameBox = new TextBox
                {
                    Text = defaultName,
                    Height = 24,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 0, 4, 0),
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                    CaretBrush = Brushes.White
                };

                m_nameBox.Loaded += (s, e) => { m_nameBox.Focus(); m_nameBox.SelectAll(); };
                Grid.SetRow(m_nameBox, 1);
                mainGrid.Children.Add(m_nameBox);

                StackPanel btnStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Button okBtn = new Button
                {
                    Content = "Duplicate",
                    Width = 90,
                    Height = 26,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };
                okBtn.Click += (s, e) => Commit();
                btnStack.Children.Add(okBtn);

                Button cancelBtn = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 26,
                    IsCancel = true
                };
                cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
                btnStack.Children.Add(cancelBtn);

                Grid.SetRow(btnStack, 3);
                mainGrid.Children.Add(btnStack);

                Content = mainGrid;
            }

            private void Commit()
            {
                string typed = m_nameBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(typed))
                {
                    FrostyMessageBox.Show("Asset name cannot be empty.", "Duplicate Asset");
                    return;
                }

                if (_existingNames.Any(x => x.Equals(typed, StringComparison.OrdinalIgnoreCase)))
                {
                    FrostyMessageBox.Show("An asset with this name already exists in the bank database. Please specify a unique name.", "Duplicate Asset");
                    return;
                }

                NewName = typed;
                DialogResult = true;
                Close();
            }
        }

        public class BankRigPickerDialog : FrostyWindow
        {
            public RigAsset SelectedRig { get; private set; }
            private ListBox m_listBox;

            public BankRigPickerDialog(List<RigAsset> rigs)
            {
                Title = "  Select Export Skeleton Rig";
                Width = 450;
                Height = 350;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;

                Grid mainGrid = new Grid { Margin = new Thickness(15) };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Label
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                TextBlock label = new TextBlock
                {
                    Text = "Select a Rig Asset from the active bank to use as the export skeleton:",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(label, 0);
                mainGrid.Children.Add(label);

                m_listBox = new ListBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                    Foreground = Brushes.White,
                    DisplayMemberPath = "Name"
                };
                foreach (var r in rigs) m_listBox.Items.Add(r);
                if (m_listBox.Items.Count > 0) m_listBox.SelectedIndex = 0;

                m_listBox.MouseDoubleClick += (s, e) => Confirm();

                Grid.SetRow(m_listBox, 1);
                mainGrid.Children.Add(m_listBox);

                StackPanel btnStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Button okBtn = new Button
                {
                    Content = "Select",
                    Width = 90,
                    Height = 26,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };
                okBtn.Click += (s, e) => Confirm();
                btnStack.Children.Add(okBtn);

                Button cancelBtn = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 26,
                    IsCancel = true
                };
                cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
                btnStack.Children.Add(cancelBtn);

                Grid.SetRow(btnStack, 2);
                mainGrid.Children.Add(btnStack);

                Content = mainGrid;
            }

            private void Confirm()
            {
                SelectedRig = m_listBox.SelectedItem as RigAsset;
                if (SelectedRig == null) return;
                DialogResult = true;
                Close();
            }
        }

        private void UpdateMeshSkeletons()
        {
            if (m_screen.CurrentSkeleton == null || _loadedMeshData.Count == 0) return;

            foreach (var e in _loadedMeshData)
            {
                if (e.SourceEntry != null)
                {
                    try
                    {
                        var ebx = App.AssetManager.GetEbx(e.SourceEntry);
                        e.PerMeshSkeleton = BuildPerMeshSkeleton(ebx.RootObject);
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogWarning($"[AntStateEditor] Failed to bind skeleton for mesh '{e.DisplayName}': {ex.Message}");
                    }
                }
            }

            OnMeshVisibilityChanged();
        }

    }
    internal class EditAnimAssetsWindow : FrostyWindow
    {
        private readonly List<AntStateAssetEditor.AnimAssetEntry> _entries;
        private readonly List<TextBox> _boxes = new List<TextBox>();

        public EditAnimAssetsWindow(List<AntStateAssetEditor.AnimAssetEntry> entries)
        {
            _entries = entries;
            Title = "  Edit Anim Assets";
            Width = 520;
            MinHeight = 150;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var outer = new StackPanel { Margin = new Thickness(10) };

            foreach (var e in entries)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = $"Track {e.TrackIndex}  Anim {e.AnimIndex}:",
                    Foreground = new SolidColorBrush(Color.FromRgb(187, 187, 187)),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11,
                };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                var tb = new TextBox
                {
                    Text = e.CurrentGuid,
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 206, 155)),
                    FontSize = 11,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                    CaretBrush = Brushes.White,
                    Padding = new Thickness(3, 2, 3, 2),
                };
                Grid.SetColumn(tb, 1);
                row.Children.Add(tb);
                _boxes.Add(tb);
                outer.Children.Add(row);
            }

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0),
            };
            var save = new Button { Content = "Save", Width = 100, Height = 26, Margin = new Thickness(0, 0, 12, 0) };
            var cancel = new Button { Content = "Cancel", Width = 80, Height = 26 };
            save.Click += (s, e) => Commit();
            cancel.Click += (s, e) => { DialogResult = false; Close(); };
            btnRow.Children.Add(save);
            btnRow.Children.Add(cancel);
            outer.Children.Add(btnRow);

            Content = outer;
        }

        private void Commit()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                string text = _boxes[i].Text.Trim();
                if (text == _entries[i].CurrentGuid) continue;

                if (Guid.TryParse(text, out Guid g))
                {
                    _entries[i].NewKey = AntStateAssetEditor.GuidToKey(g);
                    _entries[i].WasEdited = true;
                }
                else if (ulong.TryParse(text,
                    System.Globalization.NumberStyles.HexNumber, null, out ulong h))
                {
                    _entries[i].NewKey = h;
                    _entries[i].WasEdited = true;
                }
            }
            DialogResult = true;
            Close();
        }
    }
}
