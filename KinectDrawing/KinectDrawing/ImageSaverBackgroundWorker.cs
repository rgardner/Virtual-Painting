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
                    var arguments = e.Argument as IList<object>;
                    var image = arguments[0] as RenderTargetBitmap;
                    var directoryPath = arguments[1] as string;

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
