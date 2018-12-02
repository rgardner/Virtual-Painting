using System;
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
        private readonly KinectSensor sensor;
        private readonly IPaintAlgorithm paintAlgorithm;
        private Brush brush;
        private readonly BrushColorCycler brushColorCycler = new BrushColorCycler();
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

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
                    string csvSerializedSensorDataFilePath = Path.Combine(directoryPath, "sensor_data.csv");

                    using (var file = new StreamWriter(csvSerializedSensorDataFilePath))
                    {
                        file.WriteLine(SensorBody.GetCsvHeader());
                        foreach (var sensorBodyFrame in this.sensorRecorder.SensorData.BodyFrames)
                        {
                            string[] csvSerializedBodies = sensorBodyFrame.Bodies.Select(b => b.SerializeToCsv()).ToArray();
                            file.WriteLine(string.Join(",", csvSerializedBodies));
                        }
                    }

                    string jsonSerializedSensorData = JsonConvert.SerializeObject(this.sensorRecorder.SensorData);
                    string jsonSerializedSensorDataFilePath = Path.Combine(directoryPath, "sensor_data.json");
                    File.WriteAllText(jsonSerializedSensorDataFilePath, jsonSerializedSensorData);
                };
        }

        public void Paint(SensorBody body, Brush brush, Canvas canvas, SensorBodyFrame bodyFrame)
        {
            // Cycle color every 50 points to ease debugging
            bool startNewSubSession = (this.sensorRecorder.SensorData.BodyFrames.Count % 50) == 0;
            if (startNewSubSession)
            {
                this.brush = this.brushColorCycler.Next();
            }

            this.paintAlgorithm.Paint(body, this.brush, canvas, startNewSubSession);

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
            if (canvas.Children.Count > 1)
            {
                var elementCountToRemove = canvas.Children.Count - 1;
                canvas.Children.RemoveRange(1, elementCountToRemove);
            }
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
