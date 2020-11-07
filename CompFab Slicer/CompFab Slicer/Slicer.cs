using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using ClipperLib;

namespace CompFab_Slicer
{
    public class Slicer
    {
        MeshBuilder meshBuilder;
        MeshGeometry3D modelMesh;

        int scale = 10000;
        public Slicer(MeshBuilder meshBuilder, MeshGeometry3D modelMesh)
        {
            this.meshBuilder = meshBuilder;
            this.modelMesh = modelMesh;
        }

        public MeshGeometry3D Slice()
        {
            Paths zPlane = createClippingPlane(scale);
            PolyTree contours = createContoursTree(scale, modelMesh);
            Paths intersectingPoints = getIntersectingContours(Clipper.PolyTreeToPaths(contours), 180, 0.2, scale);

            Paths slice = intersect(intersectingPoints, zPlane, 0.2, scale);

            for (int i = 0; i < slice.Count; i++)
            {
                List<Point3D> polygon = new List<Point3D>();
                for (int j = 0; j < slice[i].Count; j++)
                {
                    Point3D temp = new Point3D((double)(slice[i][j].X) / scale, (double)(slice[i][j].Y) / scale, 0);
                    polygon.Add(temp);
                }
                meshBuilder.AddPolygon(polygon);
            }
            var mesh = meshBuilder.ToMesh(true);

            return mesh;
        }

        public PolyTree createContoursTree(int scale, MeshGeometry3D mesh)
        {
            PolyTree contours = new PolyTree();
            System.Windows.Media.Int32Collection indices = mesh.TriangleIndices;

            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
            {
                PolyNode contour = new PolyNode();

                Point3D vec_1 = mesh.Positions[indices[i]];
                Point3D vec_2 = mesh.Positions[indices[i + 1]];
                Point3D vec_3 = mesh.Positions[indices[i + 2]];

                contour.Contour.Add(new IntPoint((long)(vec_1.X * scale), (long)(vec_1.Y * scale), (long)(vec_1.Z * scale)));
                contour.Contour.Add(new IntPoint((long)(vec_2.X * scale), (long)(vec_2.Y * scale), (long)(vec_2.Z * scale)));
                contour.Contour.Add(new IntPoint((long)(vec_3.X * scale), (long)(vec_3.Y * scale), (long)(vec_3.Z * scale)));

                contours.AddChild(contour);
            }

            return contours;
        }

        public Paths createClippingPlane(int scale)
        {
            Paths zPlane = new Paths();
            Path cPlane = new Path();
            cPlane.Add(new IntPoint(-100 * scale, -100 * scale, (long)(0.2 * scale)));
            cPlane.Add(new IntPoint(-100 * scale, 100 * scale, (long)(0.2 * scale)));
            cPlane.Add(new IntPoint(100 * scale, 100 * scale, (long)(0.2 * scale)));
            cPlane.Add(new IntPoint(100 * scale, -100 * scale, (long)(0.2 * scale)));

            zPlane.Add(cPlane);

            return zPlane;
        }

        public void checkIntersection(IntPoint pt1, IntPoint pt2, long zPlane, ref Path intersectingContours)
        {
            long zMax, zMin;

            if (pt1.Z != pt2.Z)
            {
                if (pt1.Z < pt2.Z)
                {
                    zMin = pt1.Z;
                    zMax = pt2.Z;
                }
                else
                {
                    zMin = pt2.Z;
                    zMax = pt1.Z;

                    IntPoint temp = pt1;
                    pt1 = pt2;
                    pt2 = temp;
                }

                if (zMin < zPlane && zMax > zPlane)
                {
                    Path pt = new Path();

                    long X = pt1.X + (((zPlane - pt1.Z) * (pt2.X - pt1.X)) / (pt2.Z - pt1.Z));
                    long Y = pt1.X + (((zPlane - pt1.Z) * (pt2.Y - pt1.Y)) / (pt2.Z - pt1.Z));
                    long Z = zPlane;

                    intersectingContours.Add(new IntPoint(X, Y, Z));
                }
            }
        }


        public Paths getIntersectingContours(Paths contours, int layerNr, double layer_height, int scale)
        {
            Paths intersectingContours = new Paths();
            Path intersectingPoints = new Path();
            long zPlane = (long)(layerNr * layer_height * scale);

            for (int i = 0; i < contours.Count; i++)
            {
                checkIntersection(contours[i][0], contours[i][1], zPlane, ref intersectingPoints);
                checkIntersection(contours[i][0], contours[i][2], zPlane, ref intersectingPoints);
                checkIntersection(contours[i][1], contours[i][2], zPlane, ref intersectingPoints);
            }
            intersectingContours.Add(intersectingPoints);
            return intersectingContours;
        }

        public Paths intersect(Paths contours, Paths clip, double layer_height, int scale)
        {
            Paths solution = new Paths();

            Clipper c = new Clipper();
            c.AddPaths(contours, PolyType.ptSubject, false);
            contours.Clear();
            c.AddPaths(clip, PolyType.ptClip, true);

            PolyTree pSol = new PolyTree();

            bool succes = c.Execute(ClipType.ctIntersection, pSol,
              PolyFillType.pftNonZero, PolyFillType.pftNonZero);

            solution = Clipper.PolyTreeToPaths(pSol);
            //solution = Clipper.CleanPolygons(solution);

            return solution;
        }
    }
}
