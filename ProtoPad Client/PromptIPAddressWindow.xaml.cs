using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProtoPad_Client
{
    public partial class PromptIPAddressWindow
    {
        public PromptIPAddressWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void IPAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateOkButton();
        }

        private void PortTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateOkButton();
        }

        private void UpdateOkButton()
        {
            OkButton.IsEnabled = !(String.IsNullOrWhiteSpace(IPAddressTextBox.Text) || String.IsNullOrWhiteSpace(PortTextBox.Text));
        }
    }
}