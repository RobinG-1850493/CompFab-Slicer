using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using ClipperLib;
using System.Windows.Media;
using System.Windows;
using System.Net;

namespace CompFab_Slicer
{
    public class Slicer
    {
        MeshBuilder meshBuilder;
        MeshGeometry3D modelMesh;

        static int scale = 10000;
        int rotation;
        public Slicer(MeshBuilder meshBuilder, MeshGeometry3D modelMesh)
        {
            this.meshBuilder = meshBuilder;
            this.modelMesh = modelMesh;
            this.rotation = 0;
        }

        public (List<List<List<Point3DCollection>>>, List<PolyTree>, List<Paths>) Slice(double layerCount, double layerHeight, double shells, double infillDensity, Rect3D boundingBox)
        {
            PolyTree contours = createContoursTree(scale, modelMesh);
            List<PolyTree> treeList = new List<PolyTree>();
            List<Paths> infill = new List<Paths>();
            List<List<List<Point3DCollection>>> slicedModelWithShells = new List<List<List<Point3DCollection>>>();


            for (double z = 1; z <= layerCount; z++)
            {
                Paths intersectingPoints = getIntersectingContours(Clipper.PolyTreeToPaths(contours), z, layerHeight, scale);
                Paths connected = connectPoints(intersectingPoints);

                connected = Clipper.CleanPolygons(connected);
                List<List<Point3DCollection>> shellsPerPolygon = new List<List<Point3DCollection>>();

                for(int i = 0; i < shells+1; i++)
                {
                    if(i == 0)
                    {
                        (Paths eroded, List<Point3DCollection> polygonPoints) = ErodeLayer(connected, z, 0.2);
                        connected = eroded;

                        shellsPerPolygon.Add(polygonPoints);
                        treeList.Add(getPolyTreeStructureAtLayer(eroded, (int)z));
                    } else
                    {
                        (Paths eroded, List<Point3DCollection> polygonPoints) = ErodeLayer(connected, z, 0.4);
                        connected = eroded;

                        shellsPerPolygon.Add(polygonPoints);
                    }
                   
                }
                slicedModelWithShells.Add(shellsPerPolygon);  
            }

            infill = generateInfill(slicedModelWithShells, infillDensity, boundingBox, shells);
            
            var mesh = meshBuilder.ToMesh();
            mesh.Normals = mesh.CalculateNormals();

            return (slicedModelWithShells, treeList, infill);
        }

        private List<Paths> generateInfill(List<List<List<Point3DCollection>>> slicedModel, double infillDensity, Rect3D boundingBox, double shells)
        {

            List<Paths> infillPerLayer = new List<Paths>();
            infillDensity = ((infillDensity / 100));

            for (int layer = 0; layer < slicedModel.Count(); layer++)
            {
                Paths infill = new Paths();
                double infillLineNr = boundingBox.SizeY * infillDensity;
                double lineSpread = boundingBox.SizeY / infillLineNr;

                if (layer < shells)
                {
                    //FLOOR
                    if(rotation == 1)
                    {
                        infill = FloorRoofRotationOne(boundingBox);
                        rotation = 0;
                    }
                    else
                    {
                        infill = FloorRoofRotationTwo(boundingBox);
                        rotation = 1;
                    }
                    
                }
                else
                {
                    if(layer > (slicedModel.Count() - (shells + 1)))
                    {
                        //ROOF
                        if (rotation == 1)
                        {
                            infill = FloorRoofRotationOne(boundingBox);
                            rotation = 0;
                        }
                        else
                        {
                            infill = FloorRoofRotationTwo(boundingBox);
                            rotation = 1;
                        }
                    } 
                    else
                    {
                        //INFILL
                        for (int i = 0; i < infillLineNr; i++)
                        {
                            Path lineSegment = new Path();
                            lineSegment.Add(new IntPoint((boundingBox.X) * scale, (i * lineSpread) * scale));
                            lineSegment.Add(new IntPoint((boundingBox.X + boundingBox.SizeX) * scale, (i * lineSpread) * scale));

                            infill.Add(lineSegment);
                        }
                    }
                }
                int polyCount = slicedModel[layer].Count - 1;
                infillPerLayer.Add(infillIntersection(slicedModel[layer][polyCount], infill));
            }

            return infillPerLayer;
        }

        private Paths FloorRoofRotationOne(Rect3D boundingBox)
        {
            Paths infill = new Paths();

            for (double i = 0; i <= boundingBox.SizeX; i += 0.4)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(0, i * scale));
                lineSegment.Add(new IntPoint(i * scale, 0));

                infill.Add(lineSegment);
            }

            double test = 0;
            for (double i = boundingBox.SizeX; i > 0; i -= 0.4)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(i * scale, boundingBox.SizeX * scale));
                lineSegment.Add(new IntPoint(boundingBox.SizeX * scale, i * scale));
                test += 0.4;
                infill.Add(lineSegment);
            }

            return infill;
        }

        private Paths FloorRoofRotationTwo(Rect3D boundingBox)
        {
            Paths infill = new Paths();


            for (double i = 0; i <= boundingBox.SizeX; i += 0.4)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(0 * scale, (boundingBox.SizeX - i) * scale));
                lineSegment.Add(new IntPoint(i * scale, boundingBox.SizeX * scale));

                infill.Add(lineSegment);
            }

            for (double i = 0; i < boundingBox.SizeX; i += 0.4)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(i * scale, 0 * scale));
                lineSegment.Add(new IntPoint(boundingBox.SizeX * scale, (boundingBox.SizeX - i) * scale ));
                
                infill.Add(lineSegment);
            }

            return infill;
        }

        private Paths infillIntersection(List<Point3DCollection> polygons, Paths infill)
        {
            Paths intersectedInfill = new Paths();
            PolyTree result = new PolyTree();
            Clipper c = new Clipper();

            Paths temp = new Paths();
            for(int i = 0; i < polygons.Count(); i++)
            {
                Path polygon = new Path();
                for(int j = 0; j < polygons[i].Count(); j++)
                {
                    polygon.Add(new IntPoint(polygons[i][j].X * scale, polygons[i][j].Y * scale));
                }
                temp.Add(polygon);
            }

            c.AddPaths(temp, PolyType.ptClip, true);
            c.AddPaths(infill, PolyType.ptSubject, false);

            c.Execute(ClipType.ctIntersection, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            return Clipper.PolyTreeToPaths(result);
        }

        private PolyTree getPolyTreeStructureAtLayer(Paths polygons, int layerNr)
        {
            PolyTree result = new PolyTree();

            Clipper clipper = new Clipper();

            clipper.AddPaths(polygons, PolyType.ptSubject, true);
            clipper.Execute(ClipType.ctXor, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            return result;
        }

        private Paths erodePerimeter(Paths polygons, double diameter)
        {
            Paths erodedPaths = new Paths();

            for(int i = 0; i < polygons.Count(); i++)
            {
                Paths temp = new Paths();
                ClipperOffset co = new ClipperOffset();

                co.AddPath(polygons[i], JoinType.jtRound, EndType.etClosedPolygon);
                co.Execute(ref temp, ((-diameter) * scale));

                if(temp.Count != 0)
                {
                    erodedPaths.Add(temp[0]);
                }
                
                temp.Clear();
            }


            return erodedPaths;
        }

        public (Paths erodedPath, List<Point3DCollection> erodedLayer)  ErodeLayer(Paths connectedPoints, double layer, double diameter)
        {
            Paths eroded = erodePerimeter(connectedPoints, diameter);
            List<Point3DCollection> polygonPoints = new List<Point3DCollection>();

            for (int i = 0; i < eroded.Count; i++)
            {
                Point3DCollection pts = new Point3DCollection();
                for (int j = 0; j < eroded[i].Count; j++)
                {
                    Point3D temp = new Point3D((double)(eroded[i][j].X) / scale, (double)(eroded[i][j].Y) / scale, layer * 0.2);
                    pts.Add(temp);
                }
                polygonPoints.Add(pts);
            }

            return (eroded, polygonPoints);
        }

        private Paths connectPoints(Paths slice)
        {
            Paths connectedPolygons = new Paths();

            for (int i = 0; i < slice.Count; i++){
                Path pts = new Path();
                

                if(slice[i].Count > 0)
                {
                    IntPoint startPt, endPt;
                    pts = slice[i];

                    while (pts.Count > 2)
                    {
                        Path result = new Path();
                        startPt = pts[0];
                        endPt = pts[1];

                        result.Add(startPt);
                        result.Add(endPt);

                        pts.Remove(startPt);
                        pts.Remove(endPt);

                        bool found = true;
                        while (found && pts.Count != 0)
                        {
                            for (int j = 0; j < pts.Count; j++)
                            {
                                if (endPt == pts[j])
                                {
                                    if (j % 2 == 0)
                                    {
                                        startPt = pts[j];
                                        endPt = pts[j + 1];

                                        result.Add(startPt);
                                        result.Add(endPt);

                                        pts.RemoveAt(j);
                                        pts.RemoveAt(j);
                                    }
                                    else
                                    {
                                        startPt = pts[j];
                                        endPt = pts[j - 1];

                                        result.Add(startPt);
                                        result.Add(endPt);

                                        pts.RemoveAt(j - 1);
                                        pts.RemoveAt(j - 1);
                                    }

                                    found = true;
                                    break;
                                }
                                else
                                {
                                    found = false;
                                }
                            }
                        }
                        if(pts.Count == 2)
                        {
                            result.Add(pts[0]);
                            result.Add(pts[1]);

                            pts.Clear();
                        }
                        
                        connectedPolygons.Add(result);
                    }
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

        private void checkIntersection(IntPoint pt1, IntPoint pt2, long zPlane, ref Path intersectingContours)
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
                    long X, Y, Z;

                    X = pt1.X + (((zPlane - pt1.Z) * (pt2.X - pt1.X)) / (pt2.Z - pt1.Z));
                    Y = pt1.Y + (((zPlane - pt1.Z) * (pt2.Y - pt1.Y)) / (pt2.Z - pt1.Z));
                    Z = zPlane;

                    intersectingContours.Add(new IntPoint(X, Y, Z));
                }
            }
        }


        private Paths getIntersectingContours(Paths contours, double layerNr, double layer_height, int scale)
        {
            Paths intersectingContours = new Paths();
            Path intersectingPoints = new Path();
            long zPlane = (long)((layerNr * layer_height * scale) - ((layer_height/2 * scale)));

            for (int i = 0; i < contours.Count; i++)
            {
                checkIntersection(contours[i][0], contours[i][1], zPlane, ref intersectingPoints);
                checkIntersection(contours[i][0], contours[i][2], zPlane, ref intersectingPoints);
                checkIntersection(contours[i][1], contours[i][2], zPlane, ref intersectingPoints);
            }

            intersectingContours.Add(intersectingPoints);
            return intersectingContours;
        }

        /*private Paths intersect(Paths contours, Paths clip, double layer_height, int scale)
        {
            Paths solution = new Paths();

            Clipper c = new Clipper();
            c.AddPaths(contours, PolyType.ptSubject, false);
            contours.Clear();
            c.AddPaths(clip, PolyType.ptClip, true);

            PolyTree pSol = new PolyTree();

            c.Execute(ClipType.ctIntersection, pSol,
              PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            solution = Clipper.PolyTreeToPaths(pSol);

            return solution;
        }*/
    }
}
