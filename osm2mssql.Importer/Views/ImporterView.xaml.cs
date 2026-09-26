using System;
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
            DataContext = Resources["vm"];
            Loaded += MainWindow_Loaded;

            var viewModel = DataContext as ImporterViewModel;
            if (viewModel != null)
                DbPasswordBox.Password = viewModel.Model.Password ?? string.Empty;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ImporterViewModel;
            if (viewModel == null)
                return;

            viewModel.LogRows.CollectionChanged += (o, args) =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (LogList.Items.Count > 0)
                        LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
                }));
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

        private void DbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ImporterViewModel;
            if (viewModel != null && viewModel.Model.Password != DbPasswordBox.Password)
                viewModel.Model.Password = DbPasswordBox.Password;
        }
    }
}
