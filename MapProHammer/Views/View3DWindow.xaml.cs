// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using MapProHammer.Model;

namespace MapProHammer.Views
{
    public partial class View3DWindow : Window
    {
        // ─── Цвета по типу ───────────────────────────────────────────────
        private static readonly Dictionary<ObjectKind, Color> KindColor = new()
        {
            [ObjectKind.Generic]    = Color.FromRgb( 90,  90, 110),
            [ObjectKind.SpawnCar]   = Color.FromRgb( 50, 110, 200),
            [ObjectKind.SpawnHuman] = Color.FromRgb( 40, 170,  60),
            [ObjectKind.Wire]       = Color.FromRgb(200, 140,  30),
            [ObjectKind.Decal]      = Color.FromRgb(160,  40, 160),
        };

        // ─── Данные ──────────────────────────────────────────────────────
        private readonly List<MapObject>    _objects;
        private readonly Action<MapObject>? _onSelect;
        private          MapObject?         _selected;

        // ─── Камера ──────────────────────────────────────────────────────
        private Point3D _camTarget;
        private double  _camRadius = 500;
        private double  _camTheta  = Math.PI / 4;
        private double  _camPhi    = Math.PI / 6;
        private Point3D _initTarget;
        private double  _initRadius;

        // ─── Мышь ────────────────────────────────────────────────────────
        private Point _mouseDownPt, _lastMousePt;
        private bool  _isOrbiting, _isPanning, _hasDragged;

        // ─── Метрики сцены ───────────────────────────────────────────────
        private double _sceneSpan = 1000;
        private double _cubeSize  = 5;

        // ─────────────────────────────────────────────────────────────────

        public View3DWindow(List<MapObject>    objects,
                            MapObject?         selected = null,
                            Action<MapObject>? onSelect = null)
        {
            InitializeComponent();
            _objects  = objects;
            _selected = selected;
            _onSelect = onSelect;
            Loaded += (_, _) => { Viewport.Focus(); _ = BuildSceneAsync(); };
        }

        public void SetSelection(MapObject? obj)
        {
            _selected = obj;
            RefreshSelectionVisual();
            TxtSelInfo.Text = obj?.ToString() ?? string.Empty;
        }

        // ══════════════════════════════════════════════════════════════════
        //  Построение сцены (async — тяжёлое в фоне)
        // ══════════════════════════════════════════════════════════════════

        private async Task BuildSceneAsync()
        {
            TxtCount.Text         = "Загрузка…";
            GridVisual.Content    = null;
            ObjectsVisual.Content = null;
            AxesVisual.Content    = null;

            if (_objects.Count == 0) { UpdateCamera(); return; }

            // ── Gabariты (быстро, на UI-потоке) ─────────────────────────
            float minX = _objects.Min(o => o.Position.X), maxX = _objects.Max(o => o.Position.X);
            float minY = _objects.Min(o => o.Position.Y), maxY = _objects.Max(o => o.Position.Y);
            float minZ = _objects.Min(o => o.Position.Z), maxZ = _objects.Max(o => o.Position.Z);

            double cx = (minX + maxX) / 2.0;
            double cy = (minY + maxY) / 2.0;
            double cz = (minZ + maxZ) / 2.0;

            _sceneSpan  = Math.Max(Math.Max(maxX - minX, maxY - minY), Math.Max(maxZ - minZ, 1.0));
            _cubeSize   = Math.Max(_sceneSpan / 120.0, 0.5);
            _camTarget  = new Point3D(cx, cy, cz);
            _camRadius  = _sceneSpan * 1.25;
            _camTheta   = Math.PI / 4;
            _camPhi     = Math.PI / 6;
            _initTarget = _camTarget;
            _initRadius = _camRadius;

            // Снапшот данных для фонового потока
            var snapshot  = _objects.ToList();
            double cs     = _cubeSize;
            float  gridY  = minY;

            // ── Тяжёлая работа в фоне ────────────────────────────────────
            // Строим 5 мешей (по одному на ObjectKind) + меш сетки
            // MeshGeometry3D создаётся и замораживается в фоне — WPF это поддерживает
            var (kindMeshes, gridMesh) = await Task.Run(() =>
                BuildGeometryBackground(snapshot, cs, minX, maxX, minZ, maxZ, gridY));

            // ── Объекты (5 GeometryModel3D вместо тысяч) ────────────────
            var objGroup = new Model3DGroup();
            foreach (var (kind, mesh) in kindMeshes)
            {
                var mat = new DiffuseMaterial(new SolidColorBrush(KindColor[kind]));
                var geo = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
                objGroup.Children.Add(geo);
            }
            ObjectsVisual.Content = objGroup;

            // ── Сетка ────────────────────────────────────────────────────
            if (ChkGrid.IsChecked == true && gridMesh != null)
            {
                var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(85, 130, 130, 150)));
                var geo = new GeometryModel3D(gridMesh, mat) { BackMaterial = mat };
                var grp = new Model3DGroup();
                grp.Children.Add(geo);
                GridVisual.Content = grp;
            }

            // ── Оси ──────────────────────────────────────────────────────
            if (ChkAxes.IsChecked == true)
                AxesVisual.Content = BuildAxes(cx, cy, cz, _sceneSpan * 0.18);

            RefreshSelectionVisual();
            UpdateCamera();
            TxtCount.Text = $"{_objects.Count:N0} объектов";
        }

        // ── Вся геометрия в фоновом потоке ──────────────────────────────

        private static (Dictionary<ObjectKind, MeshGeometry3D> kinds, MeshGeometry3D? grid)
            BuildGeometryBackground(
                List<MapObject> objects, double cs,
                float minX, float maxX, float minZ, float maxZ, float baseY)
        {
            // ── Объекты: один меш на kind ────────────────────────────────
            var builders = new Dictionary<ObjectKind, MeshBuilder>();
            foreach (ObjectKind k in Enum.GetValues<ObjectKind>())
                builders[k] = new MeshBuilder();

            foreach (var obj in objects)
            {
                var kind = obj.ObjType?.DetectKind() ?? ObjectKind.Generic;
                builders[kind].AddBox(obj.Position.X, obj.Position.Y, obj.Position.Z, cs);
            }

            var kindMeshes = new Dictionary<ObjectKind, MeshGeometry3D>();
            foreach (var (kind, mb) in builders)
            {
                if (mb.IsEmpty) continue;
                var mesh = mb.ToMesh();
                mesh.Freeze();
                kindMeshes[kind] = mesh;
            }

            // ── Сетка ────────────────────────────────────────────────────
            MeshGeometry3D? gridMesh = null;
            {
                float span = Math.Max(Math.Max(maxX - minX, maxZ - minZ), 100f);
                float step = NiceStep(span, 10);
                float th   = step * 0.012f;
                float y    = baseY - th * 0.5f;
                float ext  = step * 2;
                float gx0  = (float)Math.Floor((minX - ext) / step) * step;
                float gx1  = (float)Math.Ceiling((maxX + ext) / step) * step;
                float gz0  = (float)Math.Floor((minZ - ext) / step) * step;
                float gz1  = (float)Math.Ceiling((maxZ + ext) / step) * step;

                var mb = new MeshBuilder();
                var n  = new Vector3D(0, 1, 0);

                for (float x = gx0; x <= gx1 + 0.001f; x += step)
                    mb.AddQuad(x - th, gz0, x + th, gz1, y, n);
                for (float z = gz0; z <= gz1 + 0.001f; z += step)
                    mb.AddQuad(gx0, z - th, gx1, z + th, y, n);

                gridMesh = mb.ToMesh();
                gridMesh.Freeze();
            }

            return (kindMeshes, gridMesh);
        }

        // ── Подсветка выделения (маленький отдельный куб) ───────────────

        private void RefreshSelectionVisual()
        {
            if (_selected == null)
            {
                // Скрыть, вернув пустую группу
                var empty = new Model3DGroup();
                // Найти SelectionVisual — добавим его через XAML x:Name если понадобится
                // Пока обновим через ObjectsVisual контент нет, используем AxesVisual нет —
                // На самом деле SelectionVisual есть в XAML:
                SelectionVisual.Content = null;
                TxtSelInfo.Text = string.Empty;
                return;
            }

            double s    = _cubeSize * 1.6;
            var    mb   = new MeshBuilder();
            mb.AddBox(_selected.Position.X, _selected.Position.Y, _selected.Position.Z, s);
            var mesh    = mb.ToMesh();
            var mat     = new EmissiveMaterial(new SolidColorBrush(Colors.Yellow));
            var geo     = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            var grp     = new Model3DGroup();
            grp.Children.Add(geo);
            SelectionVisual.Content = grp;
            TxtSelInfo.Text = _selected.ToString();
        }

        // ── Оси XYZ ─────────────────────────────────────────────────────

        private static Model3DGroup BuildAxes(double cx, double cy, double cz, double len)
        {
            double r = Math.Max(len * 0.018, 0.5);
            var grp = new Model3DGroup();
            grp.Children.Add(MakeAxisCylinder(cx, cy, cz, len, 0,   0,   r, Colors.Red));
            grp.Children.Add(MakeAxisCylinder(cx, cy, cz, 0,   len, 0,   r, Colors.LimeGreen));
            grp.Children.Add(MakeAxisCylinder(cx, cy, cz, 0,   0,   len, r, Colors.DodgerBlue));
            return grp;
        }

        private static GeometryModel3D MakeAxisCylinder(
            double ox, double oy, double oz,
            double dx, double dy, double dz,
            double r, Color color)
        {
            var mb = new MeshBuilder();
            mb.AddCylinder(r, 1.0, 10);
            var mesh = mb.ToMesh();

            double len = Math.Sqrt(dx*dx + dy*dy + dz*dz);
            var dir = new Vector3D(dx/len, dy/len, dz/len);
            var def = new Vector3D(0, 1, 0);

            var tg = new Transform3DGroup();
            tg.Children.Add(new ScaleTransform3D(1, len, 1));
            var axis = Vector3D.CrossProduct(def, dir);
            if (axis.LengthSquared > 1e-6)
                tg.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(axis, Vector3D.AngleBetween(def, dir))));
            tg.Children.Add(new TranslateTransform3D(ox + dx/2, oy + dy/2, oz + dz/2));

            var mat = new DiffuseMaterial(new SolidColorBrush(color));
            return new GeometryModel3D(mesh, mat) { BackMaterial = mat, Transform = tg };
        }

        // ══════════════════════════════════════════════════════════════════
        //  Камера
        // ══════════════════════════════════════════════════════════════════

        private void UpdateCamera()
        {
            double cosP = Math.Cos(_camPhi), sinP = Math.Sin(_camPhi);
            double cosT = Math.Cos(_camTheta), sinT = Math.Sin(_camTheta);
            var pos = new Point3D(
                _camTarget.X + _camRadius * cosP * sinT,
                _camTarget.Y + _camRadius * sinP,
                _camTarget.Z + _camRadius * cosP * cosT);
            Camera.Position          = pos;
            Camera.LookDirection     = _camTarget - pos;
            Camera.UpDirection       = new Vector3D(0, 1, 0);
            Camera.NearPlaneDistance = Math.Max(0.01, _camRadius * 0.001);
            Camera.FarPlaneDistance  = _camRadius * 200;
        }

        private void FitCamera()
        {
            _camTarget = _initTarget; _camRadius = _initRadius;
            _camTheta  = Math.PI / 4; _camPhi    = Math.PI / 6;
            UpdateCamera();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Мышь
        // ══════════════════════════════════════════════════════════════════

        private void Vp_MouseDown(object s, MouseButtonEventArgs e)
        {
            Viewport.Focus();
            _mouseDownPt = e.GetPosition(Viewport);
            _lastMousePt = _mouseDownPt;
            _hasDragged  = false;
            if (e.ChangedButton == MouseButton.Left)  _isOrbiting = true;
            if (e.ChangedButton == MouseButton.Right) _isPanning  = true;
            Viewport.CaptureMouse();
        }

        private void Vp_MouseMove(object s, MouseEventArgs e)
        {
            var cur = e.GetPosition(Viewport);
            double dx = cur.X - _lastMousePt.X, dy = cur.Y - _lastMousePt.Y;
            if (Math.Abs(cur.X - _mouseDownPt.X) + Math.Abs(cur.Y - _mouseDownPt.Y) > 3)
                _hasDragged = true;

            if (_isOrbiting && _hasDragged)
            {
                _camTheta -= dx * 0.007;
                _camPhi    = Math.Clamp(_camPhi - dy * 0.007, -1.55, 1.55);
                UpdateCamera();
            }
            else if (_isPanning && _hasDragged)
            {
                var dir   = Camera.LookDirection; dir.Normalize();
                var up    = Camera.UpDirection;   up.Normalize();
                var right = Vector3D.CrossProduct(dir, up); right.Normalize();
                double sc = _camRadius * 0.0013;
                _camTarget.X -= (right.X*dx + up.X*dy) * sc;
                _camTarget.Y -= (right.Y*dx + up.Y*dy) * sc;
                _camTarget.Z -= (right.Z*dx + up.Z*dy) * sc;
                UpdateCamera();
            }
            _lastMousePt = cur;
        }

        private void Vp_MouseUp(object s, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !_hasDragged)
                DoRaycast(e.GetPosition(Viewport));
            _isOrbiting = _isPanning = false;
            Viewport.ReleaseMouseCapture();
        }

        private void Vp_MouseWheel(object s, MouseWheelEventArgs e)
        {
            double f = e.Delta > 0 ? 0.85 : 1.0 / 0.85;
            _camRadius = Math.Max(_cubeSize * 2, _camRadius * f);
            UpdateCamera();
        }

        private void Vp_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.F) FitCamera();
        }

        // ── Ручной рейкаст (работает с merged mesh) ─────────────────────

        private void DoRaycast(Point screenPt)
        {
            var (rayOrigin, rayDir) = GetCameraRay(screenPt);
            double halfSize = _cubeSize * 0.65;

            MapObject? closest = null;
            double     closestT = double.MaxValue;

            foreach (var obj in _objects)
            {
                if (RayBoxIntersect(rayOrigin, rayDir,
                    obj.Position.X, obj.Position.Y, obj.Position.Z,
                    halfSize, out double t) && t < closestT)
                {
                    closestT = t;
                    closest  = obj;
                }
            }

            if (closest == null) return;
            _onSelect?.Invoke(closest);
            SetSelection(closest);
        }

        private (Point3D origin, Vector3D dir) GetCameraRay(Point screenPt)
        {
            double w = Viewport.ActualWidth, h = Viewport.ActualHeight;
            double nx = (2.0 * screenPt.X / w) - 1.0;
            double ny = 1.0 - (2.0 * screenPt.Y / h);

            var fwd   = Camera.LookDirection; fwd.Normalize();
            var up    = Camera.UpDirection;   up.Normalize();
            var right = Vector3D.CrossProduct(fwd, up); right.Normalize();

            double tanHalf = Math.Tan(Camera.FieldOfView * Math.PI / 360.0);
            double aspect  = h > 0 ? w / h : 1.0;

            var dir = fwd + right * (nx * tanHalf * aspect) + up * (ny * tanHalf);
            dir.Normalize();
            return (Camera.Position, dir);
        }

        private static bool RayBoxIntersect(
            Point3D ro, Vector3D rd,
            double cx, double cy, double cz, double half,
            out double tHit)
        {
            double tx1 = (cx - half - ro.X) / rd.X, tx2 = (cx + half - ro.X) / rd.X;
            double tmin = Math.Min(tx1, tx2),         tmax = Math.Max(tx1, tx2);
            double ty1 = (cy - half - ro.Y) / rd.Y,  ty2 = (cy + half - ro.Y) / rd.Y;
            tmin = Math.Max(tmin, Math.Min(ty1, ty2)); tmax = Math.Min(tmax, Math.Max(ty1, ty2));
            double tz1 = (cz - half - ro.Z) / rd.Z,  tz2 = (cz + half - ro.Z) / rd.Z;
            tmin = Math.Max(tmin, Math.Min(tz1, tz2)); tmax = Math.Min(tmax, Math.Max(tz1, tz2));
            tHit = tmin;
            return tmax >= Math.Max(0.0, tmin);
        }

        // ── Хелпер ──────────────────────────────────────────────────────

        private static float NiceStep(float span, int n)
        {
            float raw  = span / n;
            float[] ns = { 1,2,5,10,20,50,100,200,500,1000,2000,5000,10000 };
            return ns.OrderBy(s => Math.Abs(s - raw)).First();
        }

        // ══════════════════════════════════════════════════════════════════
        //  Toolbar
        // ══════════════════════════════════════════════════════════════════

        private void BtnFit_Click(object s, RoutedEventArgs e) => FitCamera();

        private void Rebuild_Changed(object s, RoutedEventArgs e)
        {
            if (IsLoaded) _ = BuildSceneAsync();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MeshBuilder — строится в фоновом потоке
    // ══════════════════════════════════════════════════════════════════════

    internal sealed class MeshBuilder
    {
        private readonly List<Point3D>  _pos = new();
        private readonly List<Vector3D> _nor = new();
        private readonly List<int>      _idx = new();

        public bool IsEmpty => _pos.Count == 0;

        public void AddBox(double cx, double cy, double cz, double s)
        {
            double hx = s/2, hy = s/2, hz = s/2;
            Point3D[] v =
            {
                new(cx-hx, cy-hy, cz-hz), new(cx+hx, cy-hy, cz-hz),
                new(cx+hx, cy+hy, cz-hz), new(cx-hx, cy+hy, cz-hz),
                new(cx-hx, cy-hy, cz+hz), new(cx+hx, cy-hy, cz+hz),
                new(cx+hx, cy+hy, cz+hz), new(cx-hx, cy+hy, cz+hz),
            };
            (int a,int b,int c,int d,Vector3D n)[] faces =
            {
                (0,1,2,3, new Vector3D( 0, 0,-1)),
                (5,4,7,6, new Vector3D( 0, 0, 1)),
                (4,0,3,7, new Vector3D(-1, 0, 0)),
                (1,5,6,2, new Vector3D( 1, 0, 0)),
                (3,2,6,7, new Vector3D( 0, 1, 0)),
                (4,5,1,0, new Vector3D( 0,-1, 0)),
            };
            foreach (var (a,b,c,d,n) in faces) AddQuadVerts(v[a],v[b],v[c],v[d],n);
        }

        public void AddCylinder(double radius, double height, int sides)
        {
            double hy = height / 2;
            for (int i = 0; i < sides; i++)
            {
                double a0 = 2*Math.PI*i/sides, a1 = 2*Math.PI*(i+1)/sides;
                double x0 = Math.Cos(a0)*radius, z0 = Math.Sin(a0)*radius;
                double x1 = Math.Cos(a1)*radius, z1 = Math.Sin(a1)*radius;
                var n = new Vector3D((x0+x1)/2, 0, (z0+z1)/2); n.Normalize();
                AddQuadVerts(
                    new Point3D(x0,-hy,z0), new Point3D(x1,-hy,z1),
                    new Point3D(x1, hy,z1), new Point3D(x0, hy,z0), n);
            }
        }

        public void AddQuad(double ax, double az, double bx, double bz, double y, Vector3D n)
        {
            AddQuadVerts(
                new Point3D(ax,y,az), new Point3D(bx,y,az),
                new Point3D(bx,y,bz), new Point3D(ax,y,bz), n);
        }

        private void AddQuadVerts(Point3D p0, Point3D p1, Point3D p2, Point3D p3, Vector3D n)
        {
            int s = _pos.Count;
            _pos.Add(p0); _nor.Add(n);
            _pos.Add(p1); _nor.Add(n);
            _pos.Add(p2); _nor.Add(n);
            _pos.Add(p3); _nor.Add(n);
            _idx.Add(s); _idx.Add(s+1); _idx.Add(s+2);
            _idx.Add(s); _idx.Add(s+2); _idx.Add(s+3);
        }

        public MeshGeometry3D ToMesh()
        {
            var m = new MeshGeometry3D
            {
                Positions       = new Point3DCollection(_pos),
                Normals         = new Vector3DCollection(_nor),
                TriangleIndices = new Int32Collection(_idx),
            };
            return m;
        }
    }
}
