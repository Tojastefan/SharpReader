using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SharpReader
{
    public partial class MainWindow : Window
    {
        // Kolory dla ciemnego i jasnego motywu
        private readonly Brush darkBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232323"));
        private readonly Brush darkPanelBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c3c3c"));
        private readonly Brush lightBackground = Brushes.White;
        private readonly Brush lightPanelBackground = Brushes.LightGray;
        private readonly Brush darkText = Brushes.White;
        private readonly Brush lightText = Brushes.Black;
        Color darkSidebar = (Color)ColorConverter.ConvertFromString("#3c3c3c");
        Color lightSidebar = (Color)ColorConverter.ConvertFromString("#F5F5F5");
        private bool isDarkMode = true;


        public MainWindow()
        {
            InitializeComponent();
            LoadComics();
        }

        private void LoadComics()
        {
            for (int i = 0; i < 30; i++)
            {
                Border comicBorder = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(2),
                    Width = 100,
                    Height = 150,
                    Margin = new Thickness(5),
                    Background = Brushes.White
                };

                StackPanel comicPanel = new StackPanel();
                Image comicImage = new Image
                {
                    Width = 90,
                    Height = 100,
                    Margin = new Thickness(5),
                    Source = new BitmapImage(new Uri("https://via.placeholder.com/90x100", UriKind.Absolute))
                };

                TextBlock comicTitle = new TextBlock
                {
                    Text = "Comic " + (i + 1),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                comicPanel.Children.Add(comicImage);
                comicPanel.Children.Add(comicTitle);

                comicBorder.Child = comicPanel;
                ComicsWrapPanel.Children.Add(comicBorder);
            }
        }

        private void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdzenie, czy obecny kolor tła to ciemny
            if (MainGrid.Background is SolidColorBrush brush && brush.Color == (Color)ColorConverter.ConvertFromString("#232323"))
            {
                // Zmiana na jasny motyw
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
                        textBlock.Foreground = Brushes.Black;
                    }
                }

                // Zmiana koloru menu na jasny
                foreach (var item in MainMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3"));
                        menuItem.Foreground = Brushes.Black;
                    }
                }
            }
            else
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
                        button.Foreground = Brushes.White;
                    }
                    else if (child is TextBlock textBlock)
                    {
                        textBlock.Foreground = Brushes.White;
                    }
                }

                // Zmiana koloru menu na ciemny
                foreach (var item in MainMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c3c3c"));
                        menuItem.Foreground = Brushes.White;
                    }
                }
            }
        }


        private void GridLayout_Click(object sender, RoutedEventArgs e)
        {
            ComicsWrapPanel.ItemHeight = 150;
            ComicsWrapPanel.ItemWidth = 100;
        }

        private void ListLayout_Click(object sender, RoutedEventArgs e)
        {
            ComicsWrapPanel.ItemHeight = 150;
            ComicsWrapPanel.ItemWidth = 300;
        }

    }
}
