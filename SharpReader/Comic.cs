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
        protected string category;
        protected List<Uri> images=new List<Uri>();
        protected Uri cover=null;
        public Comic(string path,string title,string category)
        {
            this.path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,path);
            this.title = title;
            this.category = category;
        }
        public Comic(string path, string title)
        {
            this.path = path;
            this.title = title;
            this.category = "Other";
        }
        public string getPath()
        {
            return path;
        }
        public string getTitle()
        {
            return title;
        }
        public string getCategory()
        {
            return category;
        }
        public List<Uri> getImages()
        {
            return images;
        }
        public int getImageCount()
        {
            return images.Count;
        }
        public virtual BitmapSource getCoverImage()
        {
            if (cover == null)
                return new BitmapImage(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"\\resources\\placeholder.jpg"),UriKind.Relative));
            return new BitmapImage(cover);
        }
    }
}