using System;
using System.IO;
using System.Windows.Forms;
using PdfiumViewer;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Drawing;

namespace SharpReader
{
    internal class ComicPDF : Comic
    {
        private PdfDocument pdfDocument;
        private PdfViewer pdfViewer;
        public PdfViewer Viewer
        {
            get { return pdfViewer; }
        }
        public ComicPDF(string path, string title) : base(path, title)
        {
            this.ComicType = "PDF";
            setup();
        }
        public ComicPDF(string path, string title, string category) : base(path, title, category)
        {
            this.ComicType = "PDF";
            setup();
        }
        public override void setup()
        {
            pdfViewer = new PdfViewer
            {
                Dock = DockStyle.Fill,
                ShowToolbar = false,
            };
            pdfDocument = PdfDocument.Load(Path);
            pdfViewer.Document = pdfDocument;
        }
        public override BitmapSource getCoverImage()
        {
            var dpi = 20;

            using (var image = pdfDocument.Render(0, dpi, dpi, PdfRenderFlags.CorrectFromDpi))
            {
                return ConvertToBitmapSource(image);
            }
        }
        private BitmapSource ConvertToBitmapSource(Image image)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Save the image to the memory stream in PNG format
                image.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;

                // Create a BitmapImage from the memory stream
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Load the image into memory
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Freeze to make it cross-thread accessible

                return bitmapImage;
            }
        }
    }
}
