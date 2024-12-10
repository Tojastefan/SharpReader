using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using PdfiumViewer;
using Forms=System.Windows.Forms;
using System.Windows.Forms.Integration;
using static System.Net.WebRequestMethods;
using System.Text.RegularExpressions;

namespace SharpReader
{
    public partial class MainWindow : Window , INotifyPropertyChanged
    {
        private readonly Brush darkText = Brushes.White;
        private readonly Brush lightText = Brushes.Black;
        private bool isDarkMode = true;
        private List<Comic> comics = new List<Comic>();
        private Brush currentTextColor;
        public Brush CurrentTextColor
        {
            get => currentTextColor;
            set
            {
                if (currentTextColor != value)
                {
                    currentTextColor = value;
                    OnPropertyChanged(nameof(CurrentTextColor));
                }
            }
        }
        Color darkSidebar = (Color)ColorConverter.ConvertFromString("#3c3c3c");
        Color lightSidebar = (Color)ColorConverter.ConvertFromString("#F5F5F5");

        public MainWindow()
        {
            CurrentTextColor = darkText;
            InitializeComponent();
            foreach (var tb in FindVisualChildren<TextBlock>(SidebarPanel))
            {
                tb.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("CurrentTextColor")
                {
                    Source = this
                });
            }
            // Example comic
            ComicImages c = new ComicImages(".\\resources\\ActionComics", "Superman");
            // ComicPDF pdf = new ComicPDF("pdffile.pdf", "Name of pdf");
            // comics.Add(pdf);
            comics.Add(c);
            switchToComicSelectionPanel();
        }

        private void LoadComics(){
            comics.ForEach(c =>
            {
                ComicsWrapPanel.Children.Add(LoadComic(c));
            });
        }
        private StackPanel LoadComic(Comic comic)
        {
            string path=comic.getPath();
            string title=comic.getTitle();
            BitmapSource cover = comic.getCoverImage();
            int width = 150, height = 350;
            StackPanel panel = new StackPanel
            {
                Height = height,
                Width = width,
                Margin = new Thickness(20)
            };
            Image image = new Image{
                Source = cover,
                Width = width,
                MaxHeight = height
            };
            TextBlock textBlock = new TextBlock{
                Text = title,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            textBlock.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("CurrentTextColor"){
                Source = this
            });
            Button button = new Button
            {
                Content = "Settings",
                Visibility = Visibility.Hidden,
            };
            panel.Children.Add(image);
            panel.Children.Add(textBlock);
            panel.Children.Add(button);
            panel.MouseDown += (sender, e) => switchToReadingPanel(sender,e,comic);
            panel.MouseEnter += (sender, e) =>
            {
                StackPanel obj = sender as StackPanel;
                Image imageInside= obj.Children[0] as Image;
                imageInside.Width = imageInside.Width + 5;
                obj.Width = obj.Width + 5;
                image.Effect = new DropShadowEffect
                {
                    RenderingBias = RenderingBias.Quality,
                    Color = isDarkMode ? Colors.White : Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.7,
                    ShadowDepth=0,
                };
                button.Visibility = Visibility.Visible;
            };
            panel.MouseLeave+= (sender, e) =>
            {
                StackPanel obj = sender as StackPanel;
                Image imageInside = obj.Children[0] as Image;
                imageInside.Width = imageInside.Width - 5;
                obj.Width = obj.Width - 5;
                image.Effect = null;
                button.Visibility = Visibility.Hidden;
            };

            return panel;
        }
        private void ChangeBackground_Click(object sender, RoutedEventArgs e){
            if(isDarkMode)
            {
                ChangeTextColor(false);
                MainGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
                MainDockPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3"));
                MainMenu.Background = new SolidColorBrush(lightSidebar);
                SidebarPanel.Background = new SolidColorBrush(lightSidebar);

                // Zmiana koloru tekstu i tła przycisków na jasny motyw
                foreach (var child in MainDockPanel.Children)
                {
                    if (child is Button button)
                    {
                        button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3"));
                        button.Foreground = Brushes.Black;
                    }
                    else if (child is TextBlock textBlock)
                    {
                        textBlock.Foreground = currentTextColor;
                    }
                }

                // Zmiana koloru menu na jasny
                foreach (var item in MainMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3"));
                        menuItem.Foreground = currentTextColor;
                    }
                }
            }
            else
            {
                ChangeTextColor(true);
                // Zmiana na ciemny motyw
                MainGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232323"));
                MainDockPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c3c3c"));
                MainMenu.Background = new SolidColorBrush(darkSidebar);
                SidebarPanel.Background = new SolidColorBrush(darkSidebar);

                // Zmiana koloru tekstu i tła przycisków na ciemny motyw
                foreach (var child in MainDockPanel.Children)
                {
                    if (child is Button button)
                    {
                        button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c3c3c"));
                        button.Foreground = currentTextColor;
                    }
                    else if (child is TextBlock textBlock)
                    {
                        textBlock.Foreground = currentTextColor;
                    }
                }

                // Zmiana koloru menu na ciemny
                foreach (var item in MainMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c3c3c"));
                        menuItem.Foreground = currentTextColor;
                    }
                }
            }
            isDarkMode = !isDarkMode;
        }

        private void GridLayout_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ListLayout_Click(object sender, RoutedEventArgs e)
        {

        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                // Recursively find children of the child
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }
        private void MyButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Button clicked!");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Example method to change the text color
        private void ChangeTextColor(bool useDarkText)
        {
            CurrentTextColor = useDarkText ? darkText : lightText;
        }
        private void switchToComicSelectionPanel()
        {
            ComicsWrapPanel.Orientation = Orientation.Horizontal;
            LoadComics();
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            ComicsWrapPanel.Children.Clear();
            HomeButton.IsEnabled = false;
            switchToComicSelectionPanel();
        }
        private void switchToReadingPanel(object sender,RoutedEventArgs e,Comic comic )
        {
            ComicsWrapPanel.Children.Clear();
            ComicsWrapPanel.Orientation = Orientation.Vertical;
            if (comic is ComicPDF comicPDF){
                var host = new WindowsFormsHost
                {
                    Child = comicPDF.Viewer,
                    Width = MainScrollViewer.ActualWidth,
                    Height = MainScrollViewer.ActualHeight,
                };
                MainScrollViewer.SizeChanged += (s, args) =>
                {
                    host.Width = MainScrollViewer.ActualWidth;
                    host.Height = MainScrollViewer.ActualHeight;
                };
                ComicsWrapPanel.Children.Clear();
                ComicsWrapPanel.Children.Add(host);
            }
            else {
                List<Uri> files = comic.getImages();
                string dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Comic");
                if (Directory.Exists(dirPath))
                {
                    foreach (Uri file in files)
                    {
                        Image firstimage = new Image
                        {
                            Source = new BitmapImage(file),
                            Width = MainScrollViewer.ActualWidth,
                            MaxHeight = 700,
                        };
                        ComicsWrapPanel.Children.Add(firstimage);
                    }
                }
                else
                {
                    Console.WriteLine("The directory does not exist.");
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    Console.WriteLine($"Base Directory: {basePath}");
                }
            }
            HomeButton.IsEnabled = true;
        }
        private void NewComic(object sender, RoutedEventArgs e)
        {
            using (Forms.FolderBrowserDialog fdb = new Forms.FolderBrowserDialog())
            {
                fdb.Description = "Select a comic folder:";
                fdb.ShowNewFolderButton = false;
                if (fdb.ShowDialog() == Forms.DialogResult.OK)
                {
                    Comic c = new Comic(fdb.SelectedPath, Path.GetFileName(fdb.SelectedPath));
                    comics.Add(c);
                    ComicsWrapPanel.Children.Add(LoadComic(c));
                }
                else
                {
                    MessageBox.Show("Error when adding comic!", "Error");
                }
            }
        }
        private void NewComicPDF(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog ofd = new Forms.OpenFileDialog())
            {
                ofd.Filter = "PDF Files (*.pdf)|*.pdf|CBZ Files (*.cbz)|*.cbz";
                if (ofd.ShowDialog() == Forms.DialogResult.OK)
                {
                    ComicPDF c = new ComicPDF(ofd.FileName,Path.GetFileName(Regex.Replace(ofd.FileName,@"\.[^.\\]+$","")));
                    comics.Add(c);
                    ComicsWrapPanel.Children.Add(LoadComic(c));
                }
                else
                {
                    MessageBox.Show("Error when adding comic!", "Error");
                }
            }
        }
    }
}
