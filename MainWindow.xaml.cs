﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Media.Media3D;
    using HelixToolkit.Wpf;

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

        private Point3D originalCenter;

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

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
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
                    }
                }
            }

            /*
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }

            */
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

            this.DrawSphere3D(this.SkeletonPointTo3D(joint.Position), 2, viewport);
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
                this.DrawJoint3D(joint, viewport);
            }
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

        private void DrawSphere3D(Point3D center, double radius, HelixToolkit.Wpf.HelixViewport3D viewport)
        {
            SphereVisual3D sphere = new SphereVisual3D();
            sphere.Center = center;
            sphere.Radius = radius;
            sphere.Fill = Brushes.DarkOliveGreen;

            viewport.Children.Add(sphere);
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

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
    }
}