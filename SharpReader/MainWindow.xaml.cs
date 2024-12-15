using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using PdfiumViewer;
using Forms=System.Windows.Forms;
using System.Windows.Forms.Integration;
using static System.Net.WebRequestMethods;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Specialized;
using System.Net.Http.Headers;
using Spire.Pdf.AI;

namespace SharpReader
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public enum Mode
        {
            SELECTION,
            READING
        }
        public enum ReadingMode
        {
            SCROLL,
            PAGE
        }
        private readonly Brush darkText = Brushes.White;
        private readonly Brush lightText = Brushes.Black;
        private bool isDarkMode;
        private Mode currentMode;
        private ReadingMode currentReadingMode;
        private List<string> categories = new List<string>();
        private List<Comic> comics = new List<Comic>();
        private int currentImageIndex=0;
        private Image currentImage;
        private Comic currentComic;
        private Dictionary<string, Image> comicImages = new Dictionary<string, Image>();
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
            categories.Add("Favourite");
            categories.Add("Other");
            InitializeComponent();
            currentMode = Mode.SELECTION;
            toggleToScrollbar(null, null);
            try
            {
                var darkTheme = AppSettings.Default.darkTheme;
                CurrentTextColor = darkTheme ? darkText : lightText;
                isDarkMode = darkTheme;
            }
            catch (NullReferenceException e)
            {
                MessageBox.Show(e.ToString(), "Error when loading settings!");
                CurrentTextColor = darkText;
                AppSettings.Default.darkTheme = false;
                isDarkMode = false;
            }
            try
            {
                var savedComics = JsonSerializer.Deserialize<Dictionary<string, string>>(AppSettings.Default.savedComics);
                foreach (var kvp in savedComics)
                {
                    string path = kvp.Key;
                    int pos = kvp.Value.IndexOf(';');
                    string type = kvp.Value.Substring(0, pos);
                    string title = kvp.Value.Substring(pos + 1);
                    switch (type)
                    {
                        case "SharpReader.ComicImages":
                            comics.Add(new ComicImages(path, title));
                            break;
                        case "SharpReader.ComicPDF":
                            comics.Add(new ComicPDF(path, title));
                            break;
                    }
                }
            }
            catch (JsonException e) { }
            ChangeBackground_Click(null, null);
            foreach (var tb in FindVisualChildren<TextBlock>(SidebarPanel))
            {
                tb.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("CurrentTextColor")
                {
                    Source = this
                });
            }
            // Example comic
            if (comics.Count < 1)
            {
                ComicImages c = new ComicImages(".\\resources\\ActionComics", "Superman");
                // ComicPDF pdf = new ComicPDF("pdffile.pdf", "Name of pdf");
                // comics.Add(pdf);
                comics.Add(c);
            }
            switchToComicSelectionPanel();
        }

        private void LoadComics()
        {
            ComicsWrapPanel.Orientation = Orientation.Vertical;
            foreach (var item in categories)
            {
                WrapPanel wp = new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Orientation = Orientation.Vertical,
                };
                WrapPanel innerwp = new WrapPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Orientation = Orientation.Horizontal,
                };
                wp.Children.Add(getText(item, 36));
                List<Comic> filteredComics = comics.FindAll((e) =>
                {
                    if (e.getCategory() == item)
                        return true;
                    return false;
                });
                filteredComics.ForEach((e) =>
                {
                    innerwp.Children.Add(LoadComic(e));
                });
                wp.Children.Add(innerwp);
                ComicsWrapPanel.Children.Add(wp);
            }

            /*
            comics.ForEach(c =>
            {
                ComicsWrapPanel.Children.Add(LoadComic(c));
            });
            */
        }
        private StackPanel LoadComic(Comic comic)
        {
            string path = comic.getPath();
            string title = comic.getTitle();
            BitmapSource cover = comic.getCoverImage();
            int width = 150, height = 350;
            StackPanel panel = new StackPanel
            {
                Height = height,
                Width = width,
                Margin = new Thickness(20)
            };
            Image image = new Image
            {
                Source = cover,
                Width = width,
                MaxHeight = height
            };
            TextBlock textBlock = new TextBlock
            {
                Text = title,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            textBlock.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("CurrentTextColor")
            {
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
            panel.MouseDown += (sender, e) => switchToReadingPanel(sender, e, comic);
            panel.MouseEnter += (sender, e) =>
            {
                StackPanel obj = sender as StackPanel;
                Image imageInside = obj.Children[0] as Image;
                imageInside.Width = imageInside.Width + 5;
                obj.Width = obj.Width + 5;
                image.Effect = new DropShadowEffect
                {
                    RenderingBias = RenderingBias.Quality,
                    Color = isDarkMode ? Colors.Black : Colors.White,
                    BlurRadius = 15,
                    Opacity = 0.7,
                    ShadowDepth = 0,
                };
                button.Visibility = Visibility.Visible;
            };
            panel.MouseLeave += (sender, e) =>
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
        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            ChangeTextColor(isDarkMode);
            if (isDarkMode)
            {
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
            else
            {
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
            AppSettings.Default.darkTheme = isDarkMode;
            isDarkMode = !isDarkMode;
        }
        private TextBlock getText(string text, int size)
        {
            if (size < 1)
                size = 1;
            TextBlock tb = new TextBlock
            {
                Text = text,
                FontSize = size,
            };
            tb.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("CurrentTextColor")
            {
                Source = this
            });
            return tb;
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Dictionary<string, string> comicDictionary = new Dictionary<string, string>();
            foreach (Comic c in comics)
            {
                comicDictionary.Add(c.getPath(), c.GetType() + ";" + c.getTitle());
            }
            AppSettings.Default.savedComics = JsonSerializer.Serialize(comicDictionary);
            Console.WriteLine(AppSettings.Default.savedComics);
            AppSettings.Default.Save();
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
            currentMode = Mode.SELECTION;
            ComicsWrapPanel.Orientation = Orientation.Horizontal;
            MainScrollViewer.ScrollToTop();
            LoadComics();
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            ComicsWrapPanel.Children.Clear();
            HomeButton.IsEnabled = false;
            switchToComicSelectionPanel();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (currentMode == Mode.READING)
            {
                double lastPos = 0.0d;
                Console.WriteLine(getImageByIndex(0).Source);
                Console.WriteLine(e.Key);
                switch (e.Key)
                {
                    case Key.A:
                        if (currentReadingMode == ReadingMode.SCROLL)
                        {
                            for (int i = 0; i < ComicsWrapPanel.Children.Count; ++i)
                            {
                                Point a = ComicsWrapPanel.Children[i].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                                if (a.Y >= 0)
                                {
                                    double pos = MainScrollViewer.VerticalOffset + lastPos;
                                    MainScrollViewer.ScrollToVerticalOffset(pos > 0d ? pos : 0d);
                                    break;
                                }
                                lastPos = a.Y;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{currentImageIndex}");
                            if (currentImageIndex>0)
                            {
                                Image temp = currentImage;
                                currentImageIndex -= 1;
                                Image newImage = getImageByIndex(currentImageIndex);
                                temp.Source = newImage.Source;
                            }
                        }
                        break;
                    case Key.D:
                        if (currentReadingMode == ReadingMode.SCROLL)
                        {
                            for (int i = 0; i < ComicsWrapPanel.Children.Count; ++i)
                            {
                                Point a = ComicsWrapPanel.Children[i].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                                if (a.Y > 0)
                                {
                                    Console.WriteLine($"{MainScrollViewer.VerticalOffset} - {a.Y}");
                                    MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + a.Y);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if(currentComic.getImageCount() > currentImageIndex + 1)
                            {
                                Image temp = currentImage;
                                currentImageIndex += 1;
                                Image newImage = getImageByIndex(currentImageIndex);
                                temp.Source = newImage.Source;
                            }
                        }
                        break;
                    case Key.T:
                        MainScrollViewer.ScrollToTop();
                        break;
                    case Key.B:
                        MainScrollViewer.ScrollToEnd();
                        break;
                }
            }
        }
        private void switchToReadingPanel(object sender, RoutedEventArgs e, Comic comic)
        {
            currentMode = Mode.READING;
            currentComic = comic;
            ComicsWrapPanel.Children.Clear();
            ComicsWrapPanel.Orientation = Orientation.Vertical;
            if (currentComic is ComicPDF comicPDF)
            {
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
            else
            {
                string dirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Comic");
                if (Directory.Exists(dirPath))
                {
                    if (currentReadingMode == ReadingMode.SCROLL)
                    {
                        List<Uri> files = currentComic.getImages();
                        foreach (Uri file in files)
                        {
                            Image img;
                            string path = file.ToString();
                            if (!comicImages.ContainsKey(path))
                            {
                                img = new Image
                                {
                                    Source = new BitmapImage(file),
                                    Width = MainScrollViewer.ActualWidth,
                                    MaxHeight = 700,
                                };
                                comicImages.Add(path, img);
                            }
                            else
                            {
                                img = comicImages[path];
                            }
                            ComicsWrapPanel.Children.Add(img);
                        }
                    }
                    else
                    {
                        currentImage = new Image
                        {
                            Source = getImageByIndex(currentImageIndex).Source,
                            Width = MainScrollViewer.ActualWidth,
                            MaxHeight = 700,
                        };
                        ComicsWrapPanel.Children.Add(currentImage);
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
        private Image getImageByIndex(int index)
        {
            if (index < 0)
                index = 0;
            List<Uri> files = currentComic.getImages();
            Image img = null;
            Uri file = files[currentImageIndex];
            string path = file.ToString();
            if (!comicImages.ContainsKey(path))
            {
                img = new Image
                {
                    Source = new BitmapImage(file),
                    Width = MainScrollViewer.ActualWidth,
                    MaxHeight = 700,
                };
                comicImages.Add(path, img);
            }
            else
            {
                img = comicImages[path];
            }
            return img;
        }
        private void NewComic(object sender, RoutedEventArgs e)
        {
            using (Forms.FolderBrowserDialog fdb = new Forms.FolderBrowserDialog())
            {
                fdb.Description = "Select a comic folder:";
                fdb.ShowNewFolderButton = false;
                if (fdb.ShowDialog() == Forms.DialogResult.OK)
                {
                    ComicImages c = new ComicImages(fdb.SelectedPath, Path.GetFileName(fdb.SelectedPath));
                    comics.Add(c);
                    ComicsWrapPanel.Children.Clear();
                    LoadComics();
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
                    ComicPDF c = new ComicPDF(ofd.FileName, Path.GetFileName(Regex.Replace(ofd.FileName, @"\.[^.\\]+$", "")));
                    comics.Add(c);
                    ComicsWrapPanel.Children.Clear();
                    LoadComics();
                }
                else
                {
                    MessageBox.Show("Error when adding comic!", "Error");
                }
            }
        }

        private void toggleToScrollbar(object sender, RoutedEventArgs e)
        {
            if (currentMode == Mode.READING)
                return;
            currentReadingMode = ReadingMode.SCROLL;
            PageButton.IsEnabled = true;
            ScrollbarButton.IsEnabled = false;
        }
        private void toggleToPage(object sender, RoutedEventArgs e)
        {
            if (currentMode == Mode.READING)
                return;
            currentReadingMode = ReadingMode.PAGE;
            PageButton.IsEnabled = false;
            ScrollbarButton.IsEnabled = true;
        }
    }
}