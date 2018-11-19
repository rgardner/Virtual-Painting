using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace KinectDrawing
{
    class ImageSaverBackgroundWorker
    {
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        public ImageSaverBackgroundWorker()
        {
            this.backgroundWorker.DoWork += (s, e) =>
                {
                    var arguments = (IList<object>)e.Argument;
                    var image = (RenderTargetBitmap)arguments[0];
                    var directoryPath = (string)arguments[1];

                    string fileName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss") + ".png";
                    string fullPath = Path.Combine(directoryPath, fileName);
                    Directory.CreateDirectory(directoryPath);
                    SaveRenderTargetBitmapAsPng(image, fullPath);
                };
        }

        public void SaveImageAsync(RenderTargetBitmap rtb, string directoryPath)
        {
            this.backgroundWorker.RunWorkerAsync(new List<object> { rtb, directoryPath });
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
