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
        public enum COMICTYPE
        {
            COMIC,
            IMAGES,
            PDF,
        }
        public string Path { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public COMICTYPE ComicType { get; set; }
        protected List<Uri> images=new List<Uri>();
        protected Uri cover=null;
        public Comic() { }
        public Comic(string path, string title, string category)
        {
            this.Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            this.Title = title;
            this.Category = category;
        }
        public Comic(string path, string title)
        {
            this.Path = path;
            this.Title = title;
            this.Category = "Other";
            this.ComicType = COMICTYPE.COMIC;
        }
        protected virtual void setup() { }
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
                return new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"\\resources\\placeholder.jpg"),UriKind.Relative));
            return new BitmapImage(cover);
        }
    }
}