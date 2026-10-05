using System;
using System.Windows;
using System.Windows.Controls;

namespace AssetBankPlugin.Windows
{
    public partial class AssetFilterMenu : UserControl
    {
        public bool ShowModifiedOnly => PART_ChkModifiedOnly.IsChecked == true;
        public bool ShowAnimationsOnly => PART_ChkAnimationsOnly.IsChecked == true;

        public bool SortAscending => PART_TogSortOrder.IsChecked == false;

        public event EventHandler FilterChanged;

        public AssetFilterMenu()
        {
            InitializeComponent();

            PART_ChkModifiedOnly.Click += OnFilterOptionChanged;
            PART_ChkAnimationsOnly.Click += OnFilterOptionChanged;

            PART_TogSortOrder.Click += OnFilterOptionChanged;

            PART_BtnReset.Click += (s, e) =>
            {
                PART_ChkModifiedOnly.IsChecked = false;
                PART_ChkAnimationsOnly.IsChecked = false;
                PART_TogSortOrder.IsChecked = false;
                FilterChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        private void OnFilterOptionChanged(object sender, RoutedEventArgs e)
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
