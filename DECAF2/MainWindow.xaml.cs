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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace decaf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GridLength previousWidth;
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                DECAF d = new DECAF();
            }
            catch (Exception err)
            {
                MessageBox.Show(err.ToString());
            }
        }

        private void addComponent_Click(object sender, RoutedEventArgs e)
        {
            this.componentList.Items.Add(getEquivColumn(this.componentList.Items.Count));
        }

        private void theSplitter_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            //this.leftColumn.Width = new GridLength(250);
            GridLengthAnimation myDoubleAnimation = new GridLengthAnimation();
            myDoubleAnimation.From = this.leftColumn.Width;

            if (this.leftColumn.Width.Value == 5.0)
            {
                myDoubleAnimation.To = previousWidth;
            }
            else
            {
                myDoubleAnimation.To = new GridLength(0.0);
                previousWidth = this.leftColumn.Width;
            }
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5));
            myDoubleAnimation.AccelerationRatio = 0.95;
            leftColumn.BeginAnimation(ColumnDefinition.WidthProperty, myDoubleAnimation);
        }

        public String getEquivColumn(int number)
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

        private void componentList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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

        private void componentName_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => componentName.Focus()));
        }

        private void componentReq_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => componentReq.Focus()));
        }

        private void componentRed_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => componentRed.Focus()));
        }
    }
}
