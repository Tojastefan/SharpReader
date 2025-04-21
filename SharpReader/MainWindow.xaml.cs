using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
using SharpReader.Controls;
using System.Windows.Data;

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
        public ReportData reportData;
        private readonly Brush darkText = Brushes.White;
        private readonly Brush lightText = Brushes.Black;
        public bool isDarkMode;
        private bool isSystemThemeMode;
        private bool mirrorOn = false;
        private Mode currentMode;
        private ReadingMode currentReadingMode;
        private SelectionMode currentSelectionMode;
        private List<string> categories;
        private List<Comic> comics = new List<Comic>();
        private int currentImageIndex = 0;
        private Image currentImage = null;
        private Image currentImageClone = null;
        private Comic currentComic;
        private Dictionary<string, Image> comicImages = new Dictionary<string, Image>();
        private Dictionary<Image, Image> originalModifiedPairImages = new Dictionary<Image, Image>();
        private Brush currentTextColor;
        private int brightness = 0;
        private int currentZoom = 1;  // Początkowy poziom zoomu
        private int currentZoomSquareSize = 120;
        private Point lastMousePosition;         // Przechowywanie ostatniej pozycji myszy
        private bool isMouseDown = false;        // Flaga, czy przycisk myszy jest wciśnięty
        private bool _isClosingHandled = false; // Flaga zapobiegająca wielokrotnemu zamykaniu

        private readonly HashSet<string> imageExtensions = new HashSet<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }; // Obsługiwane rozszerzenia plików obrazów

        ResourceManager resourceManager = new ResourceManager("SharpReader.resources.Strings", typeof(ResourceLoader).Assembly);

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
        public Binding TextColorBinding;
        Color darkSidebar = (Color)ColorConverter.ConvertFromString("#3c3c3c");
        Color lightSidebar = (Color)ColorConverter.ConvertFromString("#F5F5F5");

        public MainWindow()
        {
            InitializeComponent();
            TextColorBinding = new Binding("CurrentTextColor")
            {
                Source = this
            };
            reportData = new ReportData();
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
                        clickedElement = element.GetType().Name;
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
                tb.SetBinding(TextBlock.ForegroundProperty, TextColorBinding);
            }
            // Example comic
            if (comics.Count < 1)
            {
                ComicImages c = new ComicImages("resources\\ActionComics", "Superman");
                comics.Add(c);
            }
            switchToSelectionPanel();
        }
        // Sprawdzenie, czy plik to PDF lub obraz
        private bool IsValidFileType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            return ext == ".pdf" || imageExtensions.Contains(ext);
        }
        // Sprawdzenia Folderu
        private bool IsFolder(string filePath)
        {
            return Directory.Exists(filePath);
        }
        // Obsługa przeciągania nad ComicsWrapPanel
        private void ComicsWrapPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Sprawdzenie czy to plik PDF lub obraz
                if (files.Any(file => IsValidFileType(file) || IsFolder(file)))
                {
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
        }
        // Obsługa upuszczania pliku
        private void ComicsWrapPanel_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string file in files)
                {
                    if (Path.GetExtension(file).ToLower() == ".pdf")
                    {
                        ComicPDF newComic = new ComicPDF(file, Path.GetFileNameWithoutExtension(file));
                        comics.Add(newComic); // Dodaj komiks do listy komiksów

                        // Oczyszczenie panelu i załadowanie ponownie
                        ComicsWrapPanel.Children.Clear();
                        LoadComics();
                    }
                    else if (IsFolder(file))
                    {
                        // Obsługa folderu
                        string folderPath = file;
                        ComicImages newComicImages = new ComicImages(folderPath, Path.GetFileName(folderPath));
                        comics.Add(newComicImages); // Dodaj folder do listy komiksów

                        // Oczyszczenie panelu i załadowanie ponownie
                        ComicsWrapPanel.Children.Clear();
                        LoadComics(); // Ponownie załaduj wszystkie komiksy
                    }
                }
            }
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
            StartScrollingButton.TooltipText = resourceManager.GetString("StartScrollingButtonLabelOn");
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
            StartScrollingButton.TooltipText = resourceManager.GetString("StartScrollingButtonLabelOff");
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
                // this.Title = $"⏳ Czas: {elapsed:mm\\:ss}";
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
                filteredComics.ForEach((e) => {
                    var c = new ComicPanel(this, e)
                    {
                        Text = CurrentTextColor,
                    };
                    c.SetBinding(ForegroundProperty, TextColorBinding);
                    innerwp.Children.Add(c);
                });
                wp.Children.Add(innerwp);
                ComicsWrapPanel.Children.Add(wp);
            }
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
            // SystemBackground.TooltipText = "Use system theme off";
            SystemBackground.TooltipText = resourceManager.GetString("SystemBackgroundTextOff");
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
            // SystemBackground.TooltipText = "Use system theme on";
            SystemBackground.TooltipText = resourceManager.GetString("SystemBackgroundTextOn");
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
            tb.SetBinding(TextBlock.ForegroundProperty, TextColorBinding);
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
            ApplyZoom(1);
            Reset_Click(null, null);
            ComicsWrapPanel.Children.Clear();
            HomeButton.IsEnabled = false;
            NavigationLabel.Visibility = Visibility.Collapsed;
            NavigationStackPanel.Visibility = Visibility.Collapsed;
            StartScrollingButton.IsEnabled = false;
            currentMode = Mode.SELECTION;
            ComicsWrapPanel.Orientation = Orientation.Horizontal;
            MainScrollViewer.ScrollToTop();
            LoadComics();
        }
        public void switchToReadingPanel(object sender, RoutedEventArgs e, Comic comic)
        {
            stopAutoScrolling();
            currentMode = Mode.READING;
            currentComic = comic;
            CurrentPageLabel.Text = currentImageIndex.ToString();
            HomeButton.IsEnabled = true;
            NavigationLabel.Visibility = Visibility.Visible;
            NavigationStackPanel.Visibility = Visibility.Visible;
            StartScrollingButton.IsEnabled = true;
            ComicsWrapPanel.Children.Clear();
            ComicsWrapPanel.Orientation = Orientation.Vertical;
            if (currentComic is ComicPDF comicPDF)
            {
                if (currentReadingMode == ReadingMode.SCROLL)
                {
                    for (int i = 0; i < currentComic.getImageCount(); i++)
                    {
                        Image img, clone;
                        string path = currentComic.Path + i;
                        if (!comicImages.ContainsKey(path))
                        {
                            // BitmapImage bitmap = changeBrigthness(BitmapSourceToBitmapImage(), brightness);
                            img = new Image
                            {
                                Source = currentComic.pageToImage(i),
                                Width = MainScrollViewer.ActualWidth,
                                MaxHeight = 700,
                                RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                                RenderTransformOrigin = new Point(0.5, 0.5),
                            };
                            comicImages.Add(path, img);
                            clone = cloneImage(img);
                            originalModifiedPairImages.Add(img, clone);
                        }
                        else
                        {
                            img = comicImages[path];
                            clone = cloneImage(img);
                            originalModifiedPairImages[img] = clone;
                        }
                        ComicsWrapPanel.Children.Add(clone);
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
                    currentImageClone = cloneImage(currentImage);
                    originalModifiedPairImages.Add(currentImage, currentImageClone);
                    currentImageIndex = currentComic.SavedPage;
                    ComicsWrapPanel.Children.Add(currentImageClone);
                }
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
                            Image img, clone;
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
                                clone = cloneImage(img);
                                originalModifiedPairImages.Add(img, clone);
                            }
                            else
                            {
                                img = comicImages[path];
                                clone = cloneImage(img);
                                originalModifiedPairImages[img] = clone;
                            }

                            ComicsWrapPanel.Children.Add(clone);
                        }
                        int tempPageIndex = currentComic.SavedPage;
                        ComicsWrapPanel.UpdateLayout();
                        saveCurrentPage(tempPageIndex);
                        Point a = ComicsWrapPanel.Children[currentImageIndex].TransformToAncestor(MainScrollViewer).Transform(new Point(0, 0));
                        MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + a.Y);
                    }
                    else
                    {
                        if (currentImage != null)
                        {
                            originalModifiedPairImages.Remove(currentImage);
                        }
                        currentImage = new Image
                        {
                            Source = getImageByIndex(currentComic.SavedPage).Source,
                            Width = MainScrollViewer.ActualWidth,
                            MaxHeight = 700,
                            RenderTransform = new ScaleTransform(mirrorOn ? -1.0 : 1.0, 1.0),
                            RenderTransformOrigin = new Point(0.5, 0.5),
                        };
                        currentImageClone = cloneImage(currentImage);
                        originalModifiedPairImages.Add(currentImage, currentImageClone);
                        currentImageIndex = currentComic.SavedPage;
                        ComicsWrapPanel.Children.Add(currentImageClone);
                    }
                }
                else
                {
                    Console.WriteLine("The directory does not exist.");
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    Console.WriteLine($"Base Directory: {basePath}");
                }
            }
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            switchToSelectionPanel();
        }
        private void turnPageBy(int n)
        {
            int nextPage = currentImageIndex + n;
            turnPageTo(nextPage);
        }
        private void turnPageTo(int nextPage)
        {
            if (nextPage >= currentComic.getImageCount() || nextPage < 0)
                return;
            Reset_Click(null, null);
            saveCurrentPage(nextPage);
            Image newImage = getImageByIndex(nextPage);
            currentImage.Source = newImage.Source;
            currentImageClone.Source = newImage.Source;
        }
        private void turnPageForward()
        {
            turnPageBy(1);
        }
        private void turnPageBackward()
        {
            turnPageBy(-1);
        }
        private void NavLeftButton_Click(object sender, RoutedEventArgs e)
        {
            turnPageBackward();
        }
        private void NavRightButton_Click(object sender, RoutedEventArgs e)
        {

            turnPageForward();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
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
                            Reset_Click(null, null);
                            Image temp = currentImageClone;
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
                            Reset_Click(null, null);
                            Image temp = currentImageClone;
                            saveCurrentPage(currentComic.getImageCount() - 1);
                            Image newImage = getImageByIndex(currentImageIndex);
                            temp.Source = newImage.Source;
                        }
                        break;
                    case Key.Add:
                        ZoomIncrease();
                        break;
                    case Key.Subtract:
                        ZoomDecrease();
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
                        saveCurrentPage(i);
                        break;
                    }
                }
            }
        }
        private void saveCurrentPage(int pageIndex)
        {
            currentImageIndex = pageIndex;
            currentComic.SavedPage = currentImageIndex;
            CurrentPageLabel.Text = currentImageIndex.ToString();
        }
        public void comicSettings(object sender, RoutedEventArgs e, Comic comic)
        {
            var dialog = new ComicSettings
            {
                Owner = this,
            };
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
            Button Delete = new Button
            {
                Margin = new Thickness(5),
                Content = "Delete",
            };
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
            Delete.Click += (btnSender, btnE) =>
            {
                comics.Remove(comic);
                dialog.Close();
                switchToSelectionPanel();
            };
            dialog.Form.Children.Add(coverButton);
            dialog.Form.Children.Add(label);
            dialog.Form.Children.Add(Delete);
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
                Image img, clone;
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
                    clone = cloneImage(img);
                    originalModifiedPairImages.Add(img, clone);
                }
                else
                {
                    img = comicImages[path];
                    clone = cloneImage(img);
                    originalModifiedPairImages[img] = clone;
                }
                return clone;
            }
        }
        private void NewComic(object sender, RoutedEventArgs e)
        {
            using (Forms.FolderBrowserDialog fdb = new Forms.FolderBrowserDialog())
            {
                fdb.Description = "Select a comic folder:";
                fdb.ShowNewFolderButton = false;
                switch (fdb.ShowDialog())
                {
                    case Forms.DialogResult.OK:
                        ComicImages c = new ComicImages(fdb.SelectedPath, Path.GetFileName(fdb.SelectedPath));
                        comics.Add(c);
                        ComicsWrapPanel.Children.Clear();
                        LoadComics();
                        break;
                    case Forms.DialogResult.Cancel:
                        break;
                    default:
                        MessageBox.Show("Error when adding comic!", "Error");
                        break;
                }
            }
        }
        private void NewComicPDF(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog ofd = new Forms.OpenFileDialog())
            {
                ofd.Filter = "PDF Files (*.pdf)|*.pdf|CBZ Files (*.cbz)|*.cbz";

                switch (ofd.ShowDialog())
                {
                    case Forms.DialogResult.OK:
                        ComicPDF c = new ComicPDF(ofd.FileName, Path.GetFileName(Regex.Replace(ofd.FileName, @"\.[^.\\]+$", "")));
                        comics.Add(c);
                        ComicsWrapPanel.Children.Clear();
                        LoadComics();
                        break;
                    case Forms.DialogResult.Cancel:
                        break;
                    default:
                        MessageBox.Show("Error when adding comic!", "Error");
                        break;
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
                Header = resourceManager.GetString("NewCatMessage1"),
                PromptText = resourceManager.GetString("NewCatMessage2"),
                Owner = this,
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
        // Pobieranie jezyka Systemowego
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
            // ResourceManager resourceManager = new ResourceManager("SharpReader.resources.Strings", typeof(ResourceLoader).Assembly);

            // Toolbar
            Comic.Header = resourceManager.GetString("Comic");
            Categories.Header = resourceManager.GetString("Categories");
            Language.Header = resourceManager.GetString("Language");
            New.Header = resourceManager.GetString("NewFolder");
            NewPDF.Header = resourceManager.GetString("NewPDF");
            NewCategory.Header = resourceManager.GetString("NewCategory");
            DelCategory.Header = resourceManager.GetString("DeleteCategory");
            Tools.Header = resourceManager.GetString("Tools");
            zoomIn.Header = resourceManager.GetString("ZoomIn");
            zoomOut.Header = resourceManager.GetString("ZoomOut");
            MirrorButton.Header = resourceManager.GetString("MirrorImages");
            ResetPreferences.Header = resourceManager.GetString("ResetPreferences");

            // Sidebar buttons
            HomeButton.TooltipText = resourceManager.GetString("HomeText");
            NavLeftButton.TooltipText = resourceManager.GetString("NavLeft");
            NavRightButton.TooltipText = resourceManager.GetString("NavRight");
            ChangeBackground.TooltipText = resourceManager.GetString("ChangeBackground");
            GridButton.TooltipText = resourceManager.GetString("GridLayout");
            ListButton.TooltipText = resourceManager.GetString("ListLayout");
            ScrollbarButton.TooltipText = resourceManager.GetString("Scrollbar");
            PageButton.TooltipText = resourceManager.GetString("Page");
            BrightnessUpButton.TooltipText = resourceManager.GetString("Lighten");
            BrightnessDownButton.TooltipText = resourceManager.GetString("Darken");
            BrightnessResetDown.TooltipText = resourceManager.GetString("ResetText");

            // Sidebar sections
            Options.Text = resourceManager.GetString("Options");
            Layout.Text = resourceManager.GetString("Layout");
            Reading_Mode.Text = resourceManager.GetString("ReadingMode");
            Filter.Text = resourceManager.GetString("Filters");

            if (isSystemThemeMode)
            {
                SystemBackground.TooltipText = resourceManager.GetString("SystemBackgroundTextOn");
            }
            else
            {
                SystemBackground.TooltipText = resourceManager.GetString("SystemBackgroundTextOff");
            }

            if (src == null)
            {
                StartScrollingButton.TooltipText = resourceManager.GetString("StartScrollingButtonLabelOff");
            }
            else
            {
                StartScrollingButton.TooltipText = resourceManager.GetString("StartScrollingButtonLabelOn");
            }

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
        private void replaceClones()
        {
            if (currentReadingMode == ReadingMode.PAGE)
            {
                if (currentImage.Source is BitmapImage bmpImage)
                {
                    currentImageClone.Source = changeBrigthness(bmpImage, brightness);
                }
            }
            else
            {
                foreach (var kvp in originalModifiedPairImages)
                {
                    if (kvp.Key.Source is BitmapImage bmpImage)
                    {
                        originalModifiedPairImages[kvp.Key].Source = changeBrigthness(bmpImage, brightness);
                    }
                    else
                    {
                        throw new Exception("Not BitmapImage");
                    }
                }
            }
        }
        private void BrightnessUpButton_Click(object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
            brightness += 100;
            replaceClones();
        }
        private void BrightnessDownButton_Click(Object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
            brightness -= 100;
            replaceClones();
        }
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            stopAutoScrolling();
            brightness = 0;
            replaceClones();
        }
        private void ZoomInMenuItem(object sender, RoutedEventArgs e)
        {
            ZoomIncrease();  // Przybliżenie
        }
        private void ZoomOutMenuItem(object sender, RoutedEventArgs e)
        {
            ZoomDecrease();  // Oddalanie
        }
        private void ZoomIncrease()
        {
            ApplyZoom(currentZoom + 1);
        }
        private void ZoomDecrease()
        {
            ApplyZoom(currentZoom - 1);
        }
        private void ApplyZoom(int zoomMod)
        {
            currentZoom = zoomMod;
            currentZoom = Math.Max(1, Math.Min(10, currentZoom)); // Zoom w zakresie [1, 10]
            currentZoomSquareSize = 120 - (10 * currentZoom);
            if (currentZoom > 1)
            {
                Mouse.SetCursor(Cursors.Cross);
                magnifyingGlassCanvas.Visibility = Visibility.Visible;
            }
            else
            {
                Mouse.SetCursor(Cursors.Hand);
                magnifyingGlassCanvas.Visibility = Visibility.Hidden;
            }
            UpdateMagnifyingGlass(null, null);
        }
        private void UpdateMagnifyingGlass(object sender, MouseEventArgs args)
        {
            if (currentMode == Mode.SELECTION)
            {
                return;
            }
            const double DistanceFromMouse = 5;

            var currentMousePosition = Mouse.GetPosition(this);

            if (ActualWidth - currentMousePosition.X > magnifyingGlassEllipse.Width + DistanceFromMouse)
            {
                Canvas.SetLeft(magnifyingGlassEllipse, currentMousePosition.X + DistanceFromMouse);
            }
            else
            {
                Canvas.SetLeft(magnifyingGlassEllipse,
                    currentMousePosition.X - DistanceFromMouse - magnifyingGlassEllipse.Width);
            }
            if (ActualHeight - currentMousePosition.Y > magnifyingGlassEllipse.Height + DistanceFromMouse)
            {
                Canvas.SetTop(magnifyingGlassEllipse,
                    currentMousePosition.Y + DistanceFromMouse);
            }
            else
            {
                Canvas.SetTop(magnifyingGlassEllipse,
                    currentMousePosition.Y - DistanceFromMouse - magnifyingGlassEllipse.Height);
            }
            myVisualBrush.Viewbox =
                new Rect(currentMousePosition.X - currentZoomSquareSize / 2,
                currentMousePosition.Y - currentZoomSquareSize / 2, currentZoomSquareSize, currentZoomSquareSize);
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
                isMouseDown = true;

                // Zapisujemy ostatnią pozycję myszy
                lastMousePosition = e.GetPosition(img);
            }
        }
        // Zdarzenie uruchamiane, gdy użytkownik przesuwa myszką po obrazie
        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown && sender is Image img)
            {
                Point currentMousePosition = e.GetPosition(img);
                Vector delta = currentMousePosition - lastMousePosition;

                if (img.RenderTransform is ScaleTransform transform)
                {
                    transform.CenterX -= delta.X;
                    transform.CenterY -= delta.Y;
                }

                lastMousePosition = currentMousePosition;
            }
        }
        // Zdarzenie uruchamiane, gdy użytkownik zwalnia przycisk myszy
        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image)
            {
                isMouseDown = false;
            }
        }
        public Image cloneImage(Image original)
        {
            if (original == null)
                return null;
            Image clone = new Image
            {
                Source = original.Source,
                Width = original.Width,
                MaxHeight = original.MaxHeight,
                RenderTransform = original.RenderTransform,
                RenderTransformOrigin = original.RenderTransformOrigin,
                Height = original.Height,
                Margin = original.Margin,
                HorizontalAlignment = original.HorizontalAlignment,
                VerticalAlignment = original.VerticalAlignment,
                Stretch = original.Stretch,
                ToolTip = original.ToolTip,
            };
            return clone;
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
            string caption = resourceManager.GetString("ResetMessage1");
            string messageBoxText = resourceManager.GetString("ResetMessage2");

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
            string userReport = "";
            if (!reportData.Sent)
            {
                ReportWindow reportWindow = new ReportWindow(reportData)
                {
                    Owner = this,
                };
                var reportResult = reportWindow.ShowDialog();
                if (reportResult != null && (bool)reportResult)
                {
                    userReport =
                        $"USER REPORT\n" +
                        $"Subject: {reportData.Subject}\n" +
                        $"Description: {reportData.Description}";
                }
            }
            if (_isClosingHandled)
                return;

            _isClosingHandled = true;

            string messageBoxText = "Do you want to save changes?";
            string caption = "Quitting SharpReader";
            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;

            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                _isClosingHandled = false;
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
            // For now don't send
            if (1 > 2)
            {
                if (AppSettings.Default.allowDataCollection == true)
                {
                    await SlackLoger.SendMessageAsync(report);
                }
                if (!string.IsNullOrWhiteSpace(userReport) && reportData.Sent)
                {
                    await SlackLoger.SendMessageAsync(userReport);
                }
            }
            Application.Current.Shutdown();
        }
        private void DelCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ComicSettings
            {
                Owner = this,
            };
            dialog.Form.Children.Add(new TextBlock { Text = "Category", FontWeight = FontWeights.Bold, });
            dialog.Save.Content = "Delete";
            List<RadioButton> radioButtons = new List<RadioButton>();
            categories.ForEach((name) =>
            {
                if (name == "Other")
                {
                    return;
                }
                RadioButton rb = new RadioButton
                {
                    Name = name,
                    Content = name,
                };
                radioButtons.Add(rb);
                dialog.Form.Children.Add(rb);
            });
            bool result = dialog.ShowDialog().Value;
            if (result)
            {
                for (int i = 0; i < radioButtons.Count; ++i)
                {
                    if (radioButtons[i].IsChecked == true)
                    {
                        comics.ForEach((comic) =>
                        {
                            if (comic.Category == radioButtons[i].Name)
                            {
                                comic.Category = "Other";
                            }
                        });

                        categories.Remove(radioButtons[i].Name);
                        if (currentMode == Mode.SELECTION)
                            switchToSelectionPanel();
                        break;
                    }
                }
            }
        }
    }
}
