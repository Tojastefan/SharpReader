using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SharpReader
{
    internal class ComicImages : Comic
    {
        public ComicImages(string path, string title):base(path, title)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path);

                foreach (string file in files)
                {
                    bool isImage = Regex.IsMatch(file, @"\.(jpg|jpeg|png|gif|bmp|tiff|tif)$", RegexOptions.IgnoreCase);
                    if (isImage)
                    {
                        images.Add(new Uri(file, Path.IsPathRooted(file) ? UriKind.Absolute : UriKind.Relative));
                    }
                }
                if (images.Count < 1)
                    throw new Exception("No images");
                cover = images[0];
            }
        }
        public ComicImages(string path, string title, string category) : base(path, title, category)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path);

                foreach (string file in files)
                {
                    bool isImage = Regex.IsMatch(file, @"\.(jpg|jpeg|png|gif|bmp|tiff|tif)$", RegexOptions.IgnoreCase);
                    if (isImage)
                    {
                        images.Add(new Uri(file, Path.IsPathRooted(file) ? UriKind.Absolute : UriKind.Relative));
                    }
                }
                if (images.Count < 1)
                    throw new Exception("No images");
                cover = images[0];
            }
        }
    }
}
