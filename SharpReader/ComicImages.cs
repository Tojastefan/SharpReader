using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SharpReader
{
    public class ComicImages : Comic
    {
        public ComicImages(string path, string title) : base(path, title)
        {
            this.ComicType = COMICTYPE.IMAGES;
            setup();
        }
        public ComicImages(string path, string title, string category, int savedPage) : base(path, title, category, savedPage)
        {
            this.ComicType = COMICTYPE.IMAGES;
            setup();
        }
        protected override void setup()
        {
            if (Directory.Exists(Path))
            {
                string[] files = Directory.GetFiles(Path);

                foreach (string file in files)
                {
                    bool isImage = Regex.IsMatch(file, @"\.(jpg|jpeg|png|gif|bmp|tiff|tif)$", RegexOptions.IgnoreCase);
                    if (isImage)
                    {
                        images.Add(new Uri(file, System.IO.Path.IsPathRooted(file) ? UriKind.Absolute : UriKind.Relative));
                    }
                }
                if (images.Count < 1)
                    throw new Exception("No images");
                cover = images[0];
            }
        }
    }
}
