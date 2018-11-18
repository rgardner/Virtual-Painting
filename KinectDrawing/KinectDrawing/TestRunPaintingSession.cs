using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectDrawing
{
    class TestRunPaintingSession : IPaintingSession
    {
        private class RawJointData
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float CameraX { get; set; }
            public float CameraY { get; set; }
            public float CameraZ { get; set; }
            public TrackingState TrackingState { get; set; }

            public RawJointData(Joint joint, KinectSensor sensor)
            {
                CameraSpacePoint cameraSpacePoint = joint.Position;
                ColorSpacePoint colorSpacePoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(cameraSpacePoint);

                this.CameraX = cameraSpacePoint.X;
                this.CameraY = cameraSpacePoint.Y;
                this.CameraZ = cameraSpacePoint.Z;
                this.X = colorSpacePoint.X;
                this.Y = colorSpacePoint.Y;
                this.TrackingState = joint.TrackingState;
            }

            public static string GetCSVHeader(JointType jointType)
            {
                return $"{jointType}_X,{jointType}_Y,{jointType}_CameraX,{jointType}_CameraY,{jointType}_CameraZ,{jointType}_TrackingState";
            }

            public string ToCSV()
            {
                return $"{this.X},{this.Y},{this.CameraX},{this.CameraY},{this.CameraZ},{this.TrackingState}";
            }
        }

        private class RawBodyFrameData
        {
            public RawJointData HandTip;
            public RawJointData Hand;
            public RawJointData Wrist;
            public RawJointData Elbow;

            public RawBodyFrameData(Body body, KinectSensor sensor)
            {
                this.HandTip = new RawJointData(body.Joints[JointType.HandTipRight], sensor);
                this.Hand = new RawJointData(body.Joints[JointType.HandRight], sensor);
                this.Wrist = new RawJointData(body.Joints[JointType.WristRight], sensor);
                this.Elbow = new RawJointData(body.Joints[JointType.ElbowRight], sensor);
            }

            public static string GetCSVHeader()
            {
                return $"{RawJointData.GetCSVHeader(JointType.HandTipRight)},"
                    + $"{RawJointData.GetCSVHeader(JointType.HandRight)},"
                    + $"{RawJointData.GetCSVHeader(JointType.WristRight)},"
                    + $"{RawJointData.GetCSVHeader(JointType.ElbowRight)}";
            }

            public string ToCSV()
            {
                return $"{this.HandTip.ToCSV()},{this.Hand.ToCSV()},{this.Wrist.ToCSV()},{this.Elbow.ToCSV()}";
            }
        }

        private readonly KinectSensor sensor;
        private Brush brush;
        private readonly BrushColorCycler brushColorCycler = new BrushColorCycler();
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        private IList<RawBodyFrameData> rawBodyFrameDataPoints = new List<RawBodyFrameData>();

        public TestRunPaintingSession(KinectSensor sensor)
        {
            this.sensor = sensor;

            this.backgroundWorker.DoWork += (s, e) =>
                {
                    var arguments = e.Argument as IList<object>;
                    var backgroundWithPainting = arguments[0] as RenderTargetBitmap;
                    var background = arguments[1] as RenderTargetBitmap;
                    var parentDirectoryPath = arguments[2] as string;

                    var directoryPath = System.IO.Path.Combine(parentDirectoryPath, DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss"));

                    // Save the background with the painting
                    string backgroundWithPaintingFilePath = System.IO.Path.Combine(directoryPath, "painting.png");
                    Directory.CreateDirectory(directoryPath);
                    SaveRenderTargetBitmapAsPng(backgroundWithPainting, backgroundWithPaintingFilePath);

                    // Save just the background
                    string backgroundFilePath = System.IO.Path.Combine(directoryPath, "background.png");
                    SaveRenderTargetBitmapAsPng(background, backgroundFilePath);

                    // Save the raw body frame data points
                    string rawBodyFrameDataFilePath = System.IO.Path.Combine(directoryPath, "raw_body_frame_data.csv");
                    var handFrameDataPointStrings = this.rawBodyFrameDataPoints.Select(d => d.ToCSV());

                    using (var file = new StreamWriter(rawBodyFrameDataFilePath))
                    {
                        file.WriteLine(RawBodyFrameData.GetCSVHeader());
                        foreach (var dataPoint in handFrameDataPointStrings)
                        {
                            file.WriteLine(dataPoint);
                        }
                    }
                };
        }

        public void Paint(Body body, Canvas canvas)
        {
            // Cycle color every 50 points to ease debugging
            if ((this.rawBodyFrameDataPoints.Count % 50) == 0)
            {
                this.brush = this.brushColorCycler.Next();
                canvas.Children.Add(CreateDrawingLine());
            }

            // Paint on the canvas
            var hand = body.Joints[JointType.HandRight];
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                CameraSpacePoint handPosition = hand.Position;
                ColorSpacePoint handPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(handPosition);

                var x = handPoint.X;
                var y = handPoint.Y;

                if (!float.IsInfinity(x) && !float.IsInfinity(y))
                {
                    var trail = canvas.Children[canvas.Children.Count - 1] as Polyline;
                    trail.Points.Add(new Point { X = x, Y = y });
                }
            }

            // Log data points
            var frameData = new RawBodyFrameData(body, this.sensor);
            this.rawBodyFrameDataPoints.Add(frameData);
        }

        public void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath)
        {
            var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
            rtb.Render(background);
            rtb.Render(canvas);
            rtb.Freeze(); // necessary for the backgroundWorker to access it

            var rtb2 = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
            rtb2.Render(background);
            rtb2.Freeze(); // necessary for the backgroundWorker to access it
            this.backgroundWorker.RunWorkerAsync(new List<object> { rtb, rtb2, directoryPath});
        }

        public void ClearCanvas(Canvas canvas)
        {
            var elementCountToRemove = canvas.Children.Count - 1;
            canvas.Children.RemoveRange(1, elementCountToRemove);
        }

        private void SaveRenderTargetBitmapAsPng(RenderTargetBitmap rtb, string filePath)
        {
            BitmapEncoder pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var ms = new MemoryStream())
            {
                pngEncoder.Save(ms);
                File.WriteAllBytes(filePath, ms.ToArray());
            }
        }

        private Polyline CreateDrawingLine()
        {
            return new Polyline
            {
                Stroke = this.brush,
                StrokeThickness = 20,
                Effect = new BlurEffect
                {
                    Radius = 2
                }
            };
        }
    }
}
