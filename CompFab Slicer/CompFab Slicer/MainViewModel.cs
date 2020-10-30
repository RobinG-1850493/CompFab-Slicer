using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CompFab_Slicer
{
    public class MainViewModel
    {
        public MainViewModel(string file)
        {
            var modelGroup = new Model3DGroup();

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
            this.Model = geoModel;
        }

        public Model3D Model { get; set; }

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
    }
}

