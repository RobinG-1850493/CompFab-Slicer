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
            geoModel.Transform = new TranslateTransform3D(0, 0, 0);
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
                canvas.Children.Clear();
                MeshBuilder meshBuilder = new MeshBuilder(false, false, false);
                Model3DGroup modelGroup = new Model3DGroup();

                Slicer slicer = new Slicer(meshBuilder, modelMesh);
                
                List<PointCollection> polygons = slicer.Slice();

                SolidColorBrush b = new SolidColorBrush();
                b.Color = Colors.DarkGray;
                SolidColorBrush fillB = new SolidColorBrush();
                fillB.Color = Colors.LightBlue;

                for(int i = 0; i < polygons.Count(); i++)
                {
                    System.Windows.Shapes.Polygon p = new System.Windows.Shapes.Polygon();
                    p.Stroke = b;
                    p.Fill = fillB;
                    p.StrokeThickness = 1;
                    p.Stretch = Stretch.Uniform;
                    p.Margin = new Thickness(10);
                    
                    for(int j = 0; j < polygons[i].Count; j++)
                    {
                        p.Points.Add(polygons[i][j]);
                    }
                    canvas.Children.Add(p);
                }

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
    }
}
