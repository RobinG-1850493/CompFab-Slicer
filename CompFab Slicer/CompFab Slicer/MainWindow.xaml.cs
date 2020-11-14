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

        Material yellowMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);
        Material insideMaterial = MaterialHelper.CreateMaterial(Colors.Gray);

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
            modelMesh = geoModel.Geometry as MeshGeometry3D;

            geoModel.Material = yellowMaterial;
            geoModel.BackMaterial = insideMaterial;

            Rect3D bounds = geoModel.Bounds;
            double centerX = bounds.X + bounds.SizeX / 2;
            double centerY = bounds.Y + bounds.SizeY / 2;
            double centerZ = bounds.Z;

            geoModel.Transform = new TranslateTransform3D(-centerX, -centerY, -centerZ);
            //geoModel.Transform = new ScaleTransform3D(1, 1, 1, centerX, centerY, centerZ);

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
            if(modelMesh != null)
            {
                //layerView.Children.Clear();
                MeshBuilder meshBuilder = new MeshBuilder(false, false, false);
                Model3DGroup modelGroup = new Model3DGroup();

                Slicer slicer = new Slicer(meshBuilder, modelMesh);
                
                List<Point3DCollection> polygons = slicer.Slice();

                SolidColorBrush b = new SolidColorBrush();
                b.Color = Colors.DarkGray;
                SolidColorBrush fillB = new SolidColorBrush();
                //fillB.Color = Colors.LightBlue;

                //layerView.Camera.LookDirection = new Vector3D(0,0,-1);
                layerView.Visibility = Visibility.Visible;
                canvasSplitter.Visibility = Visibility.Visible;
                modelView.SetValue(Grid.ColumnSpanProperty, 1);


                var meshBuilder2 = new MeshBuilder(false, false, false);


                for (int i = 0; i < polygons.Count(); i++)
                {
                    var p = new HelixToolkit.Wpf.Polygon3D();

                    for (int j = 0; j < polygons[i].Count(); j++)
                    {
                        p.Points.Add(polygons[i][j]);
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
                    meshBuilder2.add*/



                    //modelGroup.;
                    //layerView.Children.Add(p);
                    //Canvas.SetTop(p, 100);
                    //Canvas.SetLeft(p, 400);
                }

                var layerModel = LayerModel;
                GeometryModel3D geomModel = new GeometryModel3D { Geometry = meshBuilder2.ToMesh(), Material = yellowMaterial, BackMaterial = insideMaterial };
                Rect3D bounds = geomModel.Bounds;
                double centerX = bounds.X + bounds.SizeX / 2;
                double centerY = bounds.Y + bounds.SizeY / 2;
                double centerZ = bounds.Z;

                geomModel.Transform = new TranslateTransform3D(-centerX, -centerY, -centerZ);

                modelGroup.Children.Add(geomModel);
                layerModel.Content = modelGroup;
            }
            else
            {
                return;
            } 
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
        
        }

        private void modelView_CameraChanged(object sender, RoutedEventArgs e)
        {
            CameraHelper.Copy(modelView.Camera, layerView.Camera);
        }
    }
}
