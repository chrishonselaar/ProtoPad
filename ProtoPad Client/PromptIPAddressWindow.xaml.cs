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
        public static readonly DependencyProperty IPAddressProperty = DependencyProperty.Register("Text", typeof(String), typeof(TextBox), new FrameworkPropertyMetadata(""));
        public string IPAddress
        {
            get { return Convert.ToString(GetValue(IPAddressProperty)); }
            set { SetValue(IPAddressProperty, value); }
        }

        public static readonly DependencyProperty PortProperty = DependencyProperty.Register("Text", typeof(String), typeof(TextBox), new FrameworkPropertyMetadata(""));
        public int Port
        {
            get { return int.Parse(Convert.ToString(GetValue(PortProperty))); }
            set { SetValue(PortProperty, value); }
        }

        public PromptIPAddressWindow()
        {
            InitializeComponent();
        }
    }

    public class CheckFilledConverter: IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.All(v => (v is string) && (!String.IsNullOrWhiteSpace(v.ToString())));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}