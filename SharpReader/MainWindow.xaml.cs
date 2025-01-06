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
using Forms=System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Text.RegularExpressions;
using System.Text.Json;
using static SharpReader.Comic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Drawing.Imaging;

namespace SharpReader
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public enum Mode
        {
            SELECTION,
            READING,
        }
        public enum SelectionMode
        {
            GRID,
            LIST,
        }
        public enum ReadingMode
        {
            SCROLL,
            PAGE,
        }
        private readonly Brush darkText = Brushes.White;
        private readonly Brush lightText = Brushes.Black;
        private bool isDarkMode;
        private Mode currentMode;
        private ReadingMode currentReadingMode;
        private SelectionMode currentSelectionMode;
        private List<string> categories;
        private List<Comic> comics = new List<Comic>();
        private int currentImageIndex = 0;
        private Image currentImage;
        private Comic currentComic;
        private Dictionary<string, Image> comicImages = new Dictionary<string, Image>();
        private Brush currentTextColor;
        private int brightness = 0;
        private double currentZoom = 1.0;  // Początkowy poziom zoomu
        private const double zoomStep = 0.2;  // Krok zoomu
        private Point lastMousePosition;         // Przechowywanie ostatniej pozycji myszy
        private bool isMouseDown = false;        // Flaga, czy przycisk myszy jest wciśnięty
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
            InitializeComponent();
            currentMode = Mode.SELECTION;
            currentSelectionMode = SelectionMode.GRID;
            GridButton.IsEnabled = false;
            toggleToScrollbar(null, null);
            try
            {
                categories = JsonSerializer.Deserialize<List<string>>(AppSettings.Default.categories);
                if (categories.Count < 1)
                    throw new Exception("No categories");
            }
            catch (Exception ex)
            {
                categories = new List<string> { "Favourite", "Other" };
            }
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
                var savedComics = JsonSerializer.Deserialize<Dictionary<string, Comic>>(AppSettings.Default.savedComics);
                foreach (var kvp in savedComics)
                {
                    Comic c = kvp.Value;
                    if (!categories.Contains(c.Category))
                    {
                        c.Category = "Other";
                    }
                    switch (c.ComicType)
                    {
                        case COMICTYPE.IMAGES:
                            comics.Add(new ComicImages(kvp.Key, c.Title, c.Category, c.SavedPage));
                            break;
                        case COMICTYPE.PDF:
                            comics.Add(new ComicPDF(kvp.Key, c.Title, c.Category));
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
                    Orientation = currentSelectionMode == SelectionMode.GRID ? Orientation.Horizontal : Orientation.Vertical,
                };
                wp.Children.Add(getText(item, 36));
                List<Comic> filteredComics = comics.FindAll((e) => e.Category == item ? true : false);
                filteredComics.ForEach((e) => innerwp.Children.Add(LoadComic(e)));
                wp.Children.Add(innerwp);
                ComicsWrapPanel.Children.Add(wp);
            }
        }
        private StackPanel LoadComic(Comic comic)
        {
            string path = comic.Path;
            string title = comic.Title;
            BitmapSource cover = comic.getCoverImage();
            int width = 150, height = 400;
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
            button.Click += (sender, e) => comicSettings(sender, e, comic);
            panel.Children.Add(image);
            panel.Children.Add(textBlock);
            if (comic.ComicType != COMICTYPE.PDF)
            {
                Grid progressContainer = new Grid
                {
                    Width = width,
                    Height = 25,
                };
                ProgressBar progressBar = new ProgressBar
                {
                    Width = width,
                    Height = 25,
                    Minimum = 0,
                    Maximum = 100,
                    Value = comic.SavedPage  * 100 / (comic.getImageCount() - 1),
                    Padding = new Thickness(0, 5, 0, 0),
                };
                TextBlock percentText = new TextBlock
                {
                    Text = progressBar.Value < 100 ? $"{progressBar.Value}%" : "Finished",
                    //Text = $"{comic.SavedPage}, {comic.getImageCount()}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    Foreground = Brushes.Black,
                };
                progressContainer.Children.Add(progressBar);
                progressContainer.Children.Add(percentText);
                panel.Children.Add(progressContainer);
            }
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
                ComicsWrapPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232323"));
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
                MainGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")); //#232323 -> #E0E0E0
                ComicsWrapPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
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
        private void GridLayout_Click(object sender, RoutedEventArgs e)
        {
            currentSelectionMode = SelectionMode.GRID;
            GridButton.IsEnabled = !GridButton.IsEnabled;
            ListButton.IsEnabled = !ListButton.IsEnabled;
            if (currentMode == Mode.READING)
                return;
            switchToComicSelectionPanel();
        }

        private void ListLayout_Click(object sender, RoutedEventArgs e)
        {
            currentSelectionMode = SelectionMode.LIST;
            GridButton.IsEnabled = !GridButton.IsEnabled;
            ListButton.IsEnabled = !ListButton.IsEnabled;
            if (currentMode == Mode.READING)
                return;
            switchToComicSelectionPanel();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                yield break;
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
        private void ChangeTextColor(bool useDarkText)
        {
            CurrentTextColor = useDarkText ? darkText : lightText;
        }
        private void switchToComicSelectionPanel()
        {
            ComicsWrapPanel.Children.Clear();
            HomeButton.IsEnabled = false;
            currentMode = Mode.SELECTION;
            ComicsWrapPanel.Orientation = Orientation.Horizontal;
            MainScrollViewer.ScrollToTop();
            LoadComics();
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            switchToComicSelectionPanel();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (currentComic is ComicPDF comicPDF)
                return;
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
                                    saveCurrentPage(i > 0 ? i - 1 : 0);
                                    break;
                                }
                                lastPos = a.Y;
                            }
                        }
                        else
                        {
                            if (currentImageIndex > 0)
                            {
                                Image temp = currentImage;
                                saveCurrentPage(currentImageIndex - 1);
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
                                    saveCurrentPage(i);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (currentComic.getImageCount() > currentImageIndex + 1)
                            {
                                Image temp = currentImage;
                                saveCurrentPage(currentImageIndex + 1);
                                Image newImage = getImageByIndex(currentImageIndex);
                                temp.Source = newImage.Source;
                            }
                        }
                        break;
                    case Key.T:
                        if (currentReadingMode == ReadingMode.SCROLL)
                            MainScrollViewer.ScrollToTop();
                        else
                        {
                            Image temp = currentImage;
                            saveCurrentPage(0);
                            Image newImage = getImageByIndex(currentImageIndex);
                            temp.Source = newImage.Source;
                        }
                        break;
                    case Key.B:
                        if (currentReadingMode == ReadingMode.SCROLL)
                            MainScrollViewer.ScrollToEnd();
                        else
                        {
                            Image temp = currentImage;
                            saveCurrentPage(currentComic.getImageCount() - 1);
                            Image newImage = getImageByIndex(currentImageIndex);
                            temp.Source = newImage.Source;
                        }
                        break;
                }
                if(currentReadingMode == ReadingMode.SCROLL || currentReadingMode == ReadingMode.PAGE)
                {
                    if (e.Key == Key.Add) // "+" to dodawanie
                    {
                        ApplyZoom(zoomStep);
                    }
                    else if (e.Key == Key.Subtract) // "-" to odejmowanie
                    {
                        ApplyZoom(-zoomStep); 
                    }
                }
            }
        }
        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (currentMode == Mode.READING)
                saveCurrentPage();
        }
        private void saveCurrentPage()
        {
            if (currentReadingMode == ReadingMode.SCROLL)
            {
                for (int i = 0; i <= ComicsWrapPanel.Children.Count; ++i)
                {
                    Point a = ComicsWrapPanel.Children[i].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                    if (a.Y > 0)
                    {
                        Console.WriteLine("Saving " + i);
                        Console.WriteLine("index: " + ComicsWrapPanel.Children.Count); //14
                        saveCurrentPage(i);
                        break;
                    }
                }
            }
        }
        private void saveCurrentPage(int pageIndex)
        {
            currentImageIndex = pageIndex; //13
            currentComic.SavedPage = pageIndex; //13
            Console.WriteLine("Index Image: " + currentImageIndex);
            Console.WriteLine("Save Page: " +  currentComic.SavedPage);
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
                            BitmapImage bitmap = changeBrigthness(new BitmapImage(file), brightness);
                            if (!comicImages.ContainsKey(path))
                            {
                                img = new Image
                                {
                                    Source = bitmap,
                                    Width = MainScrollViewer.ActualWidth,
                                    MaxHeight = 700,
                                    RenderTransform = new ScaleTransform(1.0, 1.0),  // Dodanie transformacji
                                    RenderTransformOrigin = new Point(0.5, 0.5)      // Środek transformacji
                                };
                                comicImages.Add(path, img);
                            }
                            else
                            {
                                img = comicImages[path];
                            }

                            ComicsWrapPanel.Children.Add(img);
                        }
                        int tempPageIndex = currentComic.SavedPage;
                        ComicsWrapPanel.UpdateLayout();
                        saveCurrentPage(tempPageIndex);
                        Point a = ComicsWrapPanel.Children[currentImageIndex].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                        MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + a.Y);
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
        private void comicSettings(object sender, RoutedEventArgs e, Comic comic)
        {
            var dialog = new ComicSettings();
            TextBlock titleLabel = new TextBlock { Text = "Title:" };
            TextBox titleTextBox = new TextBox { Width = 200, Text = comic.Title };
            dialog.Form.Children.Add(new TextBlock { Text = "Properties", FontWeight = FontWeights.Bold, });
            dialog.Form.Children.Add(titleLabel);
            dialog.Form.Children.Add(titleTextBox);

            dialog.Form.Children.Add(new TextBlock { Text = "Category", FontWeight = FontWeights.Bold, });
            List<RadioButton> radioButtons = new List<RadioButton>();
            categories.ForEach((name) =>
            {
                RadioButton rb = new RadioButton
                {
                    Name = name,
                    Content = name,
                    IsChecked = (comic.Category == name),
                };
                radioButtons.Add(rb);
                dialog.Form.Children.Add(rb);
            });
            dialog.Form.Children.Add(new TextBlock { Text = "Cover", FontWeight = FontWeights.Bold, });
            String tempPathToCover = null;
            Button coverButton = new Button
            {
                Content = "Select Cover",
            };
            TextBlock label = new TextBlock { Text = "Not assigned" };
            coverButton.Click += (btnSender, btnE) =>
            {
                using (Forms.OpenFileDialog ofd = new Forms.OpenFileDialog())
                {
                    ofd.Filter = "Image (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";
                    if (ofd.ShowDialog() == Forms.DialogResult.OK)
                    {
                        tempPathToCover = ofd.FileName;
                        label.Text = Path.GetFileName(ofd.FileName);
                    }
                }
            };
            dialog.Form.Children.Add(coverButton);
            dialog.Form.Children.Add(label);
            bool result = dialog.ShowDialog().Value;
            if (result)
            {
                for (int i = 0; i < radioButtons.Count; ++i)
                {
                    if (radioButtons[i].IsChecked == true)
                    {
                        comic.Category = radioButtons[i].Name;
                        break;
                    }
                }
                if (tempPathToCover != null)
                    comic.cover = new Uri(tempPathToCover);
                switchToComicSelectionPanel();
            }
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

        private void NewCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialog
            {
                Header = "New category",
                PromptText = "Enter new category name:",
            };
            bool result = dialog.ShowDialog().Value;
            if (result)
            {
                string name = dialog.InputText;
                if (!categories.Contains(name))
                {
                    categories.Add(name);
                    if (currentMode == Mode.SELECTION)
                        switchToComicSelectionPanel();
                }
            }
        }

        private void SetLanguageToEnglish(object sender, RoutedEventArgs e)
        {
            // Przykład: zmiana tekstów na angielski
            Comic.Header = "Comic";
            Categories.Header = "Categories";
            Language.Header = "Language";
            Exit.Header = "Exit";
            New.Header = "New Folder";
            NewPDF.Header = "New PDF";
            NewCategory.Header = "New Category";
            Tools.Header = "Tools";
            zoomIn.Header = "Zoom In";
            zoomOut.Header = "Zoom Out";

            // Sidebar buttons
            HomeText.Text = "Home";
            ChangeBackgroudText.Text = "Change Background";
            GridLayout.Text = "Grid Layout";
            ListLayout.Text = "List Layout";
            Scrollbar.Text = "Scrollbar";
            Page.Text = "Page";
            Lightnes.Text = "Lighten up";
            Darked.Text = "Darked";
            ResetText.Text = "Reset";

            // Sidebar sections
            Options.Text = "Options";
            Layout.Text = "Layout";
            Reading_Mode.Text = "Reading Mode";
            Filter.Text = "Filters";
        }

        private void SetLanguageToPolish(object sender, RoutedEventArgs e)
        {
            // Przykład: zmiana tekstów na polski
            Comic.Header = "Komiks";
            Categories.Header = "Kategorie";
            Language.Header = "Język";
            Exit.Header = "Zakończ";
            New.Header = "Nowy Folder";
            NewPDF.Header = "Nowy PDF";
            NewCategory.Header = "Nowa Kategoria";
            Tools.Header = "Narzędzia";
            zoomIn.Header = "Powiększ";
            zoomOut.Header = "Pomniejsz";


            // Sidebar buttons
            HomeText.Text = "Strona główna";
            ChangeBackgroudText.Text = "Zmień tło";
            GridLayout.Text = "Układ siatki";
            ListLayout.Text = "Układ listy";
            Scrollbar.Text = "Pasek przewijania";
            Page.Text = "Strona";
            Lightnes.Text = "Rozjaśnanie";
            Darked.Text = "Przyciemnanie";
            ResetText.Text = "Resetuj";

            // Sidebar sections
            Options.Text = "Opcje";
            Layout.Text = "Układ";
            Reading_Mode.Text = "Tryb czytania";
            Filter.Text = "Filtr";
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            string messageBoxText = "Do you want to save changes?";
            string caption = "Quitting SharpReader";
            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);
            if (result == MessageBoxResult.Yes)
            {
                Dictionary<string, Comic> comicDictionary = new Dictionary<string, Comic>();
                foreach (Comic c in comics)
                {
                    comicDictionary.Add(c.Path, c);
                }
                AppSettings.Default.savedComics = JsonSerializer.Serialize(comicDictionary);

                AppSettings.Default.categories = JsonSerializer.Serialize(categories);

                AppSettings.Default.Save();
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }

       private void BrightnessUpButton_Click(object sender, RoutedEventArgs e)
        {
            brightness += 100;
            foreach (var kvp in comicImages)
            {
               kvp.Value.Source =  changeBrigthness(new BitmapImage(new Uri(kvp.Key)), brightness);
            }
            //ComicsWrapPanel.UpdateLayout();
        }

        private void BrightnessDownButton_Click(Object sender, RoutedEventArgs e)
        {
            brightness -= 100;
            foreach (var kvp in comicImages)
            {
                kvp.Value.Source = changeBrigthness(new BitmapImage(new Uri(kvp.Key)), brightness);

            }
            //ComicsWrapPanel.UpdateLayout();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            brightness = 0;
            foreach (var kvp in comicImages)
            {
                kvp.Value.Source = changeBrigthness(new BitmapImage(new Uri(kvp.Key)), brightness);
            }
        }

        private void ZoomInMenuItem(object sender, RoutedEventArgs e)
        {
            ApplyZoom(zoomStep);  // Przybliżenie
        }

        private void ZoomOutMenuItem(object sender, RoutedEventArgs e)
        {
            ApplyZoom(-zoomStep);  //Oddalanie
        }

        private void ApplyZoom(double zoomDelta)
        {
            currentZoom += zoomDelta;
            currentZoom = Math.Max(0.1, Math.Min(3.0, currentZoom)); // Zoom w zakresie [0.1, 3.0]

            if (currentReadingMode == ReadingMode.SCROLL)
            {
                foreach (var item in ComicsWrapPanel.Children)
                {
                    if (item is Image img)
                    {
                        ApplyScaleTransform(img);
                    }
                }
            }
            else if (currentReadingMode == ReadingMode.PAGE)
            {
                if (currentImage != null)
                {
                    ApplyScaleTransform(currentImage);
                }
            }
        }

        private void ApplyScaleTransform(Image image)
        {
            var transform = image.RenderTransform as ScaleTransform;
            if (transform == null)
            {
                transform = new ScaleTransform(1.0, 1.0);
                image.RenderTransform = transform;
                image.RenderTransformOrigin = new Point(0.5, 0.5);
                image.Stretch = Stretch.Uniform;
                image.Width = MainScrollViewer.ActualWidth;
            }
            if (image.RenderTransform is ScaleTransform)
            {
                transform.ScaleX = currentZoom;
                transform.ScaleY = currentZoom;
            }
            // Dodajemy eventy do obsługi przesuwania myszy
            image.MouseDown += Image_MouseDown;
            image.MouseMove += Image_MouseMove;
            image.MouseUp += Image_MouseUp;
        }

        // Zdarzenie uruchamiane po kliknięciu myszy na obraz
        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img)
            {
                isMouseDown = true;  // Flaga wskazująca, że użytkownik nacisnął przycisk myszy

                // Zapisujemy ostatnią pozycję myszy
                lastMousePosition = e.GetPosition(img);
            }
        }

        // Zdarzenie uruchamiane, gdy użytkownik przesuwa myszką po obrazie
        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown && sender is Image img)
            {
                // Obliczamy przesunięcie myszy
                Point currentMousePosition = e.GetPosition(img);
                Vector delta = currentMousePosition - lastMousePosition;

                // Zmieniamy pozycję obrazu w panelu
                if (img.RenderTransform is ScaleTransform transform)
                {
                    transform.CenterX += delta.X;
                    transform.CenterY += delta.Y;
                }

                // Ustawiamy ostatnią pozycję myszy jako aktualną
                lastMousePosition = currentMousePosition;
            }
        }

        // Zdarzenie uruchamiane, gdy użytkownik zwalnia przycisk myszy
        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image)
            {
                isMouseDown = false;  // Flaga, gdy użytkownik zwalnia przycisk myszy
            }
        }

        private BitmapImage changeBrigthness(BitmapImage bitmap, int brightness)
        {
            if (brightness == 0)
                return bitmap;
            WriteableBitmap writeableBitmap = new WriteableBitmap(bitmap);
            IntPtr backBuffer = writeableBitmap.BackBuffer;

            for (int y = 0; y < writeableBitmap.PixelHeight; y++)
            {
                for (int x = 0; x < writeableBitmap.PixelWidth; x++)
                {
                    int pixelIndex = (y * writeableBitmap.BackBufferStride) + (x * 4);
                    byte blue = Marshal.ReadByte(backBuffer, pixelIndex);
                    byte green = Marshal.ReadByte(backBuffer, pixelIndex + 1);
                    byte red = Marshal.ReadByte(backBuffer, pixelIndex + 2);
                    byte alpha = Marshal.ReadByte(backBuffer, pixelIndex + 3);
                    red = (byte)Math.Max(Math.Min(255, red + brightness), 0);
                    green = (byte)Math.Max(Math.Min(255, green + brightness), 0);
                    blue = (byte)Math.Max(Math.Min(255, blue + brightness), 0);
                    Marshal.WriteByte(backBuffer, pixelIndex, blue);
                    Marshal.WriteByte(backBuffer, pixelIndex + 1, green);
                    Marshal.WriteByte(backBuffer, pixelIndex + 2, red);
                    Marshal.WriteByte(backBuffer, pixelIndex + 3, alpha);
                }
            }
            writeableBitmap.Lock();
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();
            BitmapImage resultImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;
                resultImage.BeginInit();
                resultImage.StreamSource = memoryStream;
                resultImage.CacheOption = BitmapCacheOption.OnLoad;
                resultImage.EndInit();
                resultImage.Freeze();
            }
            writeableBitmap.Lock();
            return resultImage;
        }
    }
}