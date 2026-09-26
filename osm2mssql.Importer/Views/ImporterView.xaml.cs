using System.Windows;
using System.Windows.Controls;
using osm2mssql.Importer.ViewModel;

namespace osm2mssql.Importer.Views
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = DataContext as ImporterViewModel;
            if (viewModel != null)
                osm2mssql.Importer.Languages.Language.SetLanguage(viewModel.Model.Language);
        }

        private void DatabaseName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var viewModel = DataContext as ImporterViewModel;
            if (viewModel != null)
                viewModel.ResetConnectionResult();
        }
    }
}
