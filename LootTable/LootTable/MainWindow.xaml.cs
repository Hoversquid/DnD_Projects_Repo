using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LootTable
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MagicItemTable itemTable;
        public MainWindow()
        {
            InitializeComponent();
            itemTable = new MagicItemTable();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Clears from last run
            itemTable.ClearItem();

            // This part sets the initial conditions, will need to be replaced with more dynamic input system like UI.
            itemTable.ItemVars.Element("Item_Category").Value = "Medium";
            itemTable.ItemVars.Element("Roll_Type").Value = "Treasure";


            itemTable.DefaultRollOnTable();
            ItemVarDisplay.Text = itemTable.ItemVars.ToString();
        }
    }
}
