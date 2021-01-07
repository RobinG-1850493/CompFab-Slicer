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

        public (List<List<List<Point3DCollection>>>, List<PolyTree>, List<Paths>) Slice(double layerCount, double layerHeight, double shells, double infillDensity, Rect3D boundingBox, bool supportNeeded)
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

                for (int i = 0; i < shells + 1; i++)
                {
                    Paths temp = new Paths();
                    if (i == 0)
                    {
                        (Paths eroded, List<Point3DCollection> polygonPoints) = ErodeLayer(connected, z, 0.2, layerHeight);
                        connected = eroded;

                        shellsPerPolygon.Add(polygonPoints);
                        treeList.Add(getPolyTreeStructureAtLayer(eroded, (int)z));
                    } else
                    {
                        (Paths eroded, List<Point3DCollection> polygonPoints) = ErodeLayer(connected, z, 0.4, layerHeight);
                        connected = eroded;

                        shellsPerPolygon.Add(polygonPoints);
                    }

                }
                slicedModelWithShells.Add(shellsPerPolygon);
            }
            
            if(supportNeeded)
            {
                var tempSupp = generateSupports(slicedModelWithShells, infillDensity, boundingBox, shells, layerHeight);

                List<Paths> suppRegions = tempSupp.Item1;
                List<int> supportLayers = tempSupp.Item2;

                for (int i = 0; i < suppRegions.Count(); i++)
                {
                    for (int j = 0; j < suppRegions[i].Count(); j++)
                    {
                        for (int l = 0; l < supportLayers[i]; l++)
                        {
                            Point3DCollection pts = new Point3DCollection();
                            for (int x = 0; x < suppRegions[i][j].Count(); x++)
                            {
                                Point3D temp = new Point3D((double)(suppRegions[i][j][x].X) / scale, (double)(suppRegions[i][j][x].Y) / scale, l * layerHeight);
                                pts.Add(temp);
                            }
                            slicedModelWithShells[l][0].Add(pts);
                        }
                    }
                }
            }
            

            infill = generateInfill(slicedModelWithShells, infillDensity, boundingBox, shells, layerHeight);
           


            var mesh = meshBuilder.ToMesh();
            mesh.Normals = mesh.CalculateNormals();

            return (slicedModelWithShells, treeList, infill);
        }

        private (List<Paths>, List<int>) generateSupports(List<List<List<Point3DCollection>>> slicedModel, double infillDensity, Rect3D boundingBox, double shells, double layerHeight)
        {
            Paths result = new Paths();

            List<int> supportsRequired = new List<int>();

            for (int layer = 0; layer < slicedModel.Count() - 1; layer++)
            {
                List<Point3DCollection> currLayer = slicedModel[layer][0];
                List<Point3DCollection> topLayer = slicedModel[layer + 1][0];

                Paths subject = new Paths();
                Paths clip = new Paths();

                Paths supportRegion = new Paths();

                for (int i = 0; i < currLayer.Count(); i++)
                {
                    Path temp = new Path();

                    for (int j = 0; j < currLayer[i].Count(); j++)
                    {
                        temp.Add(new IntPoint(currLayer[i][j].X * scale, currLayer[i][j].Y * scale));
                    }
                    subject.Add(temp);
                }

                for (int i = 0; i < topLayer.Count(); i++)
                {
                    Path temp = new Path();

                    for (int j = 0; j < topLayer[i].Count(); j++)
                    {
                        temp.Add(new IntPoint(topLayer[i][j].X * scale, topLayer[i][j].Y * scale));
                    }
                    clip.Add(temp);
                }

                clip = Clipper.CleanPolygons(clip);
                subject = Clipper.CleanPolygons(subject);

                subject = erodePerimeter(subject, -0.2);

                result = calculateSelfSupported(clip, subject);

                if(result.Count() != 0)
                {
                    supportsRequired.Add(layer);
                }
            }

            List<Paths> regions = new List<Paths>();
            for (int i = 0; i < supportsRequired.Count(); i++)
            {
                Paths suppRegions = calcDifference(slicedModel[supportsRequired[i] + 1][0], slicedModel[supportsRequired[i]][0]);
                PolyTree suppTree = getPolyTreeStructureAtLayer(suppRegions, 0);
                Paths resultingRegions = new Paths();

                PolyNode node = suppTree.GetFirst();

                while(!(node is null))
                {
                    Paths temp = new Paths();
                    if (node.IsHole)
                    {
                        temp.Add(node.Contour);
                        temp = erodePerimeter(temp, -0.6);

                        foreach(Path p in temp)
                        {
                            resultingRegions.Add(p);
                        }
                    }
                    else
                    {
                        temp.Add(node.Contour);
                        temp = erodePerimeter(temp, 0.6);

                        foreach(Path p in temp)
                        {
                            resultingRegions.Add(p);
                        }
                    }

                    node = node.GetNext();
                }

                regions.Add(resultingRegions);
            }

            return (regions, supportsRequired);
        }

        private Paths calculateSelfSupported(Paths subject, Paths clip)
        {
            Clipper c = new Clipper();
            PolyTree result = new PolyTree();

            c.AddPaths(clip, PolyType.ptClip, true);
            c.AddPaths(subject, PolyType.ptSubject, true);

            c.Execute(ClipType.ctDifference, result, PolyFillType.pftPositive, PolyFillType.pftNonZero);

            Paths resPaths = Clipper.PolyTreeToPaths(result);
            resPaths = Clipper.CleanPolygons(resPaths);

            return resPaths;
        }

        private List<Paths> generateInfill(List<List<List<Point3DCollection>>> slicedModel, double infillDensity, Rect3D boundingBox, double shells, double layerHeight)
        {
            var tempRoof = calculateRoofRegions(slicedModel);
            var tempFloor = calculateFloorRegions(slicedModel);

            List<int> roofRegions = tempRoof.Item1;
            List<int> roofLayers = tempRoof.Item2;

            List<int> floorRegions = tempFloor.Item1;
            List<int> floorLayers = tempFloor.Item2;

            List<Paths> infillPerLayer = new List<Paths>();
            infillDensity = ((infillDensity / 100));

            for (int layer = 0; layer < slicedModel.Count(); layer++)
            {
                Paths infill = new Paths();
                Paths denseInfill = new Paths();

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
                    else if (roofRegions.Contains(layer) || floorRegions.Contains(layer))
                    {
                        if (rotation == 1)
                        {
                            denseInfill = FloorRoofRotationOne(boundingBox);
                            infill = infillRotationOne(boundingBox, infillDensity);
                            rotation = 0;
                        }
                        else
                        {
                            denseInfill = FloorRoofRotationTwo(boundingBox);
                            infill = infillRotationTwo(boundingBox, infillDensity);
                            rotation = 1;
                        }
                    }
                    else
                    {
                        //INFILL
                        if (rotation == 1)
                        {
                            infill = infillRotationOne(boundingBox, infillDensity);
                            rotation = 0;
                        }
                        else
                        {
                            infill = infillRotationTwo(boundingBox, infillDensity);
                            rotation = 1;
                        }
                    }
                }
                if (roofRegions.Contains(layer))
                {
                    int topL = 0;

                    for(int i = 0; i < roofLayers.Count(); i++)
                    {
                        if((roofLayers[i] - layer) <= 3)
                        {
                            topL = roofLayers[i];
                        }
                    }

                    Paths roofRegion = new Paths();
                    roofRegion = calcDifference(slicedModel[layer][(int)(shells - 1)], slicedModel[topL][(int)(shells - 1)]);

                    Paths restRegion = calcSparseInfill(roofRegion, slicedModel[layer][(int)(0)]);

                    Paths layerInfill = new Paths();

                    layerInfill = denseInfillIntersection(roofRegion, denseInfill, layer, shells);
                    Paths sparseInfill = new Paths();


                    Paths cInfill = new Paths();

                    if (checkInfillAvgSize(layerInfill))
                    {
                        foreach (Path p in layerInfill)
                        {
                            cInfill.Add(p);
                        }
                        sparseInfill = RestInfillIntersection(restRegion, infill, layer, shells, layerHeight);
                    }
                    else
                    {
                        sparseInfill = infillIntersection(slicedModel[layer], infill, layer, shells, layerHeight);
                    }

                    foreach(Path p in sparseInfill)
                    {
                        cInfill.Add(p);
                    }

                    infillPerLayer.Add(cInfill);

                }
                else if (floorRegions.Contains(layer))
                {
                    int botL = 0;

                    for (int i = 0; i < floorLayers.Count(); i++)
                    {
                        if ((floorLayers[i] - layer) <= 3)
                        {
                            botL = floorLayers[i];
                        }
                    }

                    Paths floorRegion = new Paths();
                    floorRegion = calcDifference(slicedModel[layer][(int)(shells - 1)], slicedModel[botL][(int)(shells - 1)]);

                    Paths restRegion = calcSparseInfill(floorRegion, slicedModel[layer][(int)(shells - 1)]);

                    Paths layerInfill = new Paths();

                    layerInfill = denseInfillIntersection(floorRegion, denseInfill, botL, shells);
                    Paths sparseInfill = new Paths();


                    Paths cInfill = new Paths();

                    if (checkInfillAvgSize(layerInfill))
                    {
                        foreach (Path p in layerInfill)
                        {
                            cInfill.Add(p);
                        }
                        sparseInfill = RestInfillIntersection(restRegion, infill, layer, shells, layerHeight);
                    }
                    else
                    {
                        sparseInfill = infillIntersection(slicedModel[layer], infill, layer, shells, layerHeight);
                    }

                    foreach (Path p in sparseInfill)
                    {
                        cInfill.Add(p);
                    }

                    infillPerLayer.Add(cInfill);
                }
                else
                {
                    infillPerLayer.Add(infillIntersection(slicedModel[layer], infill, layer, shells, layerHeight));
                }      
            }

            return infillPerLayer;
        }

        private Boolean checkInfillAvgSize(Paths infill)
        {
            double totalSize = 0;
            int counter = 0;

            foreach(Path p in infill)
            {
                totalSize += calculateEuclideanDistance(p[0], p[1]); 
            }

            totalSize = totalSize / infill.Count();

            if(totalSize > 15000)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        private Paths calcSparseInfill(Paths subject, List<Point3DCollection> topLayer)
        {
            Clipper c = new Clipper();
            Paths clip = new Paths();

            PolyTree result = new PolyTree();


            for (int i = 0; i < topLayer.Count(); i++)
            {
                Path temp = new Path();

                for (int j = 0; j < topLayer[i].Count(); j++)
                {
                    temp.Add(new IntPoint(topLayer[i][j].X * scale, topLayer[i][j].Y * scale));
                }
                clip.Add(temp);
            }

            c.AddPaths(subject, PolyType.ptClip, true);
            c.AddPaths(clip, PolyType.ptSubject, true);

            c.Execute(ClipType.ctDifference, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            Paths resPaths = Clipper.PolyTreeToPaths(result);
            resPaths = Clipper.CleanPolygons(resPaths);

            return resPaths;
        }

        private Paths calcDifference(List<Point3DCollection> currLayer, List<Point3DCollection> topLayer)
        {
            Clipper c = new Clipper();
            Paths subject = new Paths();
            Paths clip = new Paths();

            PolyTree result = new PolyTree();

            for (int i = 0; i < currLayer.Count(); i++)
            {
                Path temp = new Path();

                for (int j = 0; j < currLayer[i].Count(); j++)
                {
                    temp.Add(new IntPoint(currLayer[i][j].X * scale, currLayer[i][j].Y * scale));
                }
                subject.Add(temp);
            }

            for (int i = 0; i < topLayer.Count(); i++)
            {
                Path temp = new Path();

                for (int j = 0; j < topLayer[i].Count(); j++)
                {
                    temp.Add(new IntPoint(topLayer[i][j].X * scale, topLayer[i][j].Y * scale));
                }
                clip.Add(temp);
            }

            c.AddPaths(clip, PolyType.ptClip, true);
            c.AddPaths(subject, PolyType.ptSubject, true);

            c.Execute(ClipType.ctDifference, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            Paths resPaths = Clipper.PolyTreeToPaths(result);
            resPaths = Clipper.CleanPolygons(resPaths);

            return resPaths;
        }


        private (List<int>, List<int>) calculateRoofRegions(List<List<List<Point3DCollection>>> slicedModel)
        {
            List<int> roofs = new List<int>();
            List<int> roofLayers = new List<int>();

            for (int i = 3; i < slicedModel.Count() - 4; i++)
            {
                Paths intersect = new Paths();

                if(roofIntersection(slicedModel[i][0], slicedModel[i + 1][0]))
                {
                    roofLayers.Add(i + 1);

                    for(int j = 0; j < 3; j++)
                    {
                        roofs.Add(i - j);
                    }
                }
            }

            return (roofs, roofLayers);
        }


        private (List<int>, List<int>) calculateFloorRegions(List<List<List<Point3DCollection>>> slicedModel)
        {
            List<int> floors = new List<int>();
            List<int> floorLayers = new List<int>();

            for (int i = 3; i < slicedModel.Count() - 4; i++)
            {
                Paths intersect = new Paths();

                if (roofIntersection(slicedModel[i][0], slicedModel[i - 1][0]))
                {
                    floorLayers.Add(i - 1);

                    for (int j = 0; j < 3; j++)
                    {
                        floors.Add(i + j);
                    }
                }
            }

            return (floors, floorLayers);
        }


        private Boolean roofIntersection(List<Point3DCollection> currLayer, List<Point3DCollection> topLayer)
        {
            Clipper c = new Clipper();
            Paths subject = new Paths();
            Paths clip = new Paths();

            PolyTree result = new PolyTree();

            for(int i = 0; i < currLayer.Count(); i++)
            {
                Path temp = new Path();

                for(int j = 0; j < currLayer[i].Count(); j++)
                {
                    temp.Add(new IntPoint(currLayer[i][j].X * scale, currLayer[i][j].Y * scale));
                }
                subject.Add(temp);
            }

            for (int i = 0; i < topLayer.Count(); i++)
            {
                Path temp = new Path();

                for (int j = 0; j < topLayer[i].Count(); j++)
                {
                    temp.Add(new IntPoint(topLayer[i][j].X * scale, topLayer[i][j].Y * scale));
                }
                clip.Add(temp);
            }

            clip = Clipper.CleanPolygons(clip);
            subject = Clipper.CleanPolygons(subject);

            c.AddPaths(clip, PolyType.ptClip, true);
            c.AddPaths(subject, PolyType.ptSubject, true);

            c.Execute(ClipType.ctDifference, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            Paths resPaths = Clipper.PolyTreeToPaths(result);
            resPaths = Clipper.CleanPolygons(resPaths);

            // Paths regions = new Paths();

            /*for(int i = 0; i < currLayer.Count(); i++)
            {
                Path temp = new Path();
                for(int j = 0; j < currLayer[i].Count(); j++)
                {
                    if (!resPaths[i].Contains(new IntPoint(currLayer[i][j].X, currLayer[i][j].Y))){
                        temp.Add(new IntPoint(currLayer[i][j].X, currLayer[i][j].Y));
                    }
                }
                regions.Add(temp);
            }*/


            bool passed = false;
            for(int i = 0; i < resPaths.Count(); i++)
            {
                if(resPaths[i].Count() != 0)
                {
                    if (checkDifference(resPaths[i]))
                    {
                        passed = true;
                    }              
                }
            }

            if(passed)
            {
                return true;
            }

            /*for (int i = 0; i < subject.Count(); i++)
            {
                if (!resPaths[i].All(subject[i].Contains))
                {
                    return true;
                }
            }*/

            return false;
        }

        private Boolean checkDifference(Path polygon)
        {
            double totalSize = 0;
            int counter = 0;

            for(int i = 0; i < polygon.Count() - 1; i++)
            {
                totalSize += calculateEuclideanDistance(polygon[i], polygon[i+1]);
            }

            totalSize = totalSize / polygon.Count();

            if (totalSize > 10000)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        private Paths infillRotationOne(Rect3D boundingBox, double infillDensity)
        {
            Paths infill = new Paths();

            double infillLineNr = boundingBox.SizeY * infillDensity;
            double lineSpread = boundingBox.SizeY / infillLineNr;

            for (int i = 0; i < infillLineNr; i++)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint((int)((boundingBox.X) * scale), (i * lineSpread) * scale));
                lineSegment.Add(new IntPoint((boundingBox.X + boundingBox.SizeX) * scale, (i * lineSpread) * scale));

                infill.Add(lineSegment);
            }

            return infill;
        }

        private Paths infillRotationTwo(Rect3D boundingBox, double infillDensity)
        {
            Paths infill = new Paths();

            double infillLineNr = boundingBox.SizeX * infillDensity;
            double lineSpread = boundingBox.SizeX / infillLineNr;

            for (int i = 0; i < infillLineNr; i++)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(((i * lineSpread) * scale), (int)(boundingBox.Y) * scale));
                lineSegment.Add(new IntPoint(((i * lineSpread) * scale), (boundingBox.Y + boundingBox.SizeY) * scale));

                infill.Add(lineSegment);
            }

            return infill;
        }

        private Paths FloorRoofRotationOne(Rect3D boundingBox)
        {
            Paths infill = new Paths();

            for(double i = 0; i < 300; i += 0.6)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(0 * scale, i * scale));
                lineSegment.Add(new IntPoint(i * scale, 0 * scale));
                infill.Add(lineSegment);
            }

            return infill;
        }

        private Paths FloorRoofRotationTwo(Rect3D boundingBox)
        {

           

            Paths infill = new Paths();

            for (double i = 0; i < 300; i += 0.6)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(0 * scale, (boundingBox.SizeX - i) * scale));
                lineSegment.Add(new IntPoint(i * scale, boundingBox.SizeX * scale));
                infill.Add(lineSegment);

            }

            /*for (double i = 0; i < boundingBox.SizeX; i += 0.6)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(0 * scale, (boundingBox.SizeX - i) * scale));
                lineSegment.Add(new IntPoint(i * scale, boundingBox.SizeX * scale));

                infill.Add(lineSegment);
            }

            for (double i = 0; i < boundingBox.SizeX; i += 0.6)
            {
                Path lineSegment = new Path();
                lineSegment.Add(new IntPoint(i * scale, 0 * scale));
                lineSegment.Add(new IntPoint(boundingBox.SizeX * scale, (boundingBox.SizeX - i) * scale ));
                
                infill.Add(lineSegment);
            }*/

            return infill;
        }

        private Paths denseInfillIntersection(Paths region, Paths infill, int layer, double shells)
        {
            Paths intersectedInfill = new Paths();
            PolyTree result = new PolyTree();
            Clipper c = new Clipper();

            region = erodePerimeter(region, 0.2);

            Paths subject = new Paths();

            for (int i = 0; i < region.Count(); i++)
            {
                Path polygon = new Path();
                for (int j = 0; j < region[i].Count(); j++)
                {
                    polygon.Add(new IntPoint(region[i][j].X, region[i][j].Y));
                }
                subject.Add(polygon);
            }

            c.AddPaths(subject, PolyType.ptClip, true);
            c.AddPaths(infill, PolyType.ptSubject, false);

            c.Execute(ClipType.ctIntersection, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            Paths pResults = Clipper.PolyTreeToPaths(result);

            //pResults = region.Concat(pResults).ToList();

            return pResults;
        }

        private Paths RestInfillIntersection(Paths polygons, Paths infill, int layer, double shells, double layerHeight)
        {
            Paths intersectedInfill = new Paths();
            PolyTree result = new PolyTree();
            Clipper c = new Clipper();

            Paths subject = new Paths();

            PolyTree test = getPolyTreeStructureAtLayer(polygons, 0);

            PolyNode node = test.GetFirst();

            while (!(node is null))
            {
                if (!node.IsHole)
                {
                    Path polygon = new Path();
                    for (int i = 0; i < node.Contour.Count; i++)
                    {
                        polygon.Add(new IntPoint(node.Contour[i].X, node.Contour[i].Y));
                    }
                    Paths nTemp = new Paths();
                    nTemp.Add(polygon);
                    (Paths h, List<Point3DCollection> e) = ErodeLayer(nTemp, layer, (0.4 * (shells - 1)), layerHeight);

                    subject.AddRange(h);
                }
                else
                {
                    Path polygon = new Path();
                    for (int i = 0; i < node.Contour.Count; i++)
                    {
                        polygon.Add(new IntPoint(node.Contour[i].X, node.Contour[i].Y));
                    }
                    subject.Add(polygon);
                }

                node = node.GetNext();
            }


            c.AddPaths(subject, PolyType.ptClip, true);
            c.AddPaths(infill, PolyType.ptSubject, false);

            c.Execute(ClipType.ctIntersection, result, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);

            return Clipper.PolyTreeToPaths(result);
        }
        private Paths infillIntersection(List<List<Point3DCollection>> polygons, Paths infill, int layer, double shells, double layerHeight)
        {
            Paths intersectedInfill = new Paths();
            PolyTree result = new PolyTree();
            Clipper c = new Clipper();

            Paths temp = new Paths();
            Paths subject = new Paths();

            for(int i = 0; i < polygons[0].Count(); i++)
            {
                Path polygon = new Path();
                for(int j = 0; j < polygons[0][i].Count(); j++)
                {
                    polygon.Add(new IntPoint(polygons[0][i][j].X * scale, polygons[0][i][j].Y * scale));
                }
                temp.Add(polygon);
            }

            PolyTree test = getPolyTreeStructureAtLayer(temp, 0);

            PolyNode node = test.GetFirst();

            while (!(node is null))
            {
                if (!node.IsHole)
                {
                    Path polygon = new Path();
                    for (int i = 0; i < node.Contour.Count; i++)
                    {
                        polygon.Add(new IntPoint(node.Contour[i].X, node.Contour[i].Y));
                    }
                    Paths nTemp = new Paths();
                    nTemp.Add(polygon);
                    (Paths h, List <Point3DCollection> e)  = ErodeLayer(nTemp, layer, (0.4 * (shells - 1)), layerHeight);

                    subject.AddRange(h);
                }
                else
                {
                    Path polygon = new Path();
                    for (int i = 0; i < node.Contour.Count; i++)
                    {
                        polygon.Add(new IntPoint(node.Contour[i].X, node.Contour[i].Y));
                    }
                    subject.Add(polygon);
                }

                node = node.GetNext();
            }


            c.AddPaths(subject, PolyType.ptClip, true);
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

            erodedPaths = Clipper.CleanPolygons(erodedPaths);

            return erodedPaths;
        }

        public (Paths erodedPath, List<Point3DCollection> erodedLayer)  ErodeLayer(Paths connectedPoints, double layer, double diameter, double layerHeight)
        {
            Paths eroded = erodePerimeter(connectedPoints, diameter);
            List<Point3DCollection> polygonPoints = new List<Point3DCollection>();

            for (int i = 0; i < eroded.Count; i++)
            {
                Point3DCollection pts = new Point3DCollection();
                for (int j = 0; j < eroded[i].Count; j++)
                {
                    Point3D temp = new Point3D((double)(eroded[i][j].X) / scale, (double)(eroded[i][j].Y) / scale, layer * layerHeight);
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
    }
}
