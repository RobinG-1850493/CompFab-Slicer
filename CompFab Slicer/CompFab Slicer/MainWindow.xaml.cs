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
            var modelGroup = new Model3DGroup();
            var visualModel = VisualModel;

            // Create a mesh builder and add a box to it
            var meshBuilder = new MeshBuilder(false, false);
            meshBuilder.AddBox(new Point3D(0, 0, 1), 1, 2, 0.5);
            meshBuilder.AddBox(new Rect3D(0, 0, 1.2, 0.5, 1, 0.4));

            // Create a mesh from the builder (and freeze it)
            var mesh = meshBuilder.ToMesh(true);

            // Create some materials
            var yellowMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);
            var insideMaterial = MaterialHelper.CreateMaterial(Colors.Gray);

            StLReader reader = new HelixToolkit.Wpf.StLReader();
            Model3DGroup group = reader.Read(file);
            GeometryModel3D geoModel = FindLargestModel(group);
            MeshGeometry3D stlMesh = geoModel.Geometry as MeshGeometry3D;

            Int32Collection indices = mesh.TriangleIndices;

            geoModel.Material = yellowMaterial;
            geoModel.BackMaterial = insideMaterial;
            geoModel.Transform = new TranslateTransform3D(0, 0, 0);
            modelGroup.Children.Add(geoModel);

            // Set the property, which will be bound to the Content property of the ModelVisual3D (see MainWindow.xaml)
            visualModel.Content = geoModel;
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
    

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
        
        }
    }
}
