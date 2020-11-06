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
using ClipperLib;

using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using System.Runtime.InteropServices;
using Polygon3D = HelixToolkit.Wpf.Polygon3D;

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
            //meshBuilder.AddBox(new Point3D(0, 0, 0), 50, 50, 0.2);
            // Create a mesh from the builder (and freeze it

            // Create some materials
            var yellowMaterial = MaterialHelper.CreateMaterial(Colors.Yellow);
            var insideMaterial = MaterialHelper.CreateMaterial(Colors.Gray);

            StLReader reader = new StLReader();
            Model3DGroup group = reader.Read(file);
            GeometryModel3D geoModel = FindLargestModel(group);
            MeshGeometry3D stlMesh = geoModel.Geometry as MeshGeometry3D;

            Int32Collection indices = stlMesh.TriangleIndices;

            Paths contours = new Paths();
            Paths clip = new Paths();
            int scale = 10000;

            for(int i = 0; i < stlMesh.TriangleIndices.Count; i += 3)
            {
                Path contour = new Path(3);

                Point3D vec_1 = stlMesh.Positions[indices[i]];
                Point3D vec_2 = stlMesh.Positions[indices[i + 1]];
                Point3D vec_3 = stlMesh.Positions[indices[i + 2]];

                contour.Add(new IntPoint((long)(vec_1.X *scale), (long)(vec_1.Y * scale), (long)(vec_1.Z * scale)));
                contour.Add(new IntPoint((long)(vec_2.X *scale), (long)(vec_2.Y * scale), (long)(vec_2.Z * scale)));
                contour.Add(new IntPoint((long)(vec_3.X *scale), (long)(vec_3.Y * scale), (long)(vec_3.Z * scale)));

                contours.Add(contour);   
            }

            Path zPlane = new Path(4);
            zPlane.Add(new IntPoint(-100 * scale, -100 * scale, 22 * scale));
            zPlane.Add(new IntPoint(-100 * scale, 100 * scale, 22 * scale));
            zPlane.Add(new IntPoint(100 * scale, 100 * scale, 22 * scale));
            zPlane.Add(new IntPoint(100 * scale, -100 * scale, 22 * scale));
            clip.Add(zPlane);

            geoModel.Material = yellowMaterial;
            geoModel.BackMaterial = insideMaterial;
            geoModel.Transform = new TranslateTransform3D(0, 0, 0);
            modelGroup.Children.Add(geoModel);

            Paths slice = intersect(contours, clip, 0.2, scale);
            

            for(int i = 0; i < slice.Count; i++)
            {
                List<Point3D> polygon = new List<Point3D>();
                for (int j = 0; j < slice[i].Count; j++)
                {
                    if(0*scale <= slice[i][j].Z && slice[i][j].Z <= (long)(0.2*scale))
                    {
                        Point3D temp = new Point3D((double)(slice[i][j].X) / scale, (double)(slice[i][j].Y) / scale, 0);
                        polygon.Add(temp);
                    }
                }
                meshBuilder.AddPolygon(polygon);
            }
            var mesh = meshBuilder.ToMesh(true);


            GeometryModel3D testModel = new GeometryModel3D { Geometry = mesh, Material = yellowMaterial, BackMaterial = insideMaterial, Transform = new TranslateTransform3D(0, 50, 0) };
            modelGroup.Children.Add(testModel);

            visualModel.Content = modelGroup;
        }

        public Paths intersect(Paths contours, Paths clip, double layer_height, int scale)
        {
            Paths solution = new Paths();
            
            Clipper c = new Clipper();
            c.AddPaths(contours, PolyType.ptSubject, false);
            c.AddPaths(clip, PolyType.ptClip, true);

            //WERKT NOG NIET

            //Clipper.ZFillCallback customCallback = clipperCallback;
            //c.ZFillFunction(customCallback);
            
            PolyTree pSol = new PolyTree();

            bool succes = c.Execute(ClipType.ctIntersection, pSol,
              PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            solution = Clipper.PolyTreeToPaths(pSol);
            // solution = Clipper.CleanPolygons(solution);

            return solution;
        }

        public static void clipperCallback(IntPoint bot1, IntPoint top1, IntPoint bot2, IntPoint top2, ref IntPoint pt)
        {
            pt.Z = 20000;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
