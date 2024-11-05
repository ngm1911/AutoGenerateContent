using System.Windows;
using System.Windows.Controls;

namespace AutoGenerateContent.CustomControl
{
    /// <summary>
    /// Interaction logic for CTextBox.xaml
    /// </summary>
    public partial class CTextBox : UserControl
    {
        public CTextBox()
        {
            InitializeComponent();
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(CTextBox), new PropertyMetadata(string.Empty));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(CTextBox), new PropertyMetadata(string.Empty));

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            CustomTextBox.Width = Width; 
            CustomTextBox.Height = Height; 
        }
    }
}
