using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoBooth
{
    class ImageSaver
    {
        /// <summary>
        /// Overlays the <paramref name="overlayImage"/> on top of the <paramref name="cameraImage"/> and saves the result to a file.
        /// </summary>
        /// <param name="cameraImage"></param>
        /// <param name="overlayImage"></param>
        internal static void SaveImage(BitmapSource cameraImage, BitmapSource overlayImage)
        {
            string myPicturesFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string parentFolderPath = Path.Combine(myPicturesFolderPath, "PhotoBooth");
            string fileName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss") + ".png";
            string filePath = Path.Combine(parentFolderPath, fileName);

            // Resize the overlay because it has different dimensions than the camera image.
            var overlayImageBitmap = BitmapSourceToBitmap(overlayImage);
            Bitmap resizedOverlayImage = ResizeImage(overlayImageBitmap, cameraImage.PixelWidth, cameraImage.PixelHeight);

            Image image = new Bitmap(cameraImage.PixelWidth, cameraImage.PixelHeight);
            using (var gr = Graphics.FromImage(image))
            {
                gr.DrawImage(BitmapSourceToBitmap(cameraImage), 0f, 0f);
                gr.DrawImage(resizedOverlayImage, 0f, 0f);
            }

            // best-effort: create parent folder if it doesn't exist
            Directory.CreateDirectory(parentFolderPath);
            image.Save(filePath, ImageFormat.Png);
        }

        private static Bitmap BitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            var bitmap = new Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight, PixelFormat.Format32bppPArgb);

            var bitmapData = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size),
                ImageLockMode.WriteOnly, bitmap.PixelFormat);

            bitmapSource.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// https://stackoverflow.com/a/24199315/4228400
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
