using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Media.Media3D;

namespace Microsoft.Samples.Kinect.SkeletonBasics.Sources
{
    class Ball
    {
        public Point3D Center { get; set; }
        public double Radius { get; set; }

        public Ball(Point3D center, double radius)
        {
            this.Center = center;
            this.Radius = radius;
        }

        public void flyUp()
        {
            this.Center.Offset(0, this.Center.Y * 0.05, 0);
        }
    }
}
