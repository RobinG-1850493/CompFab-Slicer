using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CompFab_Slicer
{
    class GcodeGenerator
    {
        private List<List<Point3DCollection>> model;
        private string filePath;
        private StreamWriter writer;

        public GcodeGenerator(List<List<Point3DCollection>> model, string filePath, double layerHeight, double nozzleDiameter, double initTemp, double initBedTemp, double printTemp, double bedTemp, double printingSpeed)
        {
            this.model = model;
            this.filePath = filePath;
            double correctedSpeed = printingSpeed * 60; // convert mm/s to mm/m

            writer = new StreamWriter(filePath);
            WriteStaticEnder3Code(initTemp, initBedTemp);
            WriteGcode(layerHeight, nozzleDiameter, printTemp, bedTemp, correctedSpeed);

            writer.Close();
        }



        private double CalculateExtrusion(double layerHeight, double nozzleDiameter, double length)
        {
            double lengthToPush = (layerHeight * nozzleDiameter * length) / (Math.PI/4 * nozzleDiameter * nozzleDiameter);
            return lengthToPush;
        }

        private void WriteGcode(double layerHeight, double nozzleDiameter, double temperature, double bedTemperature, double printingSpeed)
        {
            WriteOneLayer(0, layerHeight, nozzleDiameter, printingSpeed);
        }

        private void WriteOneLayer(int layer, double layerHeight, double nozzleDiameter, double printingSpeed)
        {
            double extrusion = 0;
            for(int i = 0; i < model[layer].Count; i++)
            {
                Point previousPosition = new Point();
                Point currentPosition = new Point();
                double length;
                
                for(int j = 0; j < model[layer][i].Count; j++)
                {
                    if(j == 0)
                    {
                        previousPosition.X = model[layer][i][j].X;
                        previousPosition.Y = model[layer][i][j].Y;
                        
                        writer.WriteLine("G0 X" + model[layer][i][j].X + " Y" + model[layer][i][j].Y);
                    }
                    else
                    {
                        currentPosition.X = model[layer][i][j].X;
                        currentPosition.Y = model[layer][i][j].Y;
                        length = CalculateDistanceBetweenTwoPoints(previousPosition, currentPosition);

                        extrusion = CalculateExtrusion(layerHeight, nozzleDiameter, length);
                        writer.WriteLine("G1 X" + model[layer][i][j].X + " Y" + model[layer][i][j].Y + " E" + extrusion);
                    }
                }
                
            }
        }

        private double CalculateDistanceBetweenTwoPoints(Point p1, Point p2)
        {
            double distance = Math.Sqrt((Math.Pow((p1.X - p2.X), 2) + Math.Pow((p1.Y - p2.Y), 2)));
            return distance;
        }

        private void WriteStaticEnder3Code( double initTemp, double initBedTemp)
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
    }
}
