using Frosty.Core.Controls;
using Frosty.Core;
using FrostySdk.Interfaces;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;
using FrostySdk;

namespace ScriptAssetPlugin.Editors
{
    public class ScriptAssetDefinition : AssetDefinition
    {
        protected static ImageSource imageSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/ScriptAssetPlugin;component/Images/ScriptAssetIcon.png") as ImageSource;

        public override ImageSource GetIcon()
        {
            return imageSource;
        }

        public override FrostyAssetEditor GetEditor(ILogger logger)
        {
            return new ScriptAssetEditor(logger);
        }
    }

    public class ScriptAssetEditor : FrostyAssetEditor
    {
        public static readonly DependencyProperty GridVisibleProperty = DependencyProperty.Register("GridVisible", typeof(bool), typeof(ScriptAssetEditor), new FrameworkPropertyMetadata(true));
        public bool GridVisible
        {
            get => (bool)GetValue(GridVisibleProperty);
            set => SetValue(GridVisibleProperty, value);
        }

        private bool firstTimeLoad = true;
        private bool isJsonAsset = false;
        private TextBox scriptTextBox;
        private TextBlock fieldNameTxtBlock;

        static ScriptAssetEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ScriptAssetEditor), new FrameworkPropertyMetadata(typeof(ScriptAssetEditor)));
        }

        public ScriptAssetEditor(ILogger inLogger) : base(inLogger) {}

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Loaded += ScriptAssetEditor_Loaded;
            fieldNameTxtBlock = GetTemplateChild("PART_FieldNameBlock") as TextBlock;
            scriptTextBox = GetTemplateChild("PART_ScriptTextBox") as TextBox;
            scriptTextBox.TextChanged += ScriptTextBox_TextChanged;
        }

        private void ScriptTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!firstTimeLoad)
            {
                dynamic root = asset.RootObject;

                if (isJsonAsset)
                    root.DefaultJson = scriptTextBox.Text;
                else
                    root.EffectScript = scriptTextBox.Text;

                App.AssetManager.ModifyEbx(root.Name, asset);
                base.InvokeOnAssetModified();
            }
        }

        private void ScriptAssetEditor_Loaded(object sender, RoutedEventArgs e)
        {
            isJsonAsset = TypeLibrary.IsSubClassOf(asset.RootObject, "SchedulableJsonAsset");
            dynamic root = asset.RootObject;

            fieldNameTxtBlock.Text = isJsonAsset ? "DefaultJson" : "EffectScript";
            scriptTextBox.Text = isJsonAsset ? root.DefaultJson : root.EffectScript;
            firstTimeLoad = false;
        }
    }
}
