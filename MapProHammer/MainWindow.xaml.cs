using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MapProHammer.Model;
using MapProHammer.Model.ObjectData;
using MapProHammer.Views;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MapProHammer
{
    public partial class MainWindow : Window
    {

        private MapFile?   _map;
        private MapObject? _selected;
        private bool       _suppressPropChange;
        private bool       _suppressSelection;
        private List<MapObject> _allObjects = new();
        private List<MapObject> _filtered   = new();

        private double _zoom = 0.1;
        private double _panX = 0, _panY = 0;
        private Point  _lastMouse;
        private bool   _isPanning, _isDragging;

        private readonly DrawingVisual _dv = new();

        // ★ Ссылка на 3D-окно (null, если закрыто)
        private View3DWindow? _view3D;

        private static readonly Dictionary<ObjectKind, (Color dot, Color border)> KC = new()
        {
            [ObjectKind.Generic]    = (Color.FromRgb( 90, 90,100), Color.FromRgb(140,140,150)),
            [ObjectKind.SpawnCar]   = (Color.FromRgb( 50,110,200), Color.FromRgb( 80,160,255)),
            [ObjectKind.SpawnHuman] = (Color.FromRgb( 40,170, 60), Color.FromRgb( 80,220,100)),
            [ObjectKind.Wire]       = (Color.FromRgb(200,140, 30), Color.FromRgb(255,190, 60)),
            [ObjectKind.Decal]      = (Color.FromRgb(160, 40,160), Color.FromRgb(210, 90,210)),
        };

        public MainWindow()
        {
            InitializeComponent();

            var img = new System.Windows.Controls.Image
            {
                Stretch           = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top,
                IsHitTestVisible  = false
            };
            ViewportCanvas.Children.Add(img);
            _dvImage = img;

            ViewportBorder.SizeChanged += (_, _) => Redraw();
        }

        private System.Windows.Controls.Image _dvImage;

        private async void BtnOpen_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Открыть карту", Filter = "Map.bytes|Map.bytes|Все файлы|*.*" };
            if (dlg.ShowDialog() != true) return;
            string path = dlg.FileName;
            try
            {
                TxtStatus.Text = "Загрузка…";
                BtnOpen.IsEnabled = false;
                Cursor = Cursors.Wait;

                var (map, objects) = await Task.Run(() =>
                {
                    var m = MapFile.Load(path);
                    var o = m.GetAllObjects();
                    return (m, o);
                });

                _map        = map;
                _allObjects = objects;
                _filtered   = _allObjects;

                TxtStatus.Text = $"{System.IO.Path.GetFileName(path)}  —  {_allObjects.Count:N0} объектов";
                BtnSave.IsEnabled = BtnSaveAs.IsEnabled = true;
                BtnAddType.IsEnabled = true;
                BtnDeleteType.IsEnabled = true;
                BtnView3D.IsEnabled = true;   // ★ разблокировать кнопку 3D
                PopulateList(_filtered);
                FitViewport();
                Redraw();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Cursor = Cursors.Arrow; BtnOpen.IsEnabled = true; }
        }

        private void BtnSave_Click(object s, RoutedEventArgs e)
        {
            if (_map == null) return;
            try { _map.Save(); TxtInfo.Text = "Сохранено."; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка"); }
        }

        private void BtnSaveAs_Click(object s, RoutedEventArgs e)
        {
            if (_map == null) return;
            var dlg = new SaveFileDialog { Title = "Сохранить как", Filter = "Map.bytes|Map.bytes|Все файлы|*.*", FileName = "Map.bytes" };
            if (dlg.ShowDialog() != true) return;
            try { _map.Save(dlg.FileName); TxtInfo.Text = $"Сохранено: {dlg.FileName}"; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка"); }
        }

        private void BtnDelete_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null || _map == null) return;
            _map.RemoveObject(_selected);
            _allObjects.Remove(_selected);
            ApplyFilter();
            _selected = null;
            BtnDelete.IsEnabled = false;
            ClearProperties();
            Redraw();
        }

        // ★ Открыть / активировать 3D-окно
        private void BtnView3D_Click(object s, RoutedEventArgs e)
        {
            if (_map == null) return;

            // Если уже открыто — просто поднять наверх
            if (_view3D?.IsVisible == true)
            {
                _view3D.Activate();
                return;
            }

            _view3D = new View3DWindow(
                _allObjects,
                _selected,
                obj => Dispatcher.Invoke(() => SelectObject(obj, scrollTo: true))
            )
            { Owner = this };

            _view3D.Closed += (_, _) => _view3D = null;
            _view3D.Show();
        }

        private void PopulateList(List<MapObject> objects)
        {
            const int MaxShow = 500;
            var items = new System.Collections.ObjectModel.ObservableCollection<MapListItem>();
            int n = Math.Min(objects.Count, MaxShow);
            for (int i = 0; i < n; i++)
                items.Add(new MapListItem { Label = objects[i].ToString(), Obj = objects[i] });
            if (objects.Count > MaxShow)
                items.Add(new MapListItem { Label = $"… ещё {objects.Count - MaxShow:N0} — уточните поиск", Obj = null });

            ListObjects.ItemsSource = items;
            ListObjects.DisplayMemberPath = "Label";
        }

        private void ApplyFilter()
        {
            string f = TxtSearch.Text.ToLowerInvariant().Trim();
            _filtered = string.IsNullOrEmpty(f)
                ? _allObjects
                : _allObjects.Where(o => o.ToString().ToLowerInvariant().Contains(f)).ToList();
            PopulateList(_filtered);
        }

        private void TxtSearch_TextChanged(object s, TextChangedEventArgs e) => ApplyFilter();

        private void ListObjects_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (_suppressSelection) return;
            if (ListObjects.SelectedItem is MapListItem { Obj: MapObject obj })
                SelectObject(obj, scrollTo: true);
        }

        private void Redraw()
        {
            double w = ViewportBorder.ActualWidth;
            double h = ViewportBorder.ActualHeight;
            if (w < 1 || h < 1) return;

            using var dc = _dv.RenderOpen();

            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(26,26,26)), null, new Rect(0,0,w,h));

            DrawGrid(dc, w, h);

            double r = Math.Max(3, Math.Min(8, _zoom * 5));
            double invZoom = 1.0 / _zoom;
            double worldLeft  = (-_panX) * invZoom;
            double worldRight = (w - _panX) * invZoom;
            double worldTop   = (-_panY) * invZoom;
            double worldBot   = (h - _panY) * invZoom;
            double margin     = r * invZoom * 2;

            int drawn = 0;
            foreach (var obj in _filtered)
            {
                double wx = obj.Position.X, wz = obj.Position.Z;
                if (wx < worldLeft - margin || wx > worldRight + margin) continue;
                if (wz < worldTop  - margin || wz > worldBot   + margin) continue;

                var (dot, border) = KC[obj.ObjType?.DetectKind() ?? ObjectKind.Generic];
                bool isSel = obj == _selected;

                var pt = W2C(wx, wz);
                double rr = isSel ? r * 1.6 : r;
                var fill   = new SolidColorBrush(isSel ? Colors.White : dot);
                var stroke = new Pen(new SolidColorBrush(isSel ? Colors.Yellow : border), isSel ? 1.5 : 0.8);
                dc.DrawEllipse(fill, stroke, pt, rr, rr);
                drawn++;
            }

            var axPen = new Pen(new SolidColorBrush(Color.FromArgb(150,100,150,255)), 1.0);
            dc.DrawLine(axPen, W2C(-99999, 0), W2C(99999, 0));
            dc.DrawLine(axPen, W2C(0, -99999), W2C(0, 99999));

            var ft = new FormattedText($"{drawn:N0} / {_filtered.Count:N0} видно",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Consolas"), 11, Brushes.Gray, 1.0);
            dc.DrawText(ft, new Point(8, h - 18));

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(_dv);
            rtb.Freeze();
            _dvImage.Source = rtb;
            _dvImage.Width  = w;
            _dvImage.Height = h;
        }

        private void DrawGrid(DrawingContext dc, double w, double h)
        {
            double[] steps = { 1, 5, 10, 50, 100, 500, 1000, 5000 };
            double targetPixels = 60;
            double worldStep = steps.OrderBy(s => Math.Abs(s * _zoom - targetPixels)).First();
            double bigStep = worldStep * 10;

            var penSmall = new Pen(new SolidColorBrush(Color.FromArgb(30,255,255,255)), 0.5);
            var penBig   = new Pen(new SolidColorBrush(Color.FromArgb(70,255,255,255)), 0.8);

            double invZ = 1.0 / _zoom;
            double startX = Math.Floor((-_panX) * invZ / worldStep) * worldStep;
            double startZ = Math.Floor((-_panY) * invZ / worldStep) * worldStep;
            double endX   = startX + w * invZ + worldStep;
            double endZ   = startZ + h * invZ + worldStep;

            for (double wx = startX; wx <= endX; wx += worldStep)
            {
                var pen = Math.Abs(wx % bigStep) < 0.01 ? penBig : penSmall;
                var p1 = W2C(wx, startZ); var p2 = W2C(wx, endZ);
                dc.DrawLine(pen, p1, p2);
            }
            for (double wz = startZ; wz <= endZ; wz += worldStep)
            {
                var pen = Math.Abs(wz % bigStep) < 0.01 ? penBig : penSmall;
                var p1 = W2C(startX, wz); var p2 = W2C(endX, wz);
                dc.DrawLine(pen, p1, p2);
            }
        }

        private Point W2C(double wx, double wz) => new(_panX + wx * _zoom, _panY + wz * _zoom);
        private (double wx, double wz) C2W(double cx, double cy) => ((cx - _panX) / _zoom, (cy - _panY) / _zoom);

        private void FitViewport()
        {
            if (_allObjects.Count == 0) return;
            double minX = _allObjects.Min(o => (double)o.Position.X);
            double maxX = _allObjects.Max(o => (double)o.Position.X);
            double minZ = _allObjects.Min(o => (double)o.Position.Z);
            double maxZ = _allObjects.Max(o => (double)o.Position.Z);
            double w = Math.Max(ViewportBorder.ActualWidth,  800);
            double h = Math.Max(ViewportBorder.ActualHeight, 600);
            double rx = Math.Max(maxX - minX, 1), rz = Math.Max(maxZ - minZ, 1);
            _zoom = Math.Min(w / rx, h / rz) * 0.85;
            _panX = w / 2 - (minX + maxX) / 2.0 * _zoom;
            _panY = h / 2 - (minZ + maxZ) / 2.0 * _zoom;
        }

        private void Viewport_MouseDown(object s, MouseButtonEventArgs e)
        {
            ViewportCanvas.Focus();
            _lastMouse = e.GetPosition(ViewportCanvas);

            if (e.ChangedButton == MouseButton.Right)
            {
                _isPanning = true;
                ViewportCanvas.CaptureMouse();
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                var hit = HitTestObject(_lastMouse);
                if (hit != null)
                {
                    SelectObject(hit, false);
                    _isDragging = true;
                    ViewportCanvas.CaptureMouse();
                }
                else
                {
                    SelectObject(null, false);
                }
            }
        }

        private void Viewport_MouseMove(object s, MouseEventArgs e)
        {
            var cur = e.GetPosition(ViewportCanvas);
            var (wx, wz) = C2W(cur.X, cur.Y);
            TxtCoords.Text = $"X: {wx:F1}  Z: {wz:F1}";

            if (_isPanning)
            {
                _panX += cur.X - _lastMouse.X;
                _panY += cur.Y - _lastMouse.Y;
                _lastMouse = cur;
                Redraw();
            }
            else if (_isDragging && _selected != null)
            {
                var (wx0, wz0) = C2W(_lastMouse.X, _lastMouse.Y);
                var (wx1, wz1) = C2W(cur.X, cur.Y);
                _selected.Position = new Vector3(
                    _selected.Position.X + (float)(wx1 - wx0),
                    _selected.Position.Y,
                    _selected.Position.Z + (float)(wz1 - wz0));
                _lastMouse = cur;
                _suppressPropChange = true;
                FillTransform(_selected);
                _suppressPropChange = false;
                Redraw();
            }
        }

        private void Viewport_MouseUp(object s, MouseButtonEventArgs e)
        {
            _isPanning = _isDragging = false;
            ViewportCanvas.ReleaseMouseCapture();
        }

        private void Viewport_MouseWheel(object s, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(ViewportCanvas);
            double f = e.Delta > 0 ? 1.18 : 1.0 / 1.18;
            _panX = pos.X + (_panX - pos.X) * f;
            _panY = pos.Y + (_panY - pos.Y) * f;
            _zoom *= f;
            Redraw();
        }

        private void Viewport_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) BtnDelete_Click(s, new RoutedEventArgs());
            if (e.Key == Key.F) { FitViewport(); Redraw(); }
        }

        private MapObject? HitTestObject(Point screenPt)
        {
            double threshold = Math.Max(6, 8 / _zoom);
            MapObject? best  = null;
            double     bestD = threshold * threshold;
            var (mx, mz) = C2W(screenPt.X, screenPt.Y);
            foreach (var obj in _filtered)
            {
                double dx = obj.Position.X - mx, dz = obj.Position.Z - mz;
                double d2 = dx*dx + dz*dz;
                if (d2 < bestD) { bestD = d2; best = obj; }
            }
            return best;
        }

        private void SelectObject(MapObject? obj, bool scrollTo)
        {
            _selected = obj;
            BtnDelete.IsEnabled = obj != null;
            BtnEdit.IsEnabled   = obj != null;

            if (scrollTo && obj != null)
            {
                var pt = W2C(obj.Position.X, obj.Position.Z);
                double w = ViewportBorder.ActualWidth, h = ViewportBorder.ActualHeight;
                if (pt.X < 0 || pt.X > w || pt.Y < 0 || pt.Y > h)
                {
                    _panX = w / 2 - obj.Position.X * _zoom;
                    _panY = h / 2 - obj.Position.Z * _zoom;
                }
            }

            _suppressSelection = true;
            ListObjects.SelectedItem = null;
            if (obj != null && ListObjects.ItemsSource != null)
                foreach (MapListItem item in ListObjects.ItemsSource)
                    if (item.Obj == obj) { ListObjects.SelectedItem = item; break; }
            _suppressSelection = false;

            FillProperties(obj);
            Redraw();

            // ★ Синхронизировать выделение с 3D-окном
            _view3D?.SetSelection(obj);
        }

        private void FillProperties(MapObject? obj)
        {
            _suppressPropChange = true;
            ExtraProps.Children.Clear();

            if (obj == null) { ClearProperties(); _suppressPropChange = false; return; }

            FillTransform(obj);

            var kind = obj.ObjType?.DetectKind() ?? ObjectKind.Generic;
            TxtObjType.Text = $"Тип: {obj.ObjType?.DisplayName ?? "?"}\n" +
                              $"Kind: {kind}\n" +
                              $"GUID: {obj.ObjType?.Guid ?? "—"}";

            if      (obj.Data is SpawnCarData   sc) BuildSpawnCarProps(sc);
            else if (obj.Data is SpawnHumanData sh) BuildSpawnHumanProps(sh);
            else if (obj.Data is WireObjectData wd) BuildWireProps(wd);
            else if (obj.Data is DecalObjectData dd)BuildDecalProps(dd);

            _suppressPropChange = false;
        }

        private void FillTransform(MapObject obj)
        {
            TbPosX.Text=F(obj.Position.X); TbPosY.Text=F(obj.Position.Y); TbPosZ.Text=F(obj.Position.Z);
            TbRotX.Text=F(obj.Rotation.X); TbRotY.Text=F(obj.Rotation.Y); TbRotZ.Text=F(obj.Rotation.Z);
            TbScaleX.Text=F(obj.Scale.X); TbScaleY.Text=F(obj.Scale.Y); TbScaleZ.Text=F(obj.Scale.Z);
        }

        private void ClearProperties()
        {
            TbPosX.Text=TbPosY.Text=TbPosZ.Text="";
            TbRotX.Text=TbRotY.Text=TbRotZ.Text="";
            TbScaleX.Text=TbScaleY.Text=TbScaleZ.Text="";
            TxtObjType.Text=""; ExtraProps.Children.Clear();
        }

        private void Transform_Changed(object s, TextChangedEventArgs e)
        {
            if (_suppressPropChange || _selected == null) return;
            _selected.Position = new Vector3(P(TbPosX.Text), P(TbPosY.Text), P(TbPosZ.Text));
            _selected.Rotation = new Vector3(P(TbRotX.Text), P(TbRotY.Text), P(TbRotZ.Text));
            _selected.Scale    = new Vector3(P(TbScaleX.Text,1f), P(TbScaleY.Text,1f), P(TbScaleZ.Text,1f));
            Redraw();
        }

        private void BuildSpawnCarProps(SpawnCarData d)
        {
            AddHeader("SpawnCar");
            AddEnum("Время дня", new[]{"Любое","Только день","Только ночь"}, d.Time, v=>d.Time=(byte)v);
            AddInt("Цена мин", d.MinK, v=>d.MinK=(byte)Math.Clamp(v,0,150));
            AddInt("Цена макс", d.MaxK, v=>d.MaxK=(byte)Math.Clamp(v,0,150));
            AddEnum("Спортивные",new[]{"Любые","Только","Исключить"}, d.Sport,  v=>d.Sport=(byte)v);
            AddEnum("Сельские",  new[]{"Любые","Только","Исключить"}, d.Selo,   v=>d.Selo=(byte)v);
            AddEnum("Русские",   new[]{"Любые","Только","Исключить"}, d.Russian,v=>d.Russian=(byte)v);
            AddEnum("Джип",      new[]{"Любые","Только","Исключить"}, d.Jip,    v=>d.Jip=(byte)v);
            AddEnum("Камаз",     new[]{"Любые","Только","Исключить"}, d.Kamaz,  v=>d.Kamaz=(byte)v);
        }

        private void BuildSpawnHumanProps(SpawnHumanData d)
        {
            AddHeader("SpawnHuman");
            AddEnum("Время дня",new[]{"Любое","Только день","Только ночь"}, d.Time, v=>d.Time=(byte)v);
            AddEnum("Пол",       new[]{"Любой","Женщина","Мужчина"}, d.Famaly, v=>d.Famaly=(byte)v);
            AddEnum("Голые",     new[]{"Любые","Только","Исключить"}, d.Naked,       v=>d.Naked=(byte)v);
            AddEnum("Костюм",    new[]{"Любые","Только","Исключить"}, d.Suit,        v=>d.Suit=(byte)v);
            AddEnum("Рабочий",   new[]{"Любые","Только","Исключить"}, d.Worker,      v=>d.Worker=(byte)v);
            AddEnum("Проститутки",new[]{"Любые","Только","Исключить"}, d.Prostitutka,v=>d.Prostitutka=(byte)v);
        }

        private void BuildWireProps(WireObjectData d)
        {
            AddHeader("Wire");
            AddLabel($"ID: {d.Id}");
            AddLabel($"Соединений: {d.ConnectToIds?.Length ?? 0}");
        }

        private void BuildDecalProps(DecalObjectData d)
        {
            AddHeader("Decal");
            AddFloat("Макс угол",     d.MaxAngle,     v=>d.MaxAngle=v);
            AddFloat("Push distance", d.PushDistance, v=>d.PushDistance=v);
            AddFloat("Opacity",       d.Opacity,      v=>d.Opacity=Math.Clamp(v,0,1));
            AddLabel($"Сплайн: {d.IsSpline}  Слой: {d.OrderLayer}");
        }

        private void AddHeader(string t) =>
            ExtraProps.Children.Add(new TextBlock { Text=t, Foreground=new SolidColorBrush(Color.FromRgb(100,170,255)),
                FontWeight=FontWeights.SemiBold, Margin=new Thickness(0,8,0,4) });

        private void AddLabel(string t) =>
            ExtraProps.Children.Add(new TextBlock { Text=t, Foreground=Brushes.DarkGray, FontSize=11, Margin=new Thickness(0,2,0,0) });

        private void AddFloat(string lbl, float val, Action<float> set)
        {
            var row = Row(lbl); var tb = new TextBox { Text=F(val), Background=Brushes.Transparent, Foreground=Brushes.LightGray };
            tb.TextChanged += (_,_) => { if (!_suppressPropChange && float.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) set(v); };
            row.Children.Add(tb); ExtraProps.Children.Add(row);
        }

        private void AddInt(string lbl, int val, Action<int> set)
        {
            var row = Row(lbl); var tb = new TextBox { Text=val.ToString(), Background=Brushes.Transparent, Foreground=Brushes.LightGray };
            tb.TextChanged += (_,_) => { if (!_suppressPropChange && int.TryParse(tb.Text, out int v)) set(v); };
            row.Children.Add(tb); ExtraProps.Children.Add(row);
        }

        private void AddEnum(string lbl, string[] opts, byte cur, Action<int> set)
        {
            var row = Row(lbl); var cb = new ComboBox
            { Background=new SolidColorBrush(Color.FromRgb(60,60,60)), Foreground=Brushes.LightGray,
              BorderBrush=new SolidColorBrush(Color.FromRgb(80,80,80)), SelectedIndex=Math.Clamp(cur,0,opts.Length-1) };
            foreach (var o in opts) cb.Items.Add(o);
            cb.SelectionChanged += (_,_) => { if (!_suppressPropChange) set(cb.SelectedIndex); };
            row.Children.Add(cb); ExtraProps.Children.Add(row);
        }

        private static StackPanel Row(string lbl)
        {
            var p = new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(0,2,0,0) };
            p.Children.Add(new TextBlock { Text=lbl+":", Width=110, Foreground=new SolidColorBrush(Color.FromRgb(160,160,160)),
                VerticalAlignment=VerticalAlignment.Center, FontSize=11 });
            return p;
        }

        private static string F(float v) => v.ToString("G6", CultureInfo.InvariantCulture);
        private static float P(string s, float fb=0f) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fb;

        private void BtnInfo_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Window
            {
                Title = "Info",
                Width = 360, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(30,30,30))
            };
            var sp = new StackPanel { Margin = new Thickness(24) };
            sp.Children.Add(new TextBlock
            {
                Text = "Hall of Fame",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100,170,255)),
                Margin = new Thickness(0,0,0,12)
            });
            sp.Children.Add(new TextBlock
            {
                Text = "@Catir1337 aka Уехал на остров! aka Станислав Абдулов",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0,0,0,8)
            });
            var link = new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run("https://github.com/catir1337"))
            {
                NavigateUri = new Uri("https://github.com/catir1337")
            };
            link.RequestNavigate += (_, ev) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ev.Uri.AbsoluteUri, UseShellExecute = true });
            };
            var tb = new TextBlock { Foreground = Brushes.CornflowerBlue };
            tb.Inlines.Add(link);
            sp.Children.Add(tb);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private void BtnAddType_Click(object s, RoutedEventArgs e)
        {
            if (_map == null) return;

            var dlg = new Views.NewTypeDialog { Owner = this };
            if (dlg.ShowDialog() != true) return;

            string objPath = dlg.ObjPath;
            string guid    = dlg.Guid;

            var existing = _map.ObjectTypes.FirstOrDefault(t =>
                string.Equals(t.ObjPath, objPath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                MessageBox.Show(
                    $"Такой тип уже есть:\nID={existing.Id}  Путь={existing.ObjPath}",
                    "Уже существует", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newType = _map.GetOrCreateType(objPath, guid);
            TxtInfo.Text = $"Тип зарегистрирован: ID={newType.Id}  «{newType.DisplayName}»";
        }

        private void BtnDeleteType_Click(object s, RoutedEventArgs e)
        {
            if (_map == null) return;

            if (_map.ObjectTypes.Count == 0)
            {
                MessageBox.Show("Нет зарегистрированных типов.", "Удаление типа",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Диалог выбора типа
            var dlg = new Window
            {
                Title  = "Удалить тип объекта",
                Width  = 520, Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner  = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var sp = new StackPanel { Margin = new Thickness(16) };

            sp.Children.Add(new TextBlock
            {
                Text = "Выбери тип для удаления:",
                Foreground = Brushes.LightGray,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var lb = new ListBox
            {
                Height = 260,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Foreground = Brushes.LightGray,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Margin = new Thickness(0, 0, 0, 8)
            };

            // Подсчитываем сколько объектов у каждого типа
            var allObjs = _map.GetAllObjects();
            foreach (var t in _map.ObjectTypes)
            {
                int usedCount = allObjs.Count(o => o.ObjInfoId == t.Id);
                string label = $"[{t.Id}]  {t.DisplayName}";
                if (usedCount > 0) label += $"  ⚠ используется в {usedCount} объектах";
                lb.Items.Add(new TypeListItem { Label = label, Type = t, UsedCount = usedCount });
            }
            lb.DisplayMemberPath = "Label";
            sp.Children.Add(lb);

            var warn = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 50)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 11
            };
            sp.Children.Add(warn);

            lb.SelectionChanged += (_, _) =>
            {
                if (lb.SelectedItem is TypeListItem item && item.UsedCount > 0)
                    warn.Text = $"⚠ Внимание: {item.UsedCount} объектов используют этот тип. " +
                                "После удаления типа они останутся на карте но потеряют ссылку.";
                else
                    warn.Text = "";
            };

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right };

            var btnDel = new Button
            {
                Content = "🗑 Удалить", Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(160, 40, 40)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 60, 60))
            };
            var btnCancel = new Button
            {
                Content = "Отмена", Padding = new Thickness(16, 6, 16, 6),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.LightGray
            };

            bool confirmed = false;
            btnDel.Click += (_, _) =>
            {
                if (lb.SelectedItem == null)
                {
                    MessageBox.Show("Выбери тип из списка!", "Удаление типа",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                confirmed = true;
                dlg.Close();
            };
            btnCancel.Click += (_, _) => dlg.Close();

            btnRow.Children.Add(btnDel);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            dlg.Content = sp;
            dlg.ShowDialog();

            if (!confirmed || lb.SelectedItem is not TypeListItem selected) return;

            var typeToDelete = selected.Type;

            // Удаляем тип
            _map.ObjectTypes.Remove(typeToDelete);
            _map.ObjTypeById.Remove(typeToDelete.Id);

            TxtInfo.Text = $"Тип удалён: [{typeToDelete.Id}] «{typeToDelete.DisplayName}»" +
                           (selected.UsedCount > 0 ? $" (было {selected.UsedCount} объектов)" : "");

            // Обновляем список если выбранный объект использовал этот тип
            if (_selected?.ObjInfoId == typeToDelete.Id)
            {
                _selected.ObjType = null;
                FillProperties(_selected);
            }

            ApplyFilter();
            Redraw();
        }

        private void BtnAdd_Click(object s, RoutedEventArgs e)
        {
            if (_map == null) return;
            var dlg = new ObjectDialog(_map.ObjectTypes) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            var obj = dlg.Result;
            _map.AddObject(obj);
            _allObjects.Add(obj);
            ApplyFilter();
            SelectObject(obj, scrollTo: true);
            TxtInfo.Text = $"Добавлен: {obj}";
        }

        private void BtnEdit_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null || _map == null) return;
            var dlg = new ObjectDialog(_map.ObjectTypes, _selected) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            ApplyFilter();
            FillProperties(_selected);
            Redraw();
            TxtInfo.Text = $"Изменён: {_selected}";
        }

        private void CtxDelete_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null || _map == null) return;
            var res = MessageBox.Show($"Удалить '{_selected}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
            _map.RemoveObject(_selected);
            _allObjects.Remove(_selected);
            ApplyFilter();
            _selected = null;
            BtnDelete.IsEnabled = false;
            ClearProperties();
            Redraw();
        }

        private void CtxFocus_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            double w = ViewportBorder.ActualWidth;
            double h = ViewportBorder.ActualHeight;
            _panX = w / 2 - _selected.Position.X * _zoom;
            _panY = h / 2 - _selected.Position.Z * _zoom;
            Redraw();
        }

        private void CtxCopyName_Click(object s, RoutedEventArgs e)
        {
            if (_selected != null)
                Clipboard.SetText(_selected.ObjType?.DisplayName ?? _selected.ToString());
        }

    }

    public class MapListItem
    {
        public string  Label { get; set; } = "";
        public MapObject? Obj { get; set; }
        public override string ToString() => Label;
    }

    public class TypeListItem
    {
        public string  Label    { get; set; } = "";
        public MapObjectType Type { get; set; } = null!;
        public int     UsedCount { get; set; }
        public override string ToString() => Label;
    }
}
