using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Managers.Entries;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace AssetBankPlugin.Windows
{
    public partial class RigExportSettingsWindow : FrostyWindow
    {
        public EbxAssetEntry SelectedMesh { get; private set; }
        public string SelectedFbxVersion { get; private set; }
        public string SelectedScale { get; private set; }
        public bool ExportSingleLod { get; private set; }

        public RigExportSettingsWindow(EbxAssetEntry initialMesh)
        {
            InitializeComponent();

            SelectedMesh = initialMesh;

            // Load saved settings from config
            SelectedFbxVersion = Config.Get<string>("AntRigExportFbxVersion", "2017", ConfigScope.Game);
            SelectedScale = Config.Get<string>("AntRigExportScale", "Centimeters", ConfigScope.Game);
            ExportSingleLod = Config.Get<bool>("AntRigExportSingleLod", false, ConfigScope.Game);
            m_singleLodCheck.IsChecked = ExportSingleLod;

            // Populate Version Dropdown
            string[] versions = { "2012", "2013", "2014", "2016", "2017" };
            foreach (var v in versions) m_fbxCombo.Items.Add(v);
            m_fbxCombo.SelectedItem = SelectedFbxVersion;

            // Populate Scale Dropdown
            string[] scales = { "Millimeters", "Centimeters", "Decimeters", "Meters", "Kilometers" };
            foreach (var sc in scales) m_scaleCombo.Items.Add(sc);
            m_scaleCombo.SelectedItem = SelectedScale;

            UpdateMeshUI();
        }

        private void UpdateMeshUI()
        {
            m_meshNameText.Text = SelectedMesh != null ? SelectedMesh.Filename : "No Mesh Selected";
            m_meshPathText.Text = SelectedMesh != null ? SelectedMesh.Name : "Click Browse to select a mesh...";
            m_meshIcon.Source = GetAssetIcon(SelectedMesh);
        }

        private void BrowseMesh_Click(object sender, RoutedEventArgs e)
        {
            var picker = new MeshPickerDialog { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedEntry != null)
            {
                SelectedMesh = picker.SelectedEntry;
                UpdateMeshUI();
            }
        }

        private void FbxCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (m_fbxCombo.SelectedItem != null)
                SelectedFbxVersion = m_fbxCombo.SelectedItem.ToString();
        }

        private void ScaleCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (m_scaleCombo.SelectedItem != null)
                SelectedScale = m_scaleCombo.SelectedItem.ToString();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMesh == null)
            {
                FrostyMessageBox.Show("Please select a target mesh asset before exporting.", "Missing Mesh");
                return;
            }

            ExportSingleLod = m_singleLodCheck.IsChecked == true;

            Config.Add("AntRigExportFbxVersion", SelectedFbxVersion, ConfigScope.Game);
            Config.Add("AntRigExportScale", SelectedScale, ConfigScope.Game);
            Config.Add("AntRigExportSingleLod", ExportSingleLod, ConfigScope.Game);
            Config.Save();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private ImageSource GetAssetIcon(EbxAssetEntry entry)
        {
            if (entry == null) return null;

            string packUri;

            if (entry.Type == "SkeletonAsset")
            {
                packUri = "pack://application:,,,/FrostyCore;component/Images/Assets/SkeletonFileType.png";
            }
            else if (entry.Type == "RigidMeshAsset")
            {
                packUri = "pack://application:,,,/AssetBankPlugin;component/Images/RigidMeshFileType.png";
            }
            else
            {
                packUri = "pack://application:,,,/AssetBankPlugin;component/Images/SkinnedMeshFileType.png";
            }

            try
            {
                return new BitmapImage(new Uri(packUri, UriKind.Absolute));
            }
            catch
            {
                return null;
            }
        }
    }
}