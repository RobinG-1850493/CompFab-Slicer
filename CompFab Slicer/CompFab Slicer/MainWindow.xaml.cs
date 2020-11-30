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
using HelixToolkit.Wpf;
using Microsoft.Win32;

namespace CompFab_Slicer
{
    public partial class MainWindow : Window
    {
        public MeshGeometry3D modelMesh;
        public List<List<Point3DCollection>> slicedPolygons;
        private double layerHeight;
        private double nozzleDiameter;
        private double numberOfShells;
        private double infill;
        private double initTemp;
        private double initBedTemp;
        private double printTemp;
        private double bedTemp;
        private double printingSpeed;
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

            if(bounds.X <= 0)
            {
                offsetX = Math.Abs(bounds.X);
            } else
            {
                offsetX = -Math.Abs(bounds.X);
            }

            if(bounds.Y <= 0)
            {
                offsetY = Math.Abs(bounds.Y);
            } else
            {
                offsetY = -Math.Abs(bounds.Y);
            }

            if(bounds.Z <= 0)
            {
                offsetZ = Math.Abs(bounds.Z);
            } else
            {
                offsetZ = -Math.Abs(bounds.Z);
            }
            modelMesh = geoModel.Geometry as MeshGeometry3D;
            
            
            for(int i = 0; i < modelMesh.Positions.Count; i++)
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
                double layerCount = bounds.SizeZ/0.2;

                slicedPolygons = slicer.Slice(layerCount);
                ShowSlicedWindows(layerCount);
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
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveGcode = new SaveFileDialog();
            saveGcode.Filter = "gcode files (*.gcode) | *.gcode";

            if (saveGcode.ShowDialog() == true)
            {
                string filePath = @saveGcode.FileName;
                GcodeGenerator generator = new GcodeGenerator(slicedPolygons ,filePath, layerHeight, nozzleDiameter, initTemp, initBedTemp, printTemp, bedTemp, printingSpeed);
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

        private void LayerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            
            if (layerSlider.Value != 0)
            {
                var meshBuilder = new MeshBuilder(false, false, false);
                Model3DGroup modelGroup = new Model3DGroup();

                for (int z = 0; z < layerSlider.Value; z++)
                {
                    Canvas2DPolygon.Points.Clear();
                    for (int i = 0; i < slicedPolygons[z].Count(); i++)
                    {
                        var polygon = new HelixToolkit.Wpf.Polygon3D();

                        for (int j = 0; j < slicedPolygons[z][i].Count(); j++)
                        {
                            polygon.Points.Add(slicedPolygons[z][i][j]);
                            Point test = new Point(slicedPolygons[z][i][j].X, slicedPolygons[z][i][j].Y);
                            Canvas2DPolygon.Points.Add(test);
                        }

                        Canvas2DPolygon.RenderTransform = new ScaleTransform(2, 2, 0, 0);

                        var flattened = polygon.Flatten();
                        meshBuilder.Append(polygon.Points, flattened.Triangulate());  
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
            
        }
    }
}
