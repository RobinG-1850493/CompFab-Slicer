using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace CompFab_Slicer
{
    class GcodeGenerator
    {
        private List<List<List<Point3DCollection>>> model;
        private StreamWriter writer;
        private static double centerX = 110;
        private static double centerY = 110;
        private double extrusion;
        private double centerXOfModel;
        private double centerYOfModel;
        private double shells;
        private bool firstTime;
        private bool adhesionNeeded;
        private List<Paths> infills;

        public GcodeGenerator(List<List<List<Point3DCollection>>> model, List<Paths> infills, string filePath, double layerHeight, double nozzleDiameter, double initTemp, double initBedTemp, double printTemp, double bedTemp, double printingSpeed, double shells, double centerXOfModel, double centerYOfModel)
        {
            this.model = model;
            this.infills = infills;
            this.centerXOfModel = centerXOfModel;
            this.centerYOfModel = centerYOfModel;
            this.shells = shells;
            this.firstTime = true;
            double correctedSpeed = printingSpeed * 60; // convert mm/s to mm/m
            this.adhesionNeeded = false;

            writer = new StreamWriter(filePath);
            WriteStaticStartCode(initTemp, initBedTemp);
            WriteGcode(layerHeight, nozzleDiameter, printTemp, bedTemp, correctedSpeed);
            WriteStaticEndCode();
            writer.Close();
        }

        public GcodeGenerator(List<List<List<Point3DCollection>>> model, List<Paths> infills, string filePath, double layerHeight, double nozzleDiameter, double initTemp, double initBedTemp, double printTemp, double bedTemp, double printingSpeed, double shells, double centerXOfModel, double centerYOfModel, string adhesionType, Rect3D boundsOfObject)
        {
            this.model = model;
            this.infills = infills;
            this.centerXOfModel = centerXOfModel;
            this.centerYOfModel = centerYOfModel;
            this.shells = shells;
            this.firstTime = true;
            double correctedSpeed = printingSpeed * 60; // convert mm/s to mm/m
            this.adhesionNeeded = true;

            writer = new StreamWriter(filePath);
            WriteStaticStartCode(initTemp, initBedTemp);
            WriteGcode(layerHeight, nozzleDiameter, printTemp, bedTemp, correctedSpeed);
            WriteStaticEndCode();
            writer.Close();
        }



        private double CalculateExtrusion(double layerHeight, double nozzleDiameter, double flowModifier, double length)
        {
            double firstHalf = layerHeight * nozzleDiameter * length;
            double secondHalf = (Math.PI/4) * Math.Pow(1.75, 2);
            double lengthToPush = firstHalf / secondHalf;
            return lengthToPush;
        }

        private void WriteGcode(double layerHeight, double nozzleDiameter, double temperature, double bedTemperature, double printingSpeed)
        {

            for(int i = 0; i < model.Count; i++)
            {
                WriteOneLayer(i, layerHeight, nozzleDiameter, printingSpeed);
            }
        }

        private void WriteOneLayer(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {

            writer.WriteLine(";LAYER:" + layer);
            if(layer == 1)
            {
                //Turn fan on for 33%
                writer.WriteLine("M106 S85");
            }
            else if(layer == 2)
            {
                //turn fan on for 66%
                writer.WriteLine("M106 S170");
            } 
            else if(layer == 3)
            {
                //turn fan on for 100%
                writer.WriteLine("M106 S255");
            }

            WriteShells(layer, layerHeight, nozzleDiameter, printingSpeed);
            WriteSkinOrInfill(layer, layerHeight, nozzleDiameter, printingSpeed);
        }


        private void WriteSkin(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            writer.WriteLine(";STARTING WITH FLOOR/ROOF");
            Point startPoint = new Point();
            Point endPoint = new Point();
            double length;
            
            double positionZ = model[layer][0][0][0].Z;
            Paths tempInfillLayer = infills[layer];
            int infillLayerCount = tempInfillLayer.Count;

            for(int i = 0; i < infillLayerCount; i++)
            {
                if(i == 0)
                {
                    startPoint.X = CalculateCorrectXCoordinate(tempInfillLayer[i][0].X / 10000.0);
                    startPoint.Y = CalculateCorrectYCoordinate(tempInfillLayer[i][0].Y / 10000.0);

                    endPoint.X = CalculateCorrectXCoordinate(tempInfillLayer[i][1].X / 10000.0);
                    endPoint.Y = CalculateCorrectYCoordinate(tempInfillLayer[i][1].Y / 10000.0);

                    length = CalculateDistanceBetweenTwoPoints(startPoint, endPoint);
                    MoveToNextPositionWithRetraction(startPoint.X, startPoint.Y, positionZ, printingSpeed);
                    extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                    MoveAndExtrudeToPosition(endPoint.X, endPoint.Y, extrusion, printingSpeed / 2);
                    tempInfillLayer.Remove(tempInfillLayer[i]);
                }
                else
                {
                    int pointPositionInInfill = 0;
                    int coordinateXorY = 0;
                    double shortestDistance = 100000000;

                    for(int j = 0; j < tempInfillLayer.Count; j++)
                    {
                        for(int k = 0; k < tempInfillLayer[j].Count; k++)
                        {
                            
                            Point tempPoint = new Point(CalculateCorrectXCoordinate(tempInfillLayer[j][k].X / 10000.0), CalculateCorrectYCoordinate(tempInfillLayer[j][k].Y / 10000.0));
                            double distance = CalculateDistanceBetweenTwoPoints(endPoint, tempPoint);
                            if(distance < shortestDistance)
                            {
                                pointPositionInInfill = j;
                                coordinateXorY = k;
                                shortestDistance = distance;
                            }
                        }
                    }

                    startPoint.X = CalculateCorrectXCoordinate(tempInfillLayer[pointPositionInInfill][coordinateXorY].X / 10000.0);
                    startPoint.Y = CalculateCorrectYCoordinate(tempInfillLayer[pointPositionInInfill][coordinateXorY].Y / 10000.0);

                    if(coordinateXorY == 0)
                    {
                        endPoint.X = CalculateCorrectXCoordinate(tempInfillLayer[pointPositionInInfill][1].X / 10000.0);
                        endPoint.Y = CalculateCorrectYCoordinate(tempInfillLayer[pointPositionInInfill][1].Y / 10000.0);
                    }
                    else
                    {
                        endPoint.X = CalculateCorrectXCoordinate(tempInfillLayer[pointPositionInInfill][0].X / 10000.0);
                        endPoint.Y = CalculateCorrectYCoordinate(tempInfillLayer[pointPositionInInfill][0].Y / 10000.0);
                    }

                    length = CalculateDistanceBetweenTwoPoints(startPoint, endPoint);
                    if(shortestDistance < 0.8)
                    {
                        MoveToNextPosition(startPoint.X, startPoint.Y, positionZ, printingSpeed);
                    }
                    else
                    {
                        MoveToNextPositionWithRetraction(startPoint.X, startPoint.Y, positionZ, printingSpeed);
                    }
                    extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                    MoveAndExtrudeToPosition(endPoint.X, endPoint.Y, extrusion, printingSpeed / 2);
                    tempInfillLayer.Remove(tempInfillLayer[pointPositionInInfill]);
                }
            }

            /*for (int i = 0; i < infills[layer].Count; i++)
            {
                if (rotation == 1)
                {
                    startPoint.X = CalculateCorrectXCoordinate(infills[layer][i][0].X / 10000.0);
                    startPoint.Y = CalculateCorrectYCoordinate(infills[layer][i][0].Y / 10000.0);

                    endPoint.X = CalculateCorrectXCoordinate(infills[layer][i][1].X / 10000.0);
                    endPoint.Y = CalculateCorrectYCoordinate(infills[layer][i][1].Y / 10000.0);
                    rotation = 0;
                }
                else
                {
                    startPoint.X = CalculateCorrectXCoordinate(infills[layer][i][1].X / 10000.0);
                    startPoint.Y = CalculateCorrectYCoordinate(infills[layer][i][1].Y / 10000.0);

                    endPoint.X = CalculateCorrectXCoordinate(infills[layer][i][0].X / 10000.0);
                    endPoint.Y = CalculateCorrectYCoordinate(infills[layer][i][0].Y / 10000.0);
                    rotation = 1;
                }

                length = CalculateDistanceBetweenTwoPoints(startPoint, endPoint);
                MoveToNextPositionWithRetraction(startPoint.X, startPoint.Y, positionZ, printingSpeed);
                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                MoveAndExtrudeToPosition(endPoint.X, endPoint.Y, extrusion, printingSpeed / 2);

            }*/

        }

        private void WriteInfill(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            writer.WriteLine(";STARTING WITH INFILL");
            Point startPoint = new Point();
            Point endPoint = new Point();
            double length;
            double positionZ = model[layer][0][0][0].Z;

            for (int i = 0; i < infills[layer].Count; i++)
            {

                startPoint.X = CalculateCorrectXCoordinate(infills[layer][i][0].X / 10000.0);
                startPoint.Y = CalculateCorrectYCoordinate(infills[layer][i][0].Y / 10000.0);

                endPoint.X = CalculateCorrectXCoordinate(infills[layer][i][1].X / 10000.0);
                endPoint.Y = CalculateCorrectYCoordinate(infills[layer][i][1].Y / 10000.0);
                
                length = CalculateDistanceBetweenTwoPoints(startPoint, endPoint);
                MoveToNextPositionWithRetraction(startPoint.X, startPoint.Y, positionZ, printingSpeed);
                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                MoveAndExtrudeToPosition(endPoint.X, endPoint.Y, extrusion, printingSpeed);
            }
        }

        private void WriteSkinOrInfill(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            writer.WriteLine(";STARTING WITH FLOOR/ROOF/INFILL");

            if (layer < shells || layer > (model.Count() - (shells + 1)))
            {
                WriteSkin(layer, layerHeight, nozzleDiameter, printingSpeed);
            }
            else
            {
                WriteInfill(layer, layerHeight, nozzleDiameter, printingSpeed);
            }
        }

        private void WriteShells(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            for (int i = (int)(shells - 1); i >= 0; i--)
            {

                for (int polygons = 0; polygons < model[layer][i].Count; polygons++)
                {
                    Point previousPosition = new Point();
                    Point currentPosition = new Point();
                    double length;
                    double positionZ = model[layer][0][0][0].Z;


                    previousPosition.X = CalculateCorrectXCoordinate(model[layer][i][polygons][0].X);
                    previousPosition.Y = CalculateCorrectYCoordinate(model[layer][i][polygons][0].Y);

                    MoveToNextPositionWithRetraction(previousPosition.X, previousPosition.Y, positionZ, printingSpeed);

                    for (int j = 0; j < model[layer][i][polygons].Count; j++)
                    {
                        if (j == 0)
                        {
                            previousPosition.X = CalculateCorrectXCoordinate(model[layer][i][polygons][j].X);
                            previousPosition.Y = CalculateCorrectYCoordinate(model[layer][i][polygons][j].Y);

                            MoveToNextPosition(previousPosition.X, previousPosition.Y, printingSpeed);
                        }
                        else
                        {
                            if (j == 1)
                            {
                                currentPosition.X = CalculateCorrectXCoordinate(model[layer][i][polygons][j].X);
                                currentPosition.Y = CalculateCorrectYCoordinate(model[layer][i][polygons][j].Y);

                                length = CalculateDistanceBetweenTwoPoints(previousPosition, currentPosition);
                                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                                MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion, printingSpeed);
                                previousPosition = currentPosition;
                            }
                            else
                            {
                                currentPosition.X = CalculateCorrectXCoordinate(model[layer][i][polygons][j].X);
                                currentPosition.Y = CalculateCorrectYCoordinate(model[layer][i][polygons][j].Y);
                                
                                length = CalculateDistanceBetweenTwoPoints(previousPosition, currentPosition);
                                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                                MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion);
                                previousPosition = currentPosition;
                            }
                        }
                       
                    }
                    //go back to first position
                    currentPosition.X = CalculateCorrectXCoordinate(model[layer][i][polygons][0].X);
                    currentPosition.Y = CalculateCorrectYCoordinate(model[layer][i][polygons][0].Y);

                    length = CalculateDistanceBetweenTwoPoints(previousPosition, new Point(currentPosition.X, currentPosition.Y));
                    extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                    MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion);

                }


                
            }
        }

        /*private void WriteOneShell(int shellNumber, int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            for (int polygons = 0; polygons < model[layer][0].Count; polygons++)
            {
                Point previousPosition = new Point();
                Point currentPosition = new Point();
                double length;

                for (int j = 0; j < model[layer][shellNumber][polygons].Count; j++)
                {

                    for (int i = (int)(shells - 1); i >= 0; i--)
                    {
                        if (j == 0)
                        {
                            previousPosition.X = CalculateCorrectXCoordinate(model[layer][shellNumber][polygons][j].X);
                            previousPosition.Y = CalculateCorrectYCoordinate(model[layer][shellNumber][polygons][j].Y);
                            double positionZ = model[layer][shellNumber][polygons][j].Z;


                            MoveToNextPosition(previousPosition.X, previousPosition.Y, positionZ, printingSpeed);
                        }
                        else
                        {
                            if (j == 1)
                            {
                                currentPosition.X = CalculateCorrectXCoordinate(model[layer][shellNumber][polygons][j].X);
                                currentPosition.Y = CalculateCorrectYCoordinate(model[layer][shellNumber][polygons][j].Y);

                                length = CalculateDistanceBetweenTwoPoints(previousPosition, currentPosition);
                                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                                MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion, printingSpeed);
                                previousPosition = currentPosition;
                            }
                            else
                            {
                                currentPosition.X = CalculateCorrectXCoordinate(model[layer][shellNumber][polygons][j].X);
                                currentPosition.Y = CalculateCorrectYCoordinate(model[layer][shellNumber][polygons][j].Y);

                                length = CalculateDistanceBetweenTwoPoints(previousPosition, currentPosition);
                                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                                MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion);
                                previousPosition = currentPosition;
                            }
                        }
                    }

                    
                }

                //go back to first position
                currentPosition.X = CalculateCorrectXCoordinate(model[layer][shellNumber][polygons][0].X);
                currentPosition.Y = CalculateCorrectYCoordinate(model[layer][shellNumber][polygons][0].Y);

                length = CalculateDistanceBetweenTwoPoints(previousPosition, new Point(currentPosition.X, currentPosition.Y));
                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion);
            }
        }*/

        private void MoveAndExtrudeToPosition(double x, double y, double extrusion)
        {
            writer.WriteLine("G1 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void MoveAndExtrudeToPosition(double x, double y, double extrusion, double printingSpeed)
        {
            writer.WriteLine("G1 F" + printingSpeed + " X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void MoveToNextPositionWithRetraction(double x, double y, double printingSpeed)
        {
            double tempExtrusion = 0.0;

            if (firstTime)
            {
                writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteLine("G1 F" + printingSpeed + " E0");
                firstTime = false;
            }
            else
            {
                if(extrusion < 6.5)
                {
                    tempExtrusion = extrusion;
                    extrusion = 0;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    extrusion = tempExtrusion;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    extrusion -= 6.5;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    extrusion += 6.5;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        private void MoveToNextPositionWithRetraction(double x, double y, double z, double printingSpeed)
        {
            double tempExtrusion = 0.0;

            if (firstTime)
            {
                writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteLine("G1 F" + printingSpeed + " E0");
                firstTime = false;
            } else
            {
                if (extrusion < 6.5)
                {
                    tempExtrusion = extrusion;
                    extrusion = 0;
                    z += 0.2;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    z -= 0.2;
                    writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    extrusion = tempExtrusion;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    extrusion -= 6.5;
                    z += 0.2;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    z -= 0.2;
                    writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                    extrusion += 6.5;
                    writer.WriteLine("G1 F" + printingSpeed + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        private void MoveToNextPosition(double x, double y, double printingSpeed)
        {
            writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
            if (firstTime)
            {
                writer.WriteLine("G1 F" + printingSpeed + " E0");
                firstTime = false;
            }
        }

        private void MoveToNextPosition(double x, double y, double z, double printingSpeed)
        {
            writer.WriteLine("G0 F9000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
            if (firstTime)
            {
                writer.WriteLine("G1 F" + printingSpeed + " E0");
                firstTime = false;
            }
        }

        private double CalculateCorrectXCoordinate(double coordinate)
        {
            double coordinateToReturn;
            coordinateToReturn = coordinate - centerXOfModel + centerX;

            return coordinateToReturn;
        }

        private double CalculateCorrectYCoordinate(double coordinate)
        {
            double coordinateToReturn;
            coordinateToReturn = coordinate - centerYOfModel + centerY;

            return coordinateToReturn;
        }

        private double CalculateDistanceBetweenTwoPoints(Point p1, Point p2)
        {
            double distance = Math.Sqrt((Math.Pow((p1.X - p2.X), 2) + Math.Pow((p1.Y - p2.Y), 2)));
            return distance;
        }

        private void WriteStaticStartCode( double initTemp, double initBedTemp)
        {
            writer.WriteLine("M140 S" + initBedTemp.ToString()); //set bed temperature to initbedtemp
            writer.WriteLine("M105");                            //update Ender3 display
            writer.WriteLine("M190 S" + initBedTemp.ToString()); //Wait till bed temperature has reached
            writer.WriteLine("M104 S" + initTemp.ToString());    //set printing temperature to initTemp 
            writer.WriteLine("M105");                            //update Ender3 display
            writer.WriteLine("M109 S" + initTemp.ToString());    //Wait till print temperature has reached
            writer.WriteLine("M82");                             //absolute extrusion mode
            writer.WriteLine("G92 E0");                          //Reset extruder
            writer.WriteLine("G28");                             //Home all axes
            writer.WriteLine("G1 Z2.0 F3000");                   //Move Z Axis up little to prevent scratching
            writer.WriteLine("G1 X0.1 Y20 Z0.3 F5000.0");        //Move to start position
            writer.WriteLine("G1 X0.1 Y200.0 Z0.3 F1500.0 E15"); //Draw the first line
            writer.WriteLine("G1 X0.4 Y200.0 Z0.3 F5000.0");     //Move to side a little
            writer.WriteLine("G1 X0.4 Y20 Z0.3 F1500.0 E30");    //Draw the second line
            writer.WriteLine("G92 E0");                          //Reset Extruder
            writer.WriteLine("G1 Z2.0 F3000");                   //Move Z Axis up little to prevent scratching
            writer.WriteLine("G1 X5 Y20 Z0.3 F5000.0");          //Move over to prevent blob squish
            writer.WriteLine("G92 E0");                          //Reset Extruder
            writer.WriteLine("G92 E0");                          //Reset Extruder
            writer.WriteLine("G1 F1800 E-6.5");                  //Same as G28
            writer.WriteLine("M107");                            //set fan off
        }

        private void WriteStaticEndCode()
        {
            writer.WriteLine("M140 S0");
            writer.WriteLine("M107");
            writer.WriteLine("G91");
            writer.WriteLine("G1 E-2 F2700");
            writer.WriteLine("G1 E-2 Z1 F2400");
            writer.WriteLine("G1 X5 Y5 F3000");
            writer.WriteLine("G1 Z10");
            writer.WriteLine("G90");
            writer.WriteLine("G1 X0 Y220");
            writer.WriteLine("M106 S0");
            writer.WriteLine("M104 S0");
            writer.WriteLine("M140 S0");
            writer.WriteLine("M84 X Y E");
            writer.WriteLine("M82");
            writer.WriteLine("M104 S0");
        }
    }
}
