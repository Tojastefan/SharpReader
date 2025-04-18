﻿using System.Windows;
using System.Windows.Input;

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
