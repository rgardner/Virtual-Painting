﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KinectDrawing.PaintAlgorithm;
using KinectRecorder;
using Microsoft.Kinect;
using Newtonsoft.Json;

namespace KinectDrawing.PaintingSession
{
    class TestModePaintingSession : IPaintingSession
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
            public RawJointData HandTip { get; set; }
            public RawJointData Hand { get; set; }
            public RawJointData Wrist { get; set; }
            public RawJointData Elbow { get; set; }

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
        private readonly IPaintAlgorithm paintAlgorithm;
        private Brush brush;
        private readonly BrushColorCycler brushColorCycler = new BrushColorCycler();
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        private IList<RawBodyFrameData> rawBodyFrameDataPoints = new List<RawBodyFrameData>();
        private readonly SensorRecorder sensorRecorder = new SensorRecorder();

        public TestModePaintingSession(KinectSensor sensor, IPaintAlgorithm paintAlgorithm)
        {
            this.sensor = sensor;
            this.paintAlgorithm = paintAlgorithm;

            this.backgroundWorker.DoWork += (s, e) =>
                {
                    var arguments = (IList<object>)e.Argument;
                    var backgroundWithPainting = (RenderTargetBitmap)arguments[0];
                    var background = (RenderTargetBitmap)arguments[1];
                    var parentDirectoryPath = (string)arguments[2];

                    var directoryPath = Path.Combine(parentDirectoryPath, DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss"));

                    // Save the background with the painting
                    string backgroundWithPaintingFilePath = Path.Combine(directoryPath, "painting.png");
                    Directory.CreateDirectory(directoryPath);
                    SaveRenderTargetBitmapAsPng(backgroundWithPainting, backgroundWithPaintingFilePath);

                    // Save just the background
                    string backgroundFilePath = Path.Combine(directoryPath, "background.png");
                    SaveRenderTargetBitmapAsPng(background, backgroundFilePath);

                    // Save the raw body frame data points
                    string rawBodyFrameDataFilePath = Path.Combine(directoryPath, "raw_body_frame_data.csv");
                    var handFrameDataPointStrings = this.rawBodyFrameDataPoints.Select(d => d.ToCSV());

                    using (var file = new StreamWriter(rawBodyFrameDataFilePath))
                    {
                        file.WriteLine(RawBodyFrameData.GetCSVHeader());
                        foreach (var dataPoint in handFrameDataPointStrings)
                        {
                            file.WriteLine(dataPoint);
                        }
                    }

                    string serializedSensorData = JsonConvert.SerializeObject(this.sensorRecorder.SensorData);
                    string filePath = Path.Combine(directoryPath, "sensor_data.json");
                    File.WriteAllText(filePath, serializedSensorData);
                };
        }

        public void Paint(Body body, Brush brush, Canvas canvas, BodyFrame bodyFrame)
        {
            // Cycle color every 50 points to ease debugging
            bool startNewSubSession = (this.rawBodyFrameDataPoints.Count % 50) == 0;
            if (startNewSubSession)
            {
                this.brush = this.brushColorCycler.Next();
            }

            this.paintAlgorithm.Paint(body, this.brush, canvas, startNewSubSession);

            // Log data points
            var frameData = new RawBodyFrameData(body, this.sensor);
            this.rawBodyFrameDataPoints.Add(frameData);
            this.sensorRecorder.LogBodyFrame(bodyFrame);
        }

        public void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath, string backgroundDirectoryPath)
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
            Debug.Assert(canvas.Children.Count > 1);
            var elementCountToRemove = canvas.Children.Count - 1;
            canvas.Children.RemoveRange(1, elementCountToRemove);
        }

        private static void SaveRenderTargetBitmapAsPng(RenderTargetBitmap rtb, string filePath)
        {
            BitmapEncoder pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var ms = new MemoryStream())
            {
                pngEncoder.Save(ms);
                File.WriteAllBytes(filePath, ms.ToArray());
            }
        }
    }
}
