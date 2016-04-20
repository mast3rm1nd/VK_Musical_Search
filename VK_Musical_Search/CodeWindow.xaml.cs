using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Text.RegularExpressions;

namespace VK_Musical_Search
{
    /// <summary>
    /// Interaction logic for CodeWindow.xaml
    /// </summary>
    public partial class CodeWindow : Window
    {
        public CodeWindow()
        {
            InitializeComponent();
        }

        private void Confirm_button_Click(object sender, RoutedEventArgs e)
        {
            var regex = @"(?<!\d)\d{6}(?!\d)";

            if (Regex.IsMatch(Code_textBox.Text, regex))
            {
                Globals.Code = Code_textBox.Text;

                this.Close();
            }
                
            else
            {
                MessageBox.Show("Код должен состоять из шести цифр", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                Globals.Code = "";
            }
        }
    }
}
