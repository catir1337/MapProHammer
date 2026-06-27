using System.Windows;

// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only

namespace MapProHammer.Views
{
    public partial class NewTypeDialog : Window
    {
        public string ObjPath { get; private set; } = string.Empty;
        public string Guid    { get; private set; } = string.Empty;

        public NewTypeDialog()
        {
            InitializeComponent();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            string path = TbObjPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Укажи путь к мешу (ObjPath).", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ObjPath = path;
            Guid    = TbGuid.Text.Trim();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
