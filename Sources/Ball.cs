using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Media.Media3D;

namespace Microsoft.Samples.Kinect.SkeletonBasics.Sources
{
    class Ball
    {
        private double _iniHeight;
        public double Height { get; set; }
        public double Radius { get; set; }
        public Point3D Center { get; set; }

        public Ball(double height, double radius)
        {
            this._iniHeight = height;
            this.Height = height;
            this.Radius = radius;

            Random random = new Random();
            double x = random.NextDouble() * 2 - 1;
            double z = random.NextDouble() * 2 - 1;

            this.Center = new Point3D(x, this.Height, z);
        }

        public void flyUp()
        {
            if (this.Height < this._iniHeight * 1.5)
            {
                this.Height += this._iniHeight * 0.05;
            }
            Random random = new Random();
            double randomDouble = random.NextDouble() - 0.5;
            double x = this.Center.X;
            double z = this.Center.Z;
            if (randomDouble > 0)
            {
                x += randomDouble * 2 + 1;
            }
            else
            {
                x += randomDouble * 2 - 1;
            }

            /*
            randomDouble = random.NextDouble() - 0.5;
            if (randomDouble > 0)
            {
                z += randomDouble * 2 + 1;
            }
            else
            {
                z += randomDouble * 2 - 1;
            }
            */

            this.Center = new Point3D(x, this.Height, z);
        }
    }
}
