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

namespace SPID
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            GetMethods(typeof(StaticClass), ref listBox);
            if (!System.IO.Directory.Exists(StaticClass.WordList + "\\.."))
            {
                MessageBox.Show("Please make sure LINKS is initialized atleast once before using any of these functions.");
            }
        }

        static void GetMethods(Type type, ref ListBox lbox)
        {
            foreach (var method in type.GetMethods())
            {                
                if (method.Attributes.HasFlag(System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.Public) &&
                    (method.ReturnType == typeof(string) || method.ReturnType == typeof(Task<string>)))
                {
                    var parameters = method.GetParameters();
                    var parameterDescriptions = string.Join
                        (",", method.GetParameters()
                                     .Select(x => "\"" + x.Name + "\"")
                                     .ToArray());

                    lbox.Items.Add(string.Format("[" + (type.Assembly.GetName()).Name + "."+ type.Name + ".{1}({2})]",
                                      method.ReturnType,
                                      method.Name,
                                      parameterDescriptions));
                }
            }
        }

        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            System.Windows.Forms.Clipboard.SetText(listBox.SelectedValue.ToString());
            status.Content = "Function copied to clipboard.";
        }
    }
}
