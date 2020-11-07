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
using System.Windows.Shapes;
using System.Windows.Markup.Localizer;

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
            Paths intersectingPoints = getIntersectingContours(Clipper.PolyTreeToPaths(contours), 1, 0.2, scale);

            //intersectingPoints = Clipper.CleanPolygons(intersectingPoints);
            Paths slice = intersect(intersectingPoints, zPlane, 0.2, scale);
            Paths connected = connectPoints(slice);

            Polygon3D poly;
            for (int i = 0; i < connected.Count; i++)
            {
                List<Point3D> polygon = new List<Point3D>();
                for (int j = 0; j < connected[i].Count; j++)
                {
                    Point3D temp = new Point3D((double)(connected[i][j].X) / scale, (double)(connected[i][j].Y) / scale, 0);
                    polygon.Add(temp);
                }
                poly = new Polygon3D(polygon);
                //FillPolygon(poly);
                meshBuilder.AddPolygon(polygon);
            }
            
            var mesh = meshBuilder.ToMesh();

            return mesh;
        }

        public void FillPolygon(Polygon3D p3)
        {
            HelixToolkit.Wpf.Polygon polygon = p3.Flatten();
            var triangleIndexes = CuttingEarsTriangulator.Triangulate(polygon.Points);

            meshBuilder.Append(p3.Points, triangleIndexes);
        }

        private Paths connectPoints(Paths slice)
        {
            Paths connectedPolygons = new Paths();
            double smallestDist = double.MaxValue, dist;
            int smallestIndex = -1;

            for(int i = 0; i < slice.Count; i++)
            {
                Path points = new Path();
                if (slice[i].Count > 0)
                {
                    points = slice[i];
                    points = points.Distinct().ToList();
                    Path polygon = new Path();
                    IntPoint startPoint = points[0];
                    polygon.Add(startPoint);
                    while (points.Count > 1)
                    {
                        smallestDist = double.MaxValue;
                        for (int k = 0; k < points.Count; k++)
                        {
                            if (startPoint != points[k])
                            {
                                dist = calculateEuclideanDistance(startPoint, points[k]);

                                if (dist < smallestDist)
                                {
                                    smallestDist = dist;
                                    smallestIndex = k;
                                }
                            }
                        }
                        polygon.Add(points[smallestIndex]);
                        IntPoint temp = startPoint;
                        startPoint = points[smallestIndex];
                        points.Remove(temp);
                    }
                    connectedPolygons.Add(polygon);
                }
            }


            return connectedPolygons;
        }

        private double calculateEuclideanDistance(IntPoint pt1, IntPoint pt2)
        {
            double result;

            long dX = pt2.X - pt1.X;
            long dY = pt2.Y - pt1.Y;
            result = Math.Sqrt(dX * dX + dY * dY);

            return result;
        }

        private PolyTree createContoursTree(int scale, MeshGeometry3D mesh)
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

        private Paths createClippingPlane(int scale)
        {
            Paths zPlane = new Paths();
            Path cPlane = new Path();

            cPlane.Add(new IntPoint(-1000 * scale, -1000 * scale));
            cPlane.Add(new IntPoint(-1000 * scale, 1000 * scale));
            cPlane.Add(new IntPoint(1000 * scale, 1000 * scale));
            cPlane.Add(new IntPoint(1000 * scale, -1000 * scale));

            zPlane.Add(cPlane);

            return zPlane;
        }

        private void checkIntersection(IntPoint pt1, IntPoint pt2, long zPlane, ref Path intersectingContours)
        {
            long zMax, zMin;
            //zPlane = (long)(0.2 * scale);

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
                    long X, Y, Z;

                    if (pt1.X == pt2.X && pt1.Y == pt2.Y)
                    {
                        X = pt1.X;
                        Y = pt1.Y;
                    }
                    else
                    {
                        X = pt1.X + (((zPlane - pt1.Z) * (pt2.X - pt1.X)) / (pt2.Z - pt1.Z));
                        Y = pt1.Y + (((zPlane - pt1.Z) * (pt2.Y - pt1.Y)) / (pt2.Z - pt1.Z));
                    }
                    
                    Z = zPlane;

                    if (Y >= 8.5 * scale)
                    {
                        System.Diagnostics.Debug.WriteLine(Y);
                    }

                    intersectingContours.Add(new IntPoint(X, Y, Z));
                }
            }
        }


        private Paths getIntersectingContours(Paths contours, int layerNr, double layer_height, int scale)
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

        private Paths intersect(Paths contours, Paths clip, double layer_height, int scale)
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

            solution = Clipper.CleanPolygons(solution);
            //solution = Clipper.SimplifyPolygons(solution);

            return solution;
        }
    }
}
