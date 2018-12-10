using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VirtualPainting.PaintAlgorithm;
using VirtualPainting.Properties;
using KinectRecorder;

namespace VirtualPainting.PaintingSession
{
    class DefaultPaintingSession : IPaintingSession
    {
        private readonly IPaintAlgorithm paintingAlgorithm;
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        public DefaultPaintingSession(IPaintAlgorithm paintingAlgorithm)
        {
            this.paintingAlgorithm = paintingAlgorithm;

            this.backgroundWorker.DoWork += (s, e) =>
                {
                    var arguments = (IList<object>)e.Argument;
                    var rtb = (RenderTargetBitmap)arguments[0];
                    var width = (int)arguments[1];
                    var height = (int)arguments[2];
                    var directoryPath = (string)arguments[3];
                    var backgroundRtb = (RenderTargetBitmap)arguments[4];
                    var backgroundDirectoryPath = (string)arguments[5];

                    var image = new Bitmap(width, height);
                    Bitmap painting = RenderTargetBitmapToBitmap(rtb);
                    var overlay = new Bitmap(Resources.SavedImageFrame, width, height);
                    using (var gr = Graphics.FromImage(image))
                    {
                        gr.DrawImage(painting, new System.Drawing.Point(0, 0));
                        gr.DrawImage(overlay, new System.Drawing.Point(0, 0));
                    }

                    string currentTime = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");
                    string fullPath = Path.Combine(directoryPath, currentTime + ".png");
                    Directory.CreateDirectory(directoryPath);
                    image.Save(fullPath, ImageFormat.Png);

                    string backgroundFilePath = Path.Combine(backgroundDirectoryPath, currentTime + "_original.png");
                    Directory.CreateDirectory(backgroundDirectoryPath);
                    SaveRenderTargetBitmapAsPng(backgroundRtb, backgroundFilePath);
                };
        }

        public void Paint(SensorBody body, System.Windows.Media.Brush brush, Canvas canvas, SensorBodyFrame bodyFrame)
        {
            this.paintingAlgorithm.Paint(body, brush, canvas);
        }

        public void SavePainting(System.Windows.Controls.Image background, Canvas canvas, int width, int height,
            string directoryPath, string backgroundDirectoryPath)
        {
            var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
            rtb.Render(background);
            rtb.Render(canvas);
            rtb.Freeze();

            var backgroundRtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
            backgroundRtb.Render(background);
            backgroundRtb.Freeze();

            this.backgroundWorker.RunWorkerAsync(new List<object> { rtb, width, height, directoryPath, backgroundRtb, backgroundDirectoryPath });
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

        public static void SaveRenderTargetBitmapAsPng(RenderTargetBitmap rtb, string filePath)
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
