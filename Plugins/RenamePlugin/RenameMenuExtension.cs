using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Xml.Linq;
using RenamePlugin.Windows;

namespace RenamePlugin
{
    public class RenameContextMenuItem : DataExplorerContextMenuExtension
    {
        public override string ContextItemName => "Rename";
        public override ImageSource Icon => null;

        public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
        {
            RenameWindow win = new RenameWindow();
            win.ShowDialog();
            return;
        });
    }
}