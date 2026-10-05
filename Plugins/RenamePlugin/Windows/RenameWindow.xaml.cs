using Frosty.Controls;
using Frosty.Core;
using System;
using System.Windows;
using FrostySdk.Managers.Entries;
using System.IO;

namespace RenamePlugin.Windows
{
    public partial class RenameWindow : FrostyDockableWindow
    {
        private string mName = "";

        public RenameWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;

            InitTextBox();
        }

        private void InitTextBox()
        {
            varNameTextBox.Text = App.SelectedAsset.Name[(App.SelectedAsset.Name.LastIndexOf('/') + 1)..];
        }

        private void VarNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            mName = varNameTextBox.Text;
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(mName))
            {
                App.Logger.LogError("Name is required.");
                return;
            }
            else if (mName == App.SelectedAsset.Name[(App.SelectedAsset.Name.LastIndexOf('/') + 1)..]) 
            {
                App.Logger.Log("Name unchanged.");
            }
            else
            {
                EbxAssetEntry asset = App.SelectedAsset;
                var ebxAsset = App.AssetManager.GetEbx(asset);
                dynamic rootObj = ebxAsset.RootObject;

                string nameOrig = asset.Name;

                string assetPath = Path.GetDirectoryName(nameOrig).Replace('\\', '/');
                string nameCleaned = mName.Replace("/", string.Empty);

                rootObj.Name = assetPath + "/" + nameCleaned;
                asset.Name = rootObj.Name;

                
                App.AssetManager.RemoveAssetFromList(asset);
                App.AssetManager.AddEbx(asset);
                //App.AssetManager.ModifyEbx(asset.Name, ebxAsset);

                RefreshEditorDataExplorer();
            }
            
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private static void RefreshEditorDataExplorer()
        {
            try
            {
                var editorWindow = App.EditorWindow;
                if (editorWindow != null)
                {
                    editorWindow.DataExplorer.ItemsSource = App.AssetManager.EnumerateEbx();
                    editorWindow.DataExplorer.RefreshItems();
                }
            }
            catch { }
        }
    }
}