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
using Forms = System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Text.Json;
using static SharpReader.Comic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Windows.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Win32;
using System.Resources;
using System.Diagnostics;

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
        private bool isSystemThemeMode;
        private bool mirrorOn = false;
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
        private bool _isClosingHandled = false; // Flaga zapobiegająca wielokrotnemu zamykaniu

        private DateTime _startTime;
        private DispatcherTimer _timer;
        private int _clickCount = 0; // Licznik kliknięć
        private Dictionary<string, int> _clickStats = new Dictionary<string, int>(); // Słownik zliczający kliknięcia w różne elementy
        private Task scrollingTask = null;
        private CancellationTokenSource src = null;
        private CancellationToken ct;
        private double scrollingSpeed = 1;
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
            TestSlack();
            this.Loaded += (object sender, RoutedEventArgs e) =>
            {
                if (AppSettings.Default.allowDataCollection == false)
                {
                    allowDataCollectionMessage();
                }
            };

            this.PreviewMouseDown += (sender, e) =>
            {
                _clickCount++;
                string clickedElement = "Nieznany element"; // Domyślnie

                if (e.OriginalSource is FrameworkElement element)
                {
                    clickedElement = element.Name; // Pobranie nazwy elementu
                    if (string.IsNullOrEmpty(clickedElement))
                        clickedElement = element.GetType().Name; // Jeśli element nie ma nazwy, używamy jego typu
                }

                // Zliczanie kliknięć w dany element
                if (_clickStats.ContainsKey(clickedElement))
                {
                    _clickStats[clickedElement]++;
                }
                else
                {
                    _clickStats[clickedElement] = 1;
                }
                // Console.WriteLine($"🖱️ Kliknięcie! Licznik: {_clickCount}");
            };

            // Wybranie Jezyka Deafultego dla aplikacji 
            string langCode = Properties.Settings.Default.Language;
            if (string.IsNullOrEmpty(langCode))
            {
                langCode = "en";  // Domyślny język
            }
            SetLanguage(langCode);

            // Ustawienie języka dla konkretnego kraju wykrytego jak nie wykryje to Angielski
            SetSystemLanguage();

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
                isSystemThemeMode = AppSettings.Default.isSystemThemeMode;
            }
            catch (NullReferenceException e)
            {
                MessageBox.Show(e.ToString(), "Error when loading settings!");
                CurrentTextColor = darkText;
                AppSettings.Default.darkTheme = false;
                isDarkMode = false;
                AppSettings.Default.isSystemThemeMode = false;
                isSystemThemeMode = false;
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
                            comics.Add(new ComicPDF(kvp.Key, c.Title, c.Category, c.SavedPage));
                            break;
                    }
                }
            }

            catch (JsonException e) { }
            ChangeBackground_Click(null, null);
            if (isSystemThemeMode)
            {
                setSystemThemeOn();
            }
            else
            {
                setSystemThemeOff();
            }
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
                ComicImages c = new ComicImages("resources\\ActionComics", "Superman");
                comics.Add(c);
            }
            switchToSelectionPanel();
        }
        private void allowDataCollectionMessage()
        {
            string messageBoxText = "This application collects anonymous diagnostics.\nIf you do not wish to share diagnostic data close this application.";
            string caption = "Allow data collection?";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Warning;

            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            if (result == MessageBoxResult.No)
            {
                Application.Current.Shutdown();
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                AppSettings.Default.allowDataCollection = true;
                AppSettings.Default.Save();
            }
        }
        private void startAutoScrolling()
        {
            StartScrollingButtonLabel.Text = "Turn off auto scrolling";
            src = new CancellationTokenSource();
            ct = src.Token;
            scrollingTask = Task.Run(() =>
            {
                const int startingValue = 1000;
                int pageDelay = startingValue;
                try
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (currentReadingMode == ReadingMode.PAGE)
                        {
                            --pageDelay;
                            if (pageDelay <= 0)
                            {
                                pageDelay = startingValue;
                                Dispatcher.Invoke(() =>
                                {
                                    turnPageForward();
                                });
                            }
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + 1.0d);
                                saveCurrentPage();
                            });
                        }
                        Thread.Sleep((int)(10 / scrollingSpeed));
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                finally
                {
                    src.Dispose();
                    src = null;
                }
            }, ct);
        }
        private void stopAutoScrolling()
        {
            StartScrollingButtonLabel.Text = "Turn on auto scrolling";
            if (src != null)
                src.Cancel();
        }
        private void StartScrollingButton_Click(object sender, RoutedEventArgs e)
        {
            if (scrollingTask == null || !scrollingTask.Status.Equals(TaskStatus.Running))
            {
                startAutoScrolling();
            }
            else
            {
                stopAutoScrolling();
            }
        }
        private void TestSlack()
        {
            // Start śledzenia czasu
            _startTime = DateTime.Now;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (sender, e) =>
            {
                TimeSpan elapsed = DateTime.Now - _startTime;
                //this.Title = $"⏳ Czas: {elapsed:mm\\:ss}";
            };
            _timer.Start();
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
                Value = comic.SavedPage <= 0 || comic.getImageCount() <= 0 ? 0 : (comic.SavedPage + 1) * 100 / comic.getImageCount(),
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
        private void setBackgroundToDark()
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
        private void setBackgroundToLight()
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
        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            ChangeTextColor(isDarkMode);
            if (isDarkMode)
            {
                setBackgroundToDark();
            }
            else
            {
                setBackgroundToLight();
            }
            AppSettings.Default.darkTheme = isDarkMode;
            isDarkMode = !isDarkMode;
        }
        private void SystemBackground_Click(object sender, RoutedEventArgs e)
        {
            isSystemThemeMode = !isSystemThemeMode;
            AppSettings.Default.isSystemThemeMode = isSystemThemeMode;
            if (!isSystemThemeMode)
            {
                setSystemThemeOff();
            }
            else
            {
                setSystemThemeOn();
            }
        }
        private void setSystemThemeOff()
        {
            isSystemThemeMode = false;
            ChangeBackground.IsEnabled = true;
            SystemBackgroudText.Text = "Use system theme off";
            ChangeTextColor(!isDarkMode);
            if (!isDarkMode)
            {
                setBackgroundToDark();
            }
            else
            {
                setBackgroundToLight();
            }
        }
        private void setSystemThemeOn()
        {
            isSystemThemeMode = true;
            ChangeBackground.IsEnabled = false;
            SystemBackgroudText.Text = "Use system theme on";
            var theme = !IsLightTheme();
            ChangeTextColor(theme);
            if (theme)
            {
                setBackgroundToDark();
            }
            else
            {
                setBackgroundToLight();
            }
        }
        private static bool IsLightTheme()
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
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
            switchToSelectionPanel();
        }

        private void ListLayout_Click(object sender, RoutedEventArgs e)
        {
            currentSelectionMode = SelectionMode.LIST;
            GridButton.IsEnabled = !GridButton.IsEnabled;
            ListButton.IsEnabled = !ListButton.IsEnabled;
            if (currentMode == Mode.READING)
                return;
            switchToSelectionPanel();
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
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
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
        private void switchToSelectionPanel()
        {
            stopAutoScrolling();
            ComicsWrapPanel.Children.Clear();
            HomeButton.IsEnabled = false;
            StartScrollingButton.IsEnabled = false;
            Reset_Click(null, null);
            currentMode = Mode.SELECTION;
            ComicsWrapPanel.Orientation = Orientation.Horizontal;
            MainScrollViewer.ScrollToTop();
            LoadComics();
        }
        private void switchToReadingPanel(object sender, RoutedEventArgs e, Comic comic)
        {
            currentMode = Mode.READING;
            currentComic = comic;
            ComicsWrapPanel.Children.Clear();
            ComicsWrapPanel.Orientation = Orientation.Vertical;
            if (currentComic is ComicPDF comicPDF)
            {
                if (currentReadingMode == ReadingMode.SCROLL)
                {
                    for (int i = 0; i < currentComic.getImageCount(); i++)
                    {
                        Image img;
                        string path = currentComic.Path + i;
                        if (!comicImages.ContainsKey(path))
                        {
                            BitmapImage bitmap = changeBrigthness(BitmapSourceToBitmapImage(currentComic.pageToImage(i)), brightness);
                            img = new Image
                            {
                                Source = bitmap,
                                Width = MainScrollViewer.ActualWidth,
                                MaxHeight = 700,
                                RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                                RenderTransformOrigin = new Point(0.5, 0.5),
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
                        Source = getImageByIndex(currentComic.SavedPage).Source,
                        Width = MainScrollViewer.ActualWidth,
                        MaxHeight = 700,
                        RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                        RenderTransformOrigin = new Point(0.5, 0.5),
                    };
                    currentImageIndex = currentComic.SavedPage;
                    ComicsWrapPanel.Children.Add(currentImage);
                }
                //var host = new WindowsFormsHost
                //{
                //    Child = comicPDF.Viewer,
                //    Width = MainScrollViewer.ActualWidth,
                //    Height = MainScrollViewer.ActualHeight,
                //};
                //MainScrollViewer.SizeChanged += (s, args) =>
                //{
                //    host.Width = MainScrollViewer.ActualWidth;
                //    host.Height = MainScrollViewer.ActualHeight;
                //};
                //ComicsWrapPanel.Children.Clear();
                //ComicsWrapPanel.Children.Add(host);
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
                                BitmapImage bitmap = changeBrigthness(new BitmapImage(file), brightness);
                                img = new Image
                                {
                                    Source = bitmap,
                                    Width = MainScrollViewer.ActualWidth,
                                    MaxHeight = 700,
                                    RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                                    RenderTransformOrigin = new Point(0.5, 0.5),
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
                            Source = getImageByIndex(currentComic.SavedPage).Source,
                            Width = MainScrollViewer.ActualWidth,
                            MaxHeight = 700,
                            RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                        };
                        currentImageIndex = currentComic.SavedPage;
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
            StartScrollingButton.IsEnabled = true;
            stopAutoScrolling();
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            switchToSelectionPanel();
        }
        private void turnPageBy(int n)
        {
            int nextPage = currentImageIndex + n;

            if (nextPage < currentComic.getImageCount() && nextPage >= 0)
            {
                saveCurrentPage(currentImageIndex + n);
                Image newImage = getImageByIndex(currentImageIndex);
                currentImage.Source = newImage.Source;
            }
        }
        private void turnPageForward()
        {
            turnPageBy(1);
        }
        private void turnPageBackward()
        {
            turnPageBy(-1);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            //if (currentComic is ComicPDF comicPDF)
            //    return;
            if (currentMode == Mode.READING)
            {
                double lastPos = 0.0d;
                switch (e.Key)
                {
                    case Key.A:
                        stopAutoScrolling();
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
                            turnPageBackward();
                        }
                        break;
                    case Key.D:
                        stopAutoScrolling();
                        if (currentReadingMode == ReadingMode.SCROLL)
                        {
                            for (int i = 0; i < ComicsWrapPanel.Children.Count; ++i)
                            {
                                Point a = ComicsWrapPanel.Children[i].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                                if (a.Y > 0)
                                {
                                    MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + a.Y);
                                    saveCurrentPage(i);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            turnPageForward();
                        }
                        break;
                    case Key.T:
                        stopAutoScrolling();
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
                        stopAutoScrolling();
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
                    case Key.Add:
                        ApplyZoom(zoomStep);
                        break;
                    case Key.Subtract:
                        ApplyZoom(-zoomStep);
                        break;
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
                for (int i = 0; i < ComicsWrapPanel.Children.Count; ++i)
                {
                    Point a = ComicsWrapPanel.Children[i].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                    if (a.Y > 0)
                    {
                        // Console.WriteLine("Saving " + i);
                        // Console.WriteLine("index: " + ComicsWrapPanel.Children.Count);
                        saveCurrentPage(i);
                        break;
                    }
                }
            }
        }
        private void saveCurrentPage(int pageIndex)
        {
            currentImageIndex = pageIndex;
            currentComic.SavedPage = pageIndex;
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
                if (!string.IsNullOrEmpty(tempPathToCover))
                    comic.cover = new Uri(tempPathToCover);
                if (!string.IsNullOrEmpty(titleTextBox.Text))
                    comic.Title = titleTextBox.Text;
                switchToSelectionPanel();
            }
        }
        private Image getImageByIndex(int index)
        {
            if (index < 0)
                index = 0;
            if (currentComic is ComicPDF comicPDF)
            {
                Image img = new Image
                {
                    Source = currentComic.pageToImage(index),
                    Width = MainScrollViewer.ActualWidth,
                    MaxHeight = 700,
                    RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                return img;
            }
            else
            {
                List<Uri> files = currentComic.getImages();
                Image img = null;
                Uri file = files[index];
                string path = file.ToString();
                if (!comicImages.ContainsKey(path))
                {
                    img = new Image
                    {
                        Source = new BitmapImage(file),
                        Width = MainScrollViewer.ActualWidth,
                        MaxHeight = 700,
                        RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                        RenderTransformOrigin = new Point(0.5, 0.5),
                    };
                    comicImages.Add(path, img);
                }
                else
                {
                    img = comicImages[path];
                }
                return img;
            }
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
                        switchToSelectionPanel();
                }
            }
        }

        private bool IsLanguageSupported(string langCode)
        {
            return langCode == "pl" || langCode == "en"; // Obsługiwane języki
        }

        //Pobieranie jezyka Systemowego
        private void SetSystemLanguage()
        {
            // Pobieranie języka systemowego
            string systemLanguage = CultureInfo.CurrentUICulture.Name;

            if (!IsLanguageSupported(systemLanguage))
            {
                systemLanguage = "en"; // Domyślny język (angielski)
            }

            Trace.WriteLine($"🌐 Język systemowy: {systemLanguage}");
            // Ustawienie języka aplikacji na podstawie języka systemowego
            SetLanguage(systemLanguage);
        }

        public void SetLanguage(string langCode)
        {
            // Ustawienie nowej kultury UI
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(langCode);

            // Pobranie zasobów językowych
            ResourceManager resourceManager = new ResourceManager("SharpReader.resources.Strings", typeof(ResourceLoader).Assembly);


            // Toolbar
            Comic.Header = resourceManager.GetString("Comic");
            Categories.Header = resourceManager.GetString("Categories");
            Language.Header = resourceManager.GetString("Language");
            New.Header = resourceManager.GetString("NewFolder");
            NewPDF.Header = resourceManager.GetString("NewPDF");
            NewCategory.Header = resourceManager.GetString("NewCategory");
            Tools.Header = resourceManager.GetString("Tools");
            zoomIn.Header = resourceManager.GetString("ZoomIn");
            zoomOut.Header = resourceManager.GetString("ZoomOut");
            MirrorButton.Header = resourceManager.GetString("MirrorImages");
            ResetPreferences.Header = resourceManager.GetString("ResetPreferences");

            // Sidebar buttons
            HomeText.Text = resourceManager.GetString("HomeText");
            ChangeBackgroudText.Text = resourceManager.GetString("ChangeBackground");
            GridLayout.Text = resourceManager.GetString("GridLayout");
            ListLayout.Text = resourceManager.GetString("ListLayout");
            Scrollbar.Text = resourceManager.GetString("Scrollbar");
            Page.Text = resourceManager.GetString("Page");
            Lightnes.Text = resourceManager.GetString("Lighten");
            Darken.Text = resourceManager.GetString("Darken");
            ResetText.Text = resourceManager.GetString("ResetText");

            // Sidebar sections
            Options.Text = resourceManager.GetString("Options");
            Layout.Text = resourceManager.GetString("Layout");
            Reading_Mode.Text = resourceManager.GetString("ReadingMode");
            Filter.Text = resourceManager.GetString("Filters");

            // Zapisanie języka w ustawieniach aplikacji
            Properties.Settings.Default.Language = langCode;
            Properties.Settings.Default.Save();
        }

        private void SetLanguageToEnglish(object sender, RoutedEventArgs e)
        {
            SetLanguage("en");
        }

        private void SetLanguageToPolish(object sender, RoutedEventArgs e)
        {
            SetLanguage("pl");
        }

        private void BrightnessUpButton_Click(object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
            brightness += 100;
            foreach (var kvp in comicImages)
            {
                kvp.Value.Source = changeBrigthness(new BitmapImage(new Uri(kvp.Key)), brightness);
            }
            //ComicsWrapPanel.UpdateLayout();
        }

        private void BrightnessDownButton_Click(Object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
            brightness -= 100;
            foreach (var kvp in comicImages)
            {
                kvp.Value.Source = changeBrigthness(new BitmapImage(new Uri(kvp.Key)), brightness);
            }
            //ComicsWrapPanel.UpdateLayout();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
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
                    transform.CenterX -= delta.X;
                    transform.CenterY -= delta.Y;
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
        public static BitmapImage BitmapSourceToBitmapImage(BitmapSource bitmapSource)
        {
            // Create a MemoryStream to hold the image data
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Encode the BitmapSource to a PNG format and save it to the MemoryStream
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);

                // Create a BitmapImage and set its stream source to the MemoryStream
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                memoryStream.Seek(0, SeekOrigin.Begin); // Reset the stream position
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Load the image into memory
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Freeze the BitmapImage for cross-thread access

                return bitmapImage;
            }
        }
        private void ResetPreferences_Click(object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
            string messageBoxText = "Do you want to reset app settings?";
            string caption = "Reset app settings";

            MessageBoxButton button = MessageBoxButton.OKCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;

            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.OK);

            if (result != MessageBoxResult.OK)
            {
                return;
            }
            AppSettings.Default.savedComics = JsonSerializer.Serialize("");
            AppSettings.Default.categories = JsonSerializer.Serialize("");
            AppSettings.Default.darkTheme = false;
            AppSettings.Default.isSystemThemeMode = false;
            AppSettings.Default.Save();
            categories.Clear();
            comics.Clear();
            categories.Add("Favourite");
            categories.Add("Other");
            ComicImages c = new ComicImages("resources\\ActionComics", "Superman");
            comics.Add(c);
            isDarkMode = false;
            ChangeTextColor(isDarkMode);
            setBackgroundToLight();
            switchToSelectionPanel();
        }
        private void adjustImageXAxis(Image img)
        {
            if (img.RenderTransform is ScaleTransform scaleTransform)
            {
                if (mirrorOn)
                {
                    scaleTransform.ScaleX *= scaleTransform.ScaleX > 0 ? -1 : 1;
                }
                else
                {
                    scaleTransform.ScaleX *= scaleTransform.ScaleX < 0 ? -1 : 1;
                }
            }
        }
        private void mirror_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            mirrorOn = !mirrorOn;
            var a = FindVisualChild<TextBlock>(item);
            if (a != null)
            {
                a.Text = mirrorOn ? "✔" : "";
            }
            foreach (var img in comicImages.Values)
            {
                adjustImageXAxis(img);
            }
            if (currentImage != null)
            {
                adjustImageXAxis(currentImage);
            }
        }
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            stopAutoScrolling();
            if (AppSettings.Default.allowDataCollection == false)
                return;
            if (_isClosingHandled)
                return; // Jeśli już obsługujemy zamykanie, nie rób nic więcej

            _isClosingHandled = true; // Ustawiamy flagę, aby zapobiec ponownemu wywołaniu

            string messageBoxText = "Do you want to save changes?";
            string caption = "Quitting SharpReader";
            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;

            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                _isClosingHandled = false; // Cofamy flagę, aby móc ponownie zamykać w przyszłości
                return;
            }

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
            TimeSpan totalTime = DateTime.Now - _startTime;
            string closeDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string clickReport = string.Join("\n", _clickStats.Select(kv => $"🔹 {kv.Key}: {kv.Value}x"));
            string systemInfo = SystemInfoCollector.GetSystemInfo();

            string appLanguage = CultureInfo.CurrentUICulture.DisplayName;

            string comicProgressReport = "";
            foreach (Comic comic in comics)
            {
                double progress = comic.SavedPage <= 0 || comic.getImageCount() <= 0 ? 0 : (comic.SavedPage + 1) * 100 / comic.getImageCount();
                string progressText = progress < 100 ? $"{progress:F1}%" : "Finished";
                string comicTitle = comic.Title;

                // Dodajemy postęp komiksu do raportu
                comicProgressReport += $"   📖 Komiks: {comicTitle}\n" +
                                       $"   🕒 Postęp: {progressText}\n\n";
            }

            string report = $"📊 Statystyki aplikacji:\n" +
                            $"⏳ Czas spędzony: {totalTime:mm\\:ss} min\n" +
                            $"📅 Zamknięto: {closeDateTime}\n" +
                            $"🖱️ Liczba kliknięć: {_clickCount}\n" +
                            $"🎯 Kliknięte elementy:\n{clickReport}\n" +
                            $"🌎 Język aplikacji: {appLanguage}\n" +
                            $"🌎 Język systemu: {CultureInfo.CurrentCulture.DisplayName}\n" +
                            $"{systemInfo}\n" +
                            $"📰 Postęp w komiksach:\n{comicProgressReport}";

            // Console.WriteLine("🚀 Wysyłam raport na Slacka...");
            e.Cancel = true;
            if (AppSettings.Default.allowDataCollection == true)
            {
                // await SlackLoger.SendMessageAsync(report);
            }
            Application.Current.Shutdown();
        }
    }
}