﻿using System.IO;
using System.Windows.Forms;
using PdfiumViewer;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.Drawing;

namespace SharpReader
{
    public class ComicPDF : Comic
    {
        private PdfDocument pdfDocument;
        private PdfViewer pdfViewer;
        public PdfViewer Viewer
        {
            get { return pdfViewer; }
        }
        public ComicPDF(string path, string title) : base(path, title)
        {
            this.ComicType = COMICTYPE.PDF;
            setup();
        }
        public ComicPDF(string path, string title, string category) : base(path, title, category, 0)
        {
            this.ComicType = COMICTYPE.PDF;
            setup();
        }
        public ComicPDF(string path, string title, string category, int savedPage) : base(path, title, category, savedPage)
        {
            this.ComicType = COMICTYPE.PDF;
            setup();
        }
        protected override void setup()
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
        public override int getImageCount()
        {
            return pdfDocument.PageCount;
        }
        public override BitmapSource pageToImage(int page)
        {
            var dpi = 80;

            using (var image = pdfDocument.Render(page, dpi, dpi, PdfRenderFlags.CorrectFromDpi))
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
