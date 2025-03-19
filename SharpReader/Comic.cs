using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

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
        public int SavedPage { get; set; }
        public COMICTYPE ComicType { get; set; }
        protected List<Uri> images=new List<Uri>();
        public Uri cover=null;
        public Comic() { }
        public Comic(string path, string title, string category,int savedPage)
        {
            this.Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            this.Title = title;
            this.Category = category;
            this.SavedPage = savedPage;
            if (path.Contains("Action"))
            {
                Console.WriteLine("------------Konstruktor 1------------");
                Console.WriteLine(this.Path);
                Console.WriteLine("------------Konstruktor 1------------");
            }
        }
        public Comic(string path, string title)
        {
            this.Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            this.Title = title;
            this.Category = "Other";
            this.ComicType = COMICTYPE.COMIC;
            this.SavedPage = 0;
            if (path.Contains("Action"))
            {
                Console.WriteLine("------------Konstruktor 2------------");
                Console.WriteLine(this.Path);
                Console.WriteLine("------------Konstruktor 2------------");
            }
        }
        protected virtual void setup() { }
        public virtual BitmapSource pageToImage(int page)
        {
            return null;
        }
        public List<Uri> getImages()
        {
            return images;
        }
        public virtual int getImageCount()
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
