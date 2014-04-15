//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Media.Media3D;
    using HelixToolkit.Wpf;

    using Microsoft.Samples.Kinect.SkeletonBasics.Sources;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        private bool isAligned = false;

        private bool hasBall = false;

        private string logFileName;

        private Point3D originalCenter;

        private TextWriter logWriter;

        public int touchDownCount { get; set; }
        private bool hasTouched;

        private double ballHeight;

        private Ball ball;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            var selection = MessageBox.Show("Do you want to perform touch-down or play a game?", "Choose a mode", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            this.DataLogWindow.DataContext = this.touchDownCount;
            this.ball = new Ball(6, 0.5);
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            this.MainViewPort.Camera.Position = new Point3D(-20, 0, 15);
            this.MainViewPort.Camera.LookDirection = new Vector3D(10, 0, -5);
            this.MainViewPort.Camera.UpDirection = new Vector3D(1, 0, 0);

            this.touchDownCount = 0;
            this.hasTouched = false;

        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            if (skeletons.Length != 0)
            {
                foreach (Skeleton skel in skeletons)
                {
                    if (skel.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        this.DrawBonesAndJoints(skel, this.MainViewPort);

                        //this.DrawSphere3D(this.Point3DChangeView(this.ball.getBallCenter()), 0.5, Brushes.Red, this.MainViewPort);

                        if (!this.hasTouched && this.CheckTouchDown(skel))
                        {
                            this.touchDownCount += 1;
                            this.hasTouched = true;
                            this.DataLogWindow.Text = "Number of touch-down performed: " + this.touchDownCount.ToString();
                        }

                        if (this.hasTouched && this.CheckRestorePosition(skel))
                        {
                            this.hasTouched = false;
                        }
                    }
                }
            }
        }

        private bool CheckRestorePosition(Skeleton skeleton)
        {
            Joint leftHand = skeleton.Joints[JointType.HandLeft];
            Joint rightHand = skeleton.Joints[JointType.HandRight];
            Joint leftFoot = skeleton.Joints[JointType.FootLeft];
            Joint rightFoot = skeleton.Joints[JointType.FootRight];

            if (leftHand.TrackingState == JointTrackingState.NotTracked ||
               rightHand.TrackingState == JointTrackingState.NotTracked ||
               leftFoot.TrackingState == JointTrackingState.NotTracked ||
               rightFoot.TrackingState == JointTrackingState.NotTracked)
            {
                return false;
            }

            return this.IsBoneVertical(skeleton, JointType.HipLeft, JointType.KneeLeft, 20) &&
                   this.IsBoneVertical(skeleton, JointType.HipRight, JointType.KneeRight, 20) &&
                   (this.CalJointDist(skeleton, JointType.HandLeft, JointType.FootLeft) > 2) &&
                   (this.CalJointDist(skeleton, JointType.HandRight, JointType.FootRight) > 2);
        }

        // generate ball with randomized x and z position
        // but at a fixed height
        private void generateBall()
        {
            Random random = new Random();
            double x_offset = random.NextDouble();
            double z_offset = random.NextDouble();

            Point3D center = new Point3D(x_offset + this.originalCenter.X, this.ballHeight, z_offset + this.originalCenter.Z);
            this.DrawSphere3D(this.Point3DChangeView(center), 0.5, Brushes.Red, this.MainViewPort);
        }

        private Point3D getOriginalCenter(Skeleton skeleton)
        {
            SkeletonPoint leftFootPos = skeleton.Joints[JointType.FootLeft].Position;
            SkeletonPoint rightFootPos = skeleton.Joints[JointType.FootRight].Position;

            float x = (leftFootPos.X + rightFootPos.X) / 2;
            float y = (leftFootPos.Y + rightFootPos.Y) / 2;
            float z = (leftFootPos.Z + rightFootPos.Z) / 2;

            isAligned = true;
            return new Point3D(x, y, z);
        }

        private void DrawJoint3D(Joint joint, HelixToolkit.Wpf.HelixViewport3D viewport)
        {
            // If we can't find either of these joints, exit
            if (joint.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            this.DrawSphere3D(this.SkeletonPointTo3D(joint.Position), 0.2, Brushes.Black, viewport);
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, HelixToolkit.Wpf.HelixViewport3D viewport)
        {
            viewport.Children.Clear();
            viewport.Children.Add(new SunLight());
            viewport.Children.Add(new GridLinesVisual3D());

            if (!isAligned)
            {
                originalCenter = getOriginalCenter(skeleton);
            }

            // Render Torso
            this.DrawBone3D(skeleton, viewport, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone3D(skeleton, viewport, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone3D(skeleton, viewport, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone3D(skeleton, viewport, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone3D(skeleton, viewport, JointType.Spine, JointType.HipCenter);
            this.DrawBone3D(skeleton, viewport, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone3D(skeleton, viewport, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone3D(skeleton, viewport, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone3D(skeleton, viewport, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone3D(skeleton, viewport, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone3D(skeleton, viewport, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone3D(skeleton, viewport, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone3D(skeleton, viewport, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone3D(skeleton, viewport, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone3D(skeleton, viewport, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone3D(skeleton, viewport, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone3D(skeleton, viewport, JointType.HipRight, JointType.KneeRight);
            this.DrawBone3D(skeleton, viewport, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone3D(skeleton, viewport, JointType.AnkleRight, JointType.FootRight);

            // Render joints
            foreach (Joint joint in skeleton.Joints)
            {
                //this.DrawJoint3D(joint, viewport);
            }

            this.DrawJoint3D(skeleton.Joints[JointType.Head], viewport);
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        private Point3D SkeletonPointTo3D(SkeletonPoint skelpoint)
        {
            double y = (skelpoint.X - originalCenter.X) * 4;
            double z = (skelpoint.Y - originalCenter.Y) * 4;
            double x = (skelpoint.Z - originalCenter.Z) * 4;

            // Convert point to 3D spac
            return new Point3D(x, y, z);
        }

        private Point3D Point3DChangeView(Point3D point)
        {
            double y = (point.X + originalCenter.X);
            double z = (point.Y + originalCenter.Y);
            double x = (point.Z + originalCenter.Z);

            // Convert point to 3D spac
            return new Point3D(x, y, z);
        }

        private void DrawBone3D(Skeleton skeleton, HelixToolkit.Wpf.HelixViewport3D viewport, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            this.DrawLine3D(this.SkeletonPointTo3D(joint0.Position), this.SkeletonPointTo3D(joint1.Position), viewport);
        }

        private void DrawLine3D(Point3D p0, Point3D p1, HelixToolkit.Wpf.HelixViewport3D viewport)
        {
            double tubeDiameter = 0.1;

            TubeVisual3D tube = new TubeVisual3D();
            tube.Path = new Point3DCollection();
            tube.Path.Add(p0);
            tube.Path.Add(p1);
            tube.Diameter = tubeDiameter;
            tube.Fill = Brushes.Black;
            tube.IsPathClosed = true;

            viewport.Children.Add(tube);
        }

        private void DrawSphere3D(Point3D center, double radius, Brush brush, HelixToolkit.Wpf.HelixViewport3D viewport)
        {
            SphereVisual3D sphere = new SphereVisual3D();
            sphere.Center = center;
            sphere.Radius = radius;
            sphere.Fill = brush;

            viewport.Children.Add(sphere);
        }

        private double CalAngle(Point3D left, Point3D center, Point3D right)
        {
            double sideL = left.DistanceTo(center);
            double sideR = right.DistanceTo(center);
            double sideC = left.DistanceTo(right);

            return Math.Acos((sideL * sideL + sideR * sideR - sideC * sideC) / (2 * sideL * sideR)) * 180 / Math.PI;
        }

        private double CalJointDist(Skeleton skeleton, JointType jointTypeLeft, JointType jointTypeRight)
        {
            Joint jointLeft = skeleton.Joints[jointTypeLeft];
            Joint jointRight = skeleton.Joints[jointTypeRight];

            // if we can't find either of these joints, return negative infinity
            if (jointLeft.TrackingState == JointTrackingState.NotTracked ||
               jointRight.TrackingState == JointTrackingState.NotTracked)
            {
                return double.NegativeInfinity;
            }

            return this.SkeletonPointTo3D(jointLeft.Position).DistanceTo(this.SkeletonPointTo3D(jointRight.Position));
        }

        private double CalJointAngle(Skeleton skeleton, JointType jointTypeLeft, JointType jointTypeCenter, JointType jointTypeRight)
        {
            Joint jointLeft = skeleton.Joints[jointTypeLeft];
            Joint jointCenter = skeleton.Joints[jointTypeCenter];
            Joint jointRight = skeleton.Joints[jointTypeRight];

            // If we can't find either of these joints, return negative infinity
            if (jointLeft.TrackingState == JointTrackingState.NotTracked ||
                jointCenter.TrackingState == JointTrackingState.NotTracked ||
                jointRight.TrackingState == JointTrackingState.NotTracked)
            {
                return double.NegativeInfinity;
            }

            return CalAngle(this.SkeletonPointTo3D(jointLeft.Position),
                            this.SkeletonPointTo3D(jointCenter.Position),
                            this.SkeletonPointTo3D(jointRight.Position));
        }

        private bool CheckBallTouch(Point3D ballCenter, double radius, Skeleton skeleton, JointType jointType)
        {
            Joint joint = skeleton.Joints[jointType];

            if (joint.TrackingState == JointTrackingState.NotTracked)
            {
                return false;
            }

            return ballCenter.DistanceTo(this.SkeletonPointTo3D(joint.Position)) < radius;
        }

        private void RecordData(DateTime timeStamp, string logData)
        {
            logWriter.WriteLine(timeStamp.ToShortTimeString() + logData);
        }

        private void InitLogFile()
        {
            logFileName = DateTime.Now.ToString();
            logFileName = "c:\\" + logFileName + ".log";

            logWriter = new StreamWriter(logFileName);
            // TODO: write header
            logWriter.WriteLine("");
        }

        /*
         * data formart:
         *  time stamp,
         *  distance between left hand and left foot,
         *  distance between right hand and right foot,
         *  angle of left thigh
         *  angle of right thigh
         *  head height
         */
        private void DisplayAndLogData(Skeleton skeleton)
        {

        }

        private bool CheckTouchDown(Skeleton skeleton)
        {
            Joint leftHand = skeleton.Joints[JointType.HandLeft];
            Joint rightHand = skeleton.Joints[JointType.HandRight];
            Joint leftFoot = skeleton.Joints[JointType.FootLeft];
            Joint rightFoot = skeleton.Joints[JointType.FootRight];

            if (leftHand.TrackingState == JointTrackingState.NotTracked ||
               rightHand.TrackingState == JointTrackingState.NotTracked ||
               leftFoot.TrackingState == JointTrackingState.NotTracked ||
               rightFoot.TrackingState == JointTrackingState.NotTracked)
            {
                return false;
            }

            return this.IsBoneVertical(skeleton, JointType.HipLeft, JointType.KneeLeft, 30) &&
                   this.IsBoneVertical(skeleton, JointType.HipRight, JointType.KneeRight, 30) &&
                   (this.CalJointDist(skeleton, JointType.HandLeft, JointType.FootLeft) < 2) &&
                   (this.CalJointDist(skeleton, JointType.HandRight, JointType.FootRight) < 2);
        }

        private bool IsBoneVertical(Skeleton skeleton, JointType jointTypeTop, JointType jointTypeBottom, double tolerance)
        {
            Joint jointTop = skeleton.Joints[jointTypeTop];
            Joint jointBottom = skeleton.Joints[jointTypeBottom];

            if (jointTop.TrackingState == JointTrackingState.NotTracked ||
               jointBottom.TrackingState == JointTrackingState.NotTracked)
            {
                return false;
            }

            Point3D bottom = this.SkeletonPointTo3D(jointBottom.Position);
            Point3D top = this.SkeletonPointTo3D(jointTop.Position);
            Point3D refBottom = top;
            refBottom.Offset(0, 0, -1);

            return tolerance > this.CalAngle(bottom, top, refBottom);
        }

        private bool IsBoneHorizontal(Skeleton skeleton, JointType jointTypeLeft, JointType jointTypeRight, double tolerance)
        {
            Joint jointLeft = skeleton.Joints[jointTypeLeft];
            Joint jointRight = skeleton.Joints[jointTypeRight];

            if (jointLeft.TrackingState == JointTrackingState.NotTracked ||
               jointRight.TrackingState == JointTrackingState.NotTracked)
            {
                return false;
            }

            Point3D left = this.SkeletonPointTo3D(jointLeft.Position);
            Point3D right = this.SkeletonPointTo3D(jointRight.Position);
            Point3D refLeft = left;
            refLeft.Offset(0, 0, -1);

            return tolerance > this.CalAngle(left, right, refLeft);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }
    }
}