using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Media.Media3D;

namespace Microsoft.Samples.Kinect.SkeletonBasics.Sources
{
    class Ball
    {
        public double Height { get; set; }
        public double Radius { get; set; }

        public Ball(double height, double radius)
        {
            this.Height = height;
            this.Radius = radius;
        }

        public void flyUp()
        {
            this.Height *= 1.05;
        }

        public Point3D getBallCenter()
        {
            Random random = new Random();
            double x = random.NextDouble() * 3;
            double z = random.NextDouble() * 3;

            return new Point3D(x, this.Height, z);
        }
    }
}
