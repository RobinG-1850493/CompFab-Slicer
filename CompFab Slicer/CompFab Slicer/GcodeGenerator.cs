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
        private int rotation = 0;
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
            WriteShells(layer, layerHeight, nozzleDiameter, printingSpeed);
            WriteSkin(layer, layerHeight, nozzleDiameter, printingSpeed);
        }

        private void WriteSkin(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            Point startPoint = new Point();
            Point endPoint = new Point();
            double length;

            for (int i = 0; i < infills[layer].Count; i++)
            {

                startPoint.X = CalculateCorrectXCoordinate(infills[layer][i][0].X / 10000);
                startPoint.Y = CalculateCorrectXCoordinate(infills[layer][i][0].Y / 10000);

                endPoint.X = CalculateCorrectXCoordinate(infills[layer][i][1].X / 10000);
                endPoint.Y = CalculateCorrectXCoordinate(infills[layer][i][1].Y / 10000);

                length = CalculateDistanceBetweenTwoPoints(startPoint, endPoint);
                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                MoveToNextPosition(startPoint.X, startPoint.Y, printingSpeed * 1.5);
                MoveAndExtrudeToPosition(endPoint.X, endPoint.Y, extrusion, printingSpeed);
            }
        }

        private void WriteShells(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            for(int i = (int)(shells - 1); i >= 0; i--)
            { 
                WriteOneShell(i, layer, layerHeight, nozzleDiameter, printingSpeed);
            }
        }

        private void WriteOneShell(int shellNumber, int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            for (int polygons = 0; polygons < model[layer][shellNumber].Count; polygons++)
            {
                Point previousPosition = new Point();
                Point currentPosition = new Point();
                double length;

                for (int j = 0; j < model[layer][shellNumber][polygons].Count; j++)
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

                //go back to first position
                currentPosition.X = CalculateCorrectXCoordinate(model[layer][shellNumber][polygons][0].X);
                currentPosition.Y = CalculateCorrectYCoordinate(model[layer][shellNumber][polygons][0].Y);

                length = CalculateDistanceBetweenTwoPoints(previousPosition, new Point(currentPosition.X, currentPosition.Y));
                extrusion += CalculateExtrusion(layerHeight, nozzleDiameter, 1, length);

                MoveAndExtrudeToPosition(currentPosition.X, currentPosition.Y, extrusion);
            }
        }

        private void MoveAndExtrudeToPosition(double x, double y, double extrusion)
        {
            writer.WriteLine("G1 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void MoveAndExtrudeToPosition(double x, double y, double extrusion, double printingSpeed)
        {
            writer.WriteLine("G1 F" + printingSpeed + " X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " E" + extrusion.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void MoveToNextPosition(double x, double y, double printingSpeed)
        {
            writer.WriteLine("G0 F3000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
            if (firstTime)
            {
                writer.WriteLine("G1 F" + printingSpeed + " E0");
                firstTime = false;
            }
        }

        private void MoveToNextPosition(double x, double y, double z, double printingSpeed)
        {
            writer.WriteLine("G0 F3000 X" + x.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Y" + y.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + " Z" + z.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
            if(firstTime)
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
