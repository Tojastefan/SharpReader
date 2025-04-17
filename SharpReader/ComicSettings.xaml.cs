using System.Windows;
using static SharpReader.MainWindow;
using System.Windows.Input;

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
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.Escape:
                    Cancel_Click(null, null);
                    break;
            }
        }
    }
}
