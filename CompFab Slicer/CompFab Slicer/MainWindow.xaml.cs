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
                var sModel = sliceModel;
                MeshBuilder meshBuilder = new MeshBuilder(false, false, false);
                Model3DGroup modelGroup = new Model3DGroup();

                Slicer slicer = new Slicer(meshBuilder, modelMesh);
                
                MeshGeometry3D mesh = slicer.Slice();

                GeometryModel3D slice = new GeometryModel3D { Geometry = mesh, Material = yellowMaterial, BackMaterial = insideMaterial, Transform = new TranslateTransform3D(0, 0, 0) };
                modelGroup.Children.Add(slice);

                sModel.Content = modelGroup;
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
