using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SharpReader
{
    /// <summary>
    /// Logika interakcji dla klasy Dialog.xaml
    /// </summary>
    public partial class Dialog : Window
    {
        public string InputText { get; set; }
        public string Header {
            set
            {
                HeaderTitle.Title = value;
            }
        }
        public string PromptText
        {
            set
            {
                Prompt.Text = value;
            }
        }
        public Dialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
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
