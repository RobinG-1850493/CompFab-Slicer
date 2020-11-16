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
        
        Material yellowMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);
        Material insideMaterial = MaterialHelper.CreateMaterial(Colors.Gray);
        System.Windows.Shapes.Polygon p2 = new System.Windows.Shapes.Polygon();


        double layerHeight;
        double nozzleDiameter;
        double numberOfShells;
        double infill;
        double initTemp;
        double initBedTemp;
        double printTemp;
        double bedTemp;
        double printingSpeed;

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

            geoModel.Material = yellowMaterial;
            geoModel.BackMaterial = insideMaterial;

            Rect3D bounds = geoModel.Bounds;
            double centerX = bounds.X + bounds.SizeX / 2;
            double centerY = bounds.Y + bounds.SizeY / 2;
            double centerZ = bounds.Z;

            geoModel.Transform = new TranslateTransform3D(-centerX, -centerY, -centerZ);
            //geoModel.Transform = new ScaleTransform3D(1, 1, 1, centerX, centerY, centerZ);
            modelMesh = geoModel.Geometry as MeshGeometry3D;

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
                //layerView.Children.Clear();
                MeshBuilder meshBuilder = new MeshBuilder(false, false, false);
                Model3DGroup modelGroup = new Model3DGroup();

                Slicer slicer = new Slicer(meshBuilder, modelMesh);

                Rect3D bounds = modelMesh.Bounds;
                double layerCount = bounds.SizeZ/0.2;

                slicedPolygons = slicer.Slice(layerCount);

                /*SolidColorBrush b = new SolidColorBrush();
                b.Color = Colors.DarkGray;
                SolidColorBrush fillB = new SolidColorBrush();*/
                //fillB.Color = Colors.LightBlue;

                //layerView.Camera.LookDirection = new Vector3D(0,0,-1);
                layerView.Visibility = Visibility.Visible;
                canvasHorizontalSplitter.Visibility = Visibility.Visible;
                layerSlider.Visibility = Visibility.Visible;
                canvasVerticalSplitter.Visibility = Visibility.Visible;
                //viewBox2D.Visibility = Visibility.Visible;
                setup2DCanvas();
                layerSlider.Maximum = layerCount;
                
                modelView.SetValue(Grid.ColumnSpanProperty, 1);
                layerSlider.Minimum = 1;

                /*var meshBuilder2 = new MeshBuilder(false, false, false);


                for (int i = 0; i < slicedPolygons[1].Count(); i++)
                {
                    var p = new HelixToolkit.Wpf.Polygon3D();


                    for (int j = 0; j < slicedPolygons[1][i].Count(); j++)
                    {
                        p.Points.Add(slicedPolygons[1][i][j]);
                    }

                    //meshBuilder2.AddPolygon(p.Points);
                    var flattened = p.Flatten();

                    meshBuilder2.Append(p.Points, flattened.Triangulate());

                    /*System.Windows.Shapes.Polygon p = new System.Windows.Shapes.Polygon();
                    p.Stroke = b;
                    p.Fill = fillB;
                    p.StrokeThickness = 0.05;
                    p.HorizontalAlignment = HorizontalAlignment.Center;
                    p.VerticalAlignment = VerticalAlignment.Center;
                    p.RenderTransform = new ScaleTransform(-15, -15, 0, 10);
                    p.Margin = new Thickness(10);
                    
                    for(int j = 0; j < polygons[i].Count; j++)
                    {
                        p.Points.Add(polygons[i][j]);
                    }

                    HelixToolkit.Wpf.Polygon p = new HelixToolkit.Wpf.Polygon();

                    for (int j = 0; j < polygons[i].Count; j++)
                    {
                        p.Points.Add(polygons[i][j]);
                    }
                    

                    var meshBuilder2 = new MeshBuilder(false, false);
                    meshBuilder2.add



                    //modelGroup.;
                    //layerView.Children.Add(p);
                    //Canvas.SetTop(p, 100);
                    //Canvas.SetLeft(p, 400);
                }

                var layerModel = LayerModel;
                GeometryModel3D geomModel = new GeometryModel3D { Geometry = meshBuilder2.ToMesh(), Material = yellowMaterial, BackMaterial = insideMaterial };
                Rect3D newBounds = geomModel.Bounds;
                double centerX = newBounds.X + newBounds.SizeX / 2;
                double centerY = newBounds.Y + newBounds.SizeY / 2;
                double centerZ = newBounds.Z;

                geomModel.Transform = new TranslateTransform3D(-centerX, -centerY, -centerZ);

                modelGroup.Children.Add(geomModel);
                layerModel.Content = modelGroup;*/
            }
            else
            {
                return;
            } 
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
            if (!gridView.Children.Contains(p2))
            {
                SolidColorBrush b = new SolidColorBrush();
                b.Color = Colors.DarkGray;
                SolidColorBrush fillB = new SolidColorBrush();
                fillB.Color = Colors.DarkGray;

                p2.Stroke = b;
                //p2.Fill = fillB;
                p2.HorizontalAlignment = HorizontalAlignment.Center;
                p2.VerticalAlignment = VerticalAlignment.Center;
                p2.StrokeThickness = 0.2;
                p2.Margin = new Thickness(10);

                Grid.SetRow(p2, 2);
                Grid.SetColumn(p2, 2);

                gridView.Children.Add(p2);
            }
            

        }

        /*public static Point GetCentroid(PointCollection nodes, int count)
        {
            int x = 0, y = 0, area = 0, k;
            Point a, b = nodes[count - 1];

            for (int i = 0; i < count; i++)
            {
                a = nodes[i];

                k = (int)(a.Y * b.X - a.X * b.Y);
                area += k;
                x += (int)(a.X + b.X) * k;
                y += (int)(a.Y + b.Y) * k;

                b = a;
            }
            area *= 3;

            return new Point(x /= area, y /= area);
        }*/

        private void layerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            


            if (layerSlider.Value != 0)
            {
                var meshBuilder2 = new MeshBuilder(false, false, false);
                Model3DGroup modelGroup = new Model3DGroup();
                



                for (int z = 0; z < layerSlider.Value; z++)
                {
                    p2.Points.Clear();
                    for (int i = 0; i < slicedPolygons[z].Count(); i++)
                    {

                        var p = new HelixToolkit.Wpf.Polygon3D();

                        for (int j = 0; j < slicedPolygons[z][i].Count(); j++)
                        {
                            p.Points.Add(slicedPolygons[z][i][j]);
                            Point test = new Point(slicedPolygons[z][i][j].X, slicedPolygons[z][i][j].Y);
                            p2.Points.Add(test);
                        }

                        double widthOfCanvas = gridView.ColumnDefinitions[2].ActualWidth;
                        double heightOfCanvas = gridView.RowDefinitions[1].ActualHeight;


                        //Point centroid = GetCentroid(p2.Points, p2.Points.Count);
                        //p2.RenderTransform = new ScaleTransform(2, 2, centroid.X, centroid.Y);
                        p2.RenderTransform = new ScaleTransform(2, 2, 0, 0);

                        //if(p.Points.Count() > 2)
                        //{
                        var flattened = p.Flatten();
                            meshBuilder2.Append(p.Points, flattened.Triangulate());
                        //}
                        

                        //meshBuilder2.AddPolygon(p.Points);
                    }


                }


                var layerModel = LayerModel;
                GeometryModel3D geomModel = new GeometryModel3D { Geometry = meshBuilder2.ToMesh(), Material = yellowMaterial, BackMaterial = yellowMaterial };
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
