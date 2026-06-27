using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using MapProHammer.Model;
// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only

namespace MapProHammer.Views
{

    public partial class ObjectDialog : Window
    {
        private readonly List<MapObjectType> _types;
        private readonly MapObject?          _editing;
        private List<MapObjectType>          _filteredTypes = new();

        public MapObject? Result { get; private set; }

        public ObjectDialog(List<MapObjectType> types)
        {
            InitializeComponent();
            _types = types;
            Title  = "Добавить объект";
            RefreshList(string.Empty);
        }

        public ObjectDialog(List<MapObjectType> types, MapObject editing)
        {
            InitializeComponent();
            _types   = types;
            _editing = editing;
            Title    = "Изменить объект";
            RefreshList(string.Empty);
            FillFields(editing);
        }

        // Заполнить/обновить список с сортировкой A→Z и фильтром
        private void RefreshList(string filter)
        {
            var query = string.IsNullOrEmpty(filter)
                ? _types
                : _types.Where(t => t.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));

            _filteredTypes = query
                .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ListTypes.ItemsSource       = _filteredTypes;
            ListTypes.DisplayMemberPath = "DisplayName";
        }

        private void TxtTypeSearch_TextChanged(object s, TextChangedEventArgs e)
        {
            var prev = ListTypes.SelectedItem as MapObjectType;
            RefreshList(TxtTypeSearch.Text.Trim());

            // Сохранить выделение если тип ещё виден после фильтра
            if (prev != null && _filteredTypes.Contains(prev))
            {
                ListTypes.SelectedItem = prev;
                ListTypes.ScrollIntoView(prev);
            }
        }

        private void ListTypes_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            TxtTypePath.Text = ListTypes.SelectedItem is MapObjectType t ? t.ObjPath : string.Empty;
        }

        private void FillFields(MapObject obj)
        {
            TbX.Text  = F(obj.Position.X); TbY.Text = F(obj.Position.Y); TbZ.Text = F(obj.Position.Z);
            TbRX.Text = F(obj.Rotation.X); TbRY.Text = F(obj.Rotation.Y); TbRZ.Text = F(obj.Rotation.Z);
            TbSX.Text = F(obj.Scale.X);    TbSY.Text = F(obj.Scale.Y);    TbSZ.Text = F(obj.Scale.Z);

            if (obj.ObjType != null)
            {
                var match = _filteredTypes.FirstOrDefault(t => t.Id == obj.ObjType.Id);
                if (match != null) { ListTypes.SelectedItem = match; ListTypes.ScrollIntoView(match); }
            }
        }

        private void BtnOK_Click(object s, RoutedEventArgs e)
        {
            if (ListTypes.SelectedItem is not MapObjectType selType)
            {
                MessageBox.Show("Выберите тип объекта.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var obj = _editing ?? new MapObject();
            obj.Position  = new Vector3(P(TbX.Text), P(TbY.Text), P(TbZ.Text));
            obj.Rotation  = new Vector3(P(TbRX.Text), P(TbRY.Text), P(TbRZ.Text));
            obj.Scale     = new Vector3(P(TbSX.Text, 1f), P(TbSY.Text, 1f), P(TbSZ.Text, 1f));
            obj.ObjType   = selType;
            obj.ObjInfoId = selType.Id;

            Result = obj;
            DialogResult = true;
        }

        private void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;

        private static string F(float v) => v.ToString("G6", CultureInfo.InvariantCulture);
        private static float  P(string s, float fb = 0f) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fb;
    }
}
