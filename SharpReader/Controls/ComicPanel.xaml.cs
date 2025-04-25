using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using Effects = System.Windows.Media.Effects;

namespace SharpReader.Controls
{
    public partial class ComicPanel : UserControl
    {
        private MainWindow parent;
        private Comic comic;
        public MainWindow.SelectionMode Variant
        {
            get { return (MainWindow.SelectionMode)GetValue(VariantProperty); }
            set
            {
                SetValue(VariantProperty, value);
                switch (Variant)
                {
                    case MainWindow.SelectionMode.GRID:
                        Title.Visibility = Visibility.Visible;
                        PanelList.Visibility = Visibility.Collapsed;
                        SettingsButton.Visibility = Visibility.Hidden;
                        ProgressContainer.Visibility = Visibility.Visible;
                        break;
                    case MainWindow.SelectionMode.LIST:
                        Title.Visibility = Visibility.Collapsed;
                        PanelList.Visibility = Visibility.Visible;
                        SettingsButton.Visibility = Visibility.Collapsed;
                        ProgressContainer.Visibility = Visibility.Collapsed;
                        break;
                }
            }
        }
        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register("Variant", typeof(MainWindow.SelectionMode), typeof(ComicPanel),
                new PropertyMetadata(MainWindow.SelectionMode.GRID));
        public Binding ColorBinding
        {
            set
            {
                Title.SetBinding(TextBlock.ForegroundProperty, value);
                TitleList.SetBinding(TextBlock.ForegroundProperty, value);
            }
        }
        public string ComicTitle
        {
            get { return (string)GetValue(ComicTitleProperty); }
            set { SetValue(ComicTitleProperty, value); }
        }
        public static readonly DependencyProperty ComicTitleProperty =
            DependencyProperty.Register("ComicTitle", typeof(string), typeof(ComicPanel), new PropertyMetadata("ComicTitle"));


        public ComicPanel(MainWindow parent, Comic comic)
        {
            InitializeComponent();
            this.parent = parent;
            this.comic = comic;
            CoverImage.Source = comic.getCoverImage();
            ComicTitle = comic.Title;
            ProgressBar.Value = comic.SavedPage <= 0 || comic.getImageCount() <= 0 ? 0 : (comic.SavedPage + 1) * 100 / comic.getImageCount();
            ProgressBarList.Value=ProgressBar.Value;
            ProgressLabel.Text = ProgressBar.Value < 100 ? $"{ProgressBar.Value}%" : "Finished";
            ProgressLabelList.Text = ProgressLabel.Text;
            SettingsButton.Visibility = Visibility.Hidden;
            SettingsButton.Click += (sender, e) => parent.comicSettings(sender, e, comic);
            SettingsButtonList.Click += (sender, e) => parent.comicSettings(sender, e, comic);
            Wrap.MouseEnter += (sender, e) =>
            {
                CoverImage.Width = CoverImage.Width + 5;
                Panel.Width = Panel.Width + 5;
                CoverImageBorder.Effect = new Effects.DropShadowEffect
                {
                    RenderingBias = Effects.RenderingBias.Quality,
                    Color = parent.isDarkMode ? Colors.Black : Colors.White,
                    BlurRadius = 15,
                    Opacity = 0.7,
                    ShadowDepth = 0,
                };
                if (Variant == MainWindow.SelectionMode.GRID)
                    SettingsButton.Visibility = Visibility.Visible;
            };
            Wrap.MouseLeave += (sender, e) =>
            {
                CoverImage.Width = CoverImage.Width - 5;
                Panel.Width = Panel.Width - 5;
                CoverImageBorder.Effect = null;
                SettingsButton.Visibility = Visibility.Hidden;
            };
            Panel.MouseDown += (sender, e) => parent.switchToReadingPanel(sender, e, comic);
        }
    }
}
