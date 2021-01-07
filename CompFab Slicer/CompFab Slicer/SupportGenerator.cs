using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;
using ClipperLib;

namespace CompFab_Slicer
{
    public class SupportGenerator
    {
        private List<List<List<Point3DCollection>>> model;
        public SupportGenerator(List<List<List<Point3DCollection>>> model)
        {
            this.model = model;
        }


    }
}
