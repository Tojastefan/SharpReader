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
        private string title;
        private string path;
        private List<Uri> images;
        public Comic(string path,string title)
        {
            this.title = title;
            this.path = path;
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path);
                images=new List<Uri>();

                foreach (string file in files)
                {
                    images.Add(new Uri(file, Path.IsPathRooted(file) ? UriKind.Absolute : UriKind.Relative));
                }
            }
        }
        public string getTitle()
        {
            return title;
        }
        public List<Uri> getImages()
        {
            return images;
        }
        public Uri getFirstImage()
        {
            return images[0];
        }
    }
}