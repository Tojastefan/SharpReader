using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Effects = System.Windows.Media.Effects;

namespace SharpReader.Controls
{
    public partial class ComicPanel : UserControl
    {
        //public string ImagePath
        //{
        //    get { return (string)GetValue(ImagePathProperty); }
        //    set { SetValue(ImagePathProperty, value); }
        //}
        //public static readonly DependencyProperty ImagePathProperty =
        //    DependencyProperty.Register("ImagePath", typeof(string), typeof(ComicPanel), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("TextColor", typeof(Brush), typeof(ComicPanel), new PropertyMetadata(Brushes.Black));

        public Brush Text
        {
            get { return (Brush)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public ComicPanel(MainWindow parent, Comic comic)
        {
            InitializeComponent();
            CoverImage.Source = comic.getCoverImage();
            Title.Text = comic.Title;
            ProgressBar.Value = comic.SavedPage <= 0 || comic.getImageCount() <= 0 ? 0 : (comic.SavedPage + 1) * 100 / comic.getImageCount();
            ProgressLabel.Text = ProgressBar.Value < 100 ? $"{ProgressBar.Value}%" : "Finished";
            SettingsButton.Visibility = Visibility.Hidden;
            SettingsButton.Click += (sender, e) => parent.comicSettings(sender, e, comic);
            Panel.MouseEnter += (sender, e) =>
            {
                CoverImage.Width = CoverImage.Width + 5;
                Panel.Width = Panel.Width + 5;
                CoverImage.Effect = new Effects.DropShadowEffect
                {
                    RenderingBias = Effects.RenderingBias.Quality,
                    Color = parent.isDarkMode ? Colors.Black : Colors.White,
                    BlurRadius = 15,
                    Opacity = 0.7,
                    ShadowDepth = 0,
                };
                SettingsButton.Visibility = Visibility.Visible;
            };
            Panel.MouseLeave += (sender, e) =>
            {
                CoverImage.Width = CoverImage.Width - 5;
                Panel.Width = Panel.Width - 5;
                CoverImage.Effect = null;
                SettingsButton.Visibility = Visibility.Hidden;
            };
            Panel.MouseDown += (sender, e) => parent.switchToReadingPanel(sender, e, comic);
        }
    }
}
