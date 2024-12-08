using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;

namespace SharpReader
{
    internal class Comic
    {
        private string path;
        private string title;
        List<Uri> images=new List<Uri>();
        private Uri cover=null;
        public Comic(string path,string title)
        {
            this.path = path;
            this.title = title;
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path);

                foreach (string file in files)
                {
                    images.Add(new Uri(file, Path.IsPathRooted(file) ? UriKind.Absolute : UriKind.Relative));
                }
                cover = images[0];
            }
        }
        public string getPath()
        {
            return path;
        }
        public string getTitle()
        {
            return title;
        }
        public List<Uri> getImages()
        {
            return images;
        }
        public Uri getCoverImage()
        {
            return cover;
        }
    }
}