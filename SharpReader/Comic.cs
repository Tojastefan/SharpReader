using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Text.RegularExpressions;

namespace SharpReader
{
    internal class Comic
    {
        protected string path;
        protected string title;
        protected List<Uri> images=new List<Uri>();
        protected Uri cover=null;
        public Comic(string path,string title)
        {
            this.path = path;
            this.title = title;
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
        public virtual BitmapSource getCoverImage()
        {
            if (cover == null)
                return new BitmapImage(new Uri("\\resources\\placeholder.jpg",UriKind.Relative));
            return new BitmapImage(cover);
        }
    }
}