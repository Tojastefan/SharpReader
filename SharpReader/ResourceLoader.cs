using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace SharpReader
{
    internal class ResourceLoader
    {
        //private static ResourceManager resourceManager = new ResourceManager("SharpReader.Properties.Strings", typeof(ResourceLoader).Assembly);
        private static readonly ResourceManager resourceManager = new ResourceManager("SharpReader.Resources.Strings", typeof(ResourceLoader).Assembly);

        public static string GetString(string key)
        {
            return resourceManager.GetString(key);
        }
    }
}
