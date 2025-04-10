using System;
using System.Windows;
using System.Windows.Controls;
using static SharpReader.ReportData;

namespace SharpReader
{
    public partial class ReportWindow : Window
    {
        private ReportData data;
        public ReportWindow(ReportData data)
        {
            InitializeComponent();
            this.data = data;
            foreach (var subject in Enum.GetValues(typeof(SubjectType)))
            {
                ComboBoxItem item = new ComboBoxItem
                {
                    Content = subject,
                    IsSelected = subject.Equals(SubjectType.OTHER) ? true : false,
                };
                SubjectSelect.Items.Add(item);
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            data.Description = Description.Text;
            if (string.IsNullOrWhiteSpace(data.Description))
                return;
            DialogResult = true;
            data.Sent = true;
            data.Subject = (SubjectType)Enum.Parse(typeof(SubjectType), SubjectSelect.Text);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
