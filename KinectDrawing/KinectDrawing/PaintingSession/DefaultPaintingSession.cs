using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KinectDrawing.PaintAlgorithm;
using KinectDrawing.Properties;
using Microsoft.Kinect;

namespace KinectDrawing.PaintingSession
{
    class DefaultPaintingSession : IPaintingSession
    {
        private readonly KinectSensor sensor;
        private readonly IPaintAlgorithm paintingAlgorithm;
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        public DefaultPaintingSession(KinectSensor sensor, IPaintAlgorithm paintingAlgorithm)
        {
            this.sensor = sensor;
            this.paintingAlgorithm = paintingAlgorithm;

            this.backgroundWorker.DoWork += (s, e) =>
                {
                    var arguments = (IList<object>)e.Argument;
                    var rtb = (RenderTargetBitmap)arguments[0];
                    var width = (int)arguments[1];
                    var height = (int)arguments[2];
                    var directoryPath = (string)arguments[3];

                    var image = new Bitmap(width, height);
                    Bitmap painting = RenderTargetBitmapToBitmap(rtb);
                    var overlay = new Bitmap(Resources.SavedImageFrame, width, height);
                    using (var gr = Graphics.FromImage(image))
                    {
                        gr.DrawImage(painting, new System.Drawing.Point(0, 0));
                        gr.DrawImage(overlay, new System.Drawing.Point(0, 0));
                    }

                    string fileName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss") + ".png";
                    string fullPath = Path.Combine(directoryPath, fileName);
                    Directory.CreateDirectory(directoryPath);
                    image.Save(fullPath, ImageFormat.Png);
                };
        }

        public void Paint(Body body, System.Windows.Media.Brush brush, Canvas canvas, bool startNewSubSession)
        {
            this.paintingAlgorithm.Paint(body, brush, canvas, startNewSubSession);
        }

        public void SavePainting(System.Windows.Controls.Image background, Canvas canvas, int width, int height, string directoryPath)
        {
            var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
            rtb.Render(background);
            rtb.Render(canvas);
            rtb.Freeze();
            this.backgroundWorker.RunWorkerAsync(new List<object> { rtb, width, height, directoryPath });
        }

        public void ClearCanvas(Canvas canvas)
        {
            Debug.Assert(canvas.Children.Count > 1);
            var elementCountToRemove = canvas.Children.Count - 1;
            canvas.Children.RemoveRange(1, elementCountToRemove);
        }

        private static Bitmap RenderTargetBitmapToBitmap(RenderTargetBitmap rtb)
        {
            var bitmap = new Bitmap(rtb.PixelWidth, rtb.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            var bitmapData = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size),
            ImageLockMode.WriteOnly, bitmap.PixelFormat);

            rtb.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }
    }
}
