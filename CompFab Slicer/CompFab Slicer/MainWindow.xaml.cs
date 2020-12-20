using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ClipperLib;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace CompFab_Slicer
{
    public partial class MainWindow : Window
    {
        public MeshGeometry3D modelMesh;
        public List<List<List<Point3DCollection>>> slicedPolygons;
        public List<PolyTree> treeList;
        public List<Paths> infillPerLayer;
        private double layerHeight;
        private double nozzleDiameter;
        private double numberOfShells;
        private double infill;
        private double initTemp;
        private double initBedTemp;
        private double printTemp;
        private double bedTemp;
        private double printingSpeed;
        private string adhesionMode;
        double centerXOfModel;
        double centerYOfModel;
        private System.Windows.Shapes.Polygon Canvas2DPolygon = new System.Windows.Shapes.Polygon();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openSTLFile = new OpenFileDialog();
            openSTLFile.Filter = "stl files (*.stl)|*.*";

            if (openSTLFile.ShowDialog() == true)
            {
                string filePath = @openSTLFile.FileName;
                ShowSTL(filePath);
            }
        }


        public void ShowSTL(string file)
        {
            var visualModel = VisualModel;

            Model3DGroup modelGroup = new Model3DGroup();
            StLReader reader = new StLReader();
            Model3DGroup group = reader.Read(file);
            GeometryModel3D geoModel = FindLargestModel(group);

            geoModel.Material = MaterialHelper.CreateMaterial(Colors.Yellow);
            geoModel.BackMaterial = MaterialHelper.CreateMaterial(Colors.Gray);

            Rect3D bounds = geoModel.Bounds;
            double offsetX;
            double offsetY;
            double offsetZ;
            centerXOfModel = bounds.SizeX / 2;
            centerYOfModel = bounds.SizeY / 2;

            if (bounds.X <= 0)
            {
                offsetX = Math.Abs(bounds.X);
            }
            else
            {
                offsetX = -Math.Abs(bounds.X);
            }

            if (bounds.Y <= 0)
            {
                offsetY = Math.Abs(bounds.Y);
            }
            else
            {
                offsetY = -Math.Abs(bounds.Y);
            }

            if (bounds.Z <= 0)
            {
                offsetZ = Math.Abs(bounds.Z);
            }
            else
            {
                offsetZ = -Math.Abs(bounds.Z);
            }
            modelMesh = geoModel.Geometry as MeshGeometry3D;


            for (int i = 0; i < modelMesh.Positions.Count; i++)
            {
                Point3D pos = modelMesh.Positions[i];
                pos.X += offsetX;
                pos.Y += offsetY;
                pos.Z += offsetZ;
                modelMesh.Positions[i] = pos;
            }

            geoModel.Geometry = modelMesh;
            //geoModel.Bounds = modelMesh.Bounds;
            modelGroup.Children.Add(geoModel);
            visualModel.Content = modelGroup;
        }

        private GeometryModel3D FindLargestModel(Model3DGroup group)
        {
            if (group.Children.Count == 1)
            {
                return group.Children[0] as GeometryModel3D;
            }

            int maxCount = int.MinValue;

            GeometryModel3D maxModel = null;
            foreach (GeometryModel3D model in group.Children)
            {
                int count = ((MeshGeometry3D)model.Geometry).Positions.Count;
                if (maxCount < count)
                {
                    maxCount = count;
                    maxModel = model;
                }
            }
            return maxModel;
        }

        private void Slice_Click(object sender, RoutedEventArgs e)
        {
            LoadSettingsFromTextBox();

            if (modelMesh != null)
            {
                MeshBuilder meshBuilder = new MeshBuilder(false, false, false);
                Slicer slicer = new Slicer(meshBuilder, modelMesh);

                Rect3D bounds = modelMesh.Bounds;
                double layerCount = bounds.SizeZ / 0.2;

                var result = slicer.Slice(layerCount, layerHeight, numberOfShells, infill, bounds);
                slicedPolygons = result.Item1;
                treeList = result.Item2;
                infillPerLayer = result.Item3;

                ShowSlicedWindows(layerCount);
                draw2DCanvas();
                draw3DCavnas();
            }
            else
            {
                return;
            }
        }

        private void ShowSlicedWindows(double layerCount)
        {
            layerView.Visibility = Visibility.Visible;
            canvasHorizontalSplitter.Visibility = Visibility.Visible;
            layerSlider.Visibility = Visibility.Visible;
            canvasVerticalSplitter.Visibility = Visibility.Visible;
            setup2DCanvas();
            layerSlider.Maximum = layerCount;

            modelView.SetValue(Grid.ColumnSpanProperty, 1);
            layerSlider.Minimum = 1;
        }

        private void LoadSettingsFromTextBox()
        {
            layerHeight = Convert.ToDouble(layerHeightTextBox.Text);
            nozzleDiameter = Convert.ToDouble(nozzleDiameterTextBox.Text);
            numberOfShells = Convert.ToDouble(numberOfShellsTextBox.Text);
            infill = Convert.ToDouble(infillTextBox.Text);
            initTemp = Convert.ToDouble(initialTemperatureTextBox.Text);
            initBedTemp = Convert.ToDouble(initialBedTempTextBox.Text);
            printTemp = Convert.ToDouble(printingTemperatureTextBox.Text);
            bedTemp = Convert.ToDouble(bedTemperatureTextBox.Text);
            printingSpeed = Convert.ToDouble(printingSpeedTextBox.Text);
            adhesionMode = Convert.ToString(adhesionComboBox.SelectedItem.ToString());
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveGcode = new SaveFileDialog();
            saveGcode.Filter = "gcode files (*.gcode) | *.gcode";

            if (saveGcode.ShowDialog() == true)
            {
                string filePath = @saveGcode.FileName;
                GcodeGenerator generator = new GcodeGenerator(slicedPolygons, infillPerLayer, filePath, layerHeight, nozzleDiameter, initTemp, initBedTemp, printTemp, bedTemp, printingSpeed, numberOfShells, centerXOfModel, centerYOfModel);
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void modelView_CameraChanged(object sender, RoutedEventArgs e)
        {
            CameraHelper.Copy(modelView.Camera, layerView.Camera);
        }

        private void setup2DCanvas()
        {
            if (!gridView.Children.Contains(Canvas2DPolygon))
            {
                SolidColorBrush b = new SolidColorBrush();
                b.Color = Colors.DarkGray;
                SolidColorBrush fillB = new SolidColorBrush();
                fillB.Color = Colors.DarkGray;

                Canvas2DPolygon.Stroke = b;
                Canvas2DPolygon.HorizontalAlignment = HorizontalAlignment.Center;
                Canvas2DPolygon.VerticalAlignment = VerticalAlignment.Center;
                Canvas2DPolygon.StrokeThickness = 0.2;
                Canvas2DPolygon.Margin = new Thickness(10);

                Grid.SetRow(Canvas2DPolygon, 2);
                Grid.SetColumn(Canvas2DPolygon, 2);

                gridView.Children.Add(Canvas2DPolygon);
            }


        }

        private System.Windows.Shapes.Polygon setupPolygon(Color color)
        {
            System.Windows.Shapes.Polygon polygon = new System.Windows.Shapes.Polygon();

            SolidColorBrush b = new SolidColorBrush();
            b.Color = color;
            SolidColorBrush fillB = new SolidColorBrush();
            fillB.Color = color;

            polygon.Stroke = b;
            polygon.StrokeThickness = 0.05;
            polygon.Margin = new Thickness(10);

            Grid.SetRow(polygon, 2);
            Grid.SetColumn(polygon, 2);

            polygon.RenderTransform = new ScaleTransform(8, 8, 0, 0);

            return polygon;
        }

        private void draw2DCanvas()
        {
            gridCanvas.Children.Clear();
            if (layerSlider.Value != 0)
            {
                int layer = (int)layerSlider.Value - 1;

                for(int a = 0; a < slicedPolygons[layer].Count() - 1; a++)
                {
                    for (int i = 0; i < slicedPolygons[layer][a].Count(); i++)
                    {
                        System.Windows.Shapes.Polygon polygon;
                        if (a == 0)
                        {
                            polygon = setupPolygon(Colors.Black);
                        }
                        else
                        {
                            polygon = setupPolygon(Colors.Red);
                        }
                        

                        for (int j = 0; j < slicedPolygons[layer][a][i].Count(); j++)
                        {
                            Point temp = new Point(slicedPolygons[layer][a][i][j].X, slicedPolygons[layer][a][i][j].Y);
                            polygon.Points.Add(temp);
                        }
                        gridCanvas.Children.Add(polygon);
                    }

                    for (int i = 0; i < infillPerLayer[layer].Count(); i++)
                    {
                        System.Windows.Shapes.Polygon polygon = setupPolygon(Colors.Blue);

                        for(int j = 0; j < infillPerLayer[layer][i].Count(); j++)
                        {
                            polygon.Points.Add(new Point(infillPerLayer[layer][i][j].X / 10000.0, infillPerLayer[layer][i][j].Y / 10000.0));
                        }
                        gridCanvas.Children.Add(polygon);
                    }
                }
            }
        }

        private void nodeTravel(PolyNode node, ref List<Point> solids, ref List<List<Point>> holes, ref List<Point3D> points)
        {
            List<Point> solid = new List<Point>();
            List<Point> hole = new List<Point>();
            for (int j = 0; j < node.Contour.Count(); j++)
            {
                if (node.IsHole)
                {
                    hole.Add(new Point(node.Contour[j].X, node.Contour[j].Y));
                }
                else
                {
                    solids.Add(new Point(node.Contour[j].X, node.Contour[j].Y));
                }
                points.Add(new Point3D(node.Contour[j].X, node.Contour[j].Y, node.Contour[j].Z));
            }
            if (node.IsHole) holes.Add(hole);

            foreach (PolyNode p in node.Childs)
            {
                nodeTravel(p, ref solids, ref holes, ref points);
            }
        }

        private void draw3DCavnas()
        {
            var meshBuilder = new MeshBuilder(false, false);
            Model3DGroup modelGroup = new Model3DGroup();


            for (int i = 0; i < (int)(layerSlider.Value - 1); i++)
            {
                PolyTree tempTree = treeList[i];
                List<Point> solids = new List<Point>();
                List<List<Point>> holes = new List<List<Point>>();
                List<Point3D> points = new List<Point3D>();

                foreach (PolyNode p in tempTree.Childs)
                {
                    nodeTravel(p, ref solids, ref holes, ref points);
                }

                if (solids.Count() > 2)
                {
                    var triangulated = SweepLinePolygonTriangulator.Triangulate(solids, holes);
                    var p = new HelixToolkit.Wpf.Polygon();
                    List<Point3D> triangles = new List<Point3D>();

                    for (int b = 0; b < triangulated.Count(); b++)
                    {
                        triangles.Add(new Point3D(points[triangulated[b]].X / 10000.0, points[triangulated[b]].Y / 10000.0, (layerHeight * i)));
                    }

                    meshBuilder.AddTriangles(triangles);
                }
            }

            var layerModel = LayerModel;
            GeometryModel3D geomModel = new GeometryModel3D { Geometry = meshBuilder.ToMesh(), Material = MaterialHelper.CreateMaterial(Colors.Yellow), BackMaterial = MaterialHelper.CreateMaterial(Colors.Yellow) };
            Rect3D newBounds = geomModel.Bounds;
            double centerX = newBounds.X + newBounds.SizeX / 2;
            double centerY = newBounds.Y + newBounds.SizeY / 2;
            double centerZ = newBounds.Z;

            geomModel.Transform = new TranslateTransform3D(-centerX, -centerY, -centerZ);

            modelGroup.Children.Add(geomModel);

            layerModel.Content = modelGroup;
        }

        private void LayerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (layerSlider.Value != 0)
            {
                draw2DCanvas();
                draw3DCavnas();
            }
        }
    }
}
