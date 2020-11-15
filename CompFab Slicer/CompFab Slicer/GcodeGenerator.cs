using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            writer = new StreamWriter(filePath);
            WriteStaticEnder3Code(initTemp, initBedTemp);
            WriteGcode(layerHeight, nozzleDiameter);

            writer.Close();
        }



        private double CalculateExtrusion(double layerHeight, double nozzleDiameter, double length)
        {
            double lengthToPush = (layerHeight * nozzleDiameter * length) / (Math.PI/4 * nozzleDiameter * nozzleDiameter);
            return lengthToPush;
        }

        private void WriteGcode(double layerHeight, double nozzleDiameter)
        {

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
