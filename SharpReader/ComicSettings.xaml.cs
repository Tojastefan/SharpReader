using System.Windows;

namespace SharpReader
{
    /// <summary>
    /// Logika interakcji dla klasy ComicSettings.xaml
    /// </summary>
    public partial class ComicSettings : Window
    {
        public ComicSettings()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
