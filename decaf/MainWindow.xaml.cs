using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace decaf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GridLength _previousWidth = new GridLength(250.0);
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                var d = new DECAF(true);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.ToString());
            }
        }

        private void AddComponentClick(object sender, RoutedEventArgs e)
        {
            componentList.Items.Add(GetEquivColumn(componentList.Items.Count));
        }

        private void TheSplitterDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var myDoubleAnimation = new GridLengthAnimation {From = leftColumn.Width};

            if (leftColumn.Width.Value - 60 < 0)
            {
                myDoubleAnimation.To = _previousWidth;
            }
            else
            {
                myDoubleAnimation.To = new GridLength(0.0);
                _previousWidth = leftColumn.Width;
            }
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5));
            myDoubleAnimation.AccelerationRatio = 0.95;
            leftColumn.BeginAnimation(ColumnDefinition.WidthProperty, myDoubleAnimation);
        }

        public String GetEquivColumn(int number)
        {
            String converted = "";
            while (number >= 0)
            {
                int remainder = number % 26;
                converted = (char)(remainder + 'A') + converted;
                number = (number / 26) - 1;
            }

            return converted;
        }

        private void ComponentListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (componentList.SelectedIndex == -1)
            {
                propertiesBox.IsEnabled = false;
            }
            else
            {
                propertiesBox.IsEnabled = true;
                componentName.Text = componentList.SelectedItem.ToString();
            }
        }

        private void ComponentNameLostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => componentName.Focus()));
        }

        private void ComponentReqLostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => componentReq.Focus()));
        }

        private void ComponentRedLostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => componentRed.Focus()));
        }
    }
}
