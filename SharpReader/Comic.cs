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
        List<Image> images;
        public Comic(string path)
        {
            this.path = path;
        }
    }
}