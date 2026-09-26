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
            Closing += MainWindow_Closing;

            var viewModel = DataContext as ImporterViewModel;
            if (viewModel != null)
            {
                DbPasswordBox.Password = viewModel.Model.Password ?? string.Empty;
                RestoreWindowSettings(viewModel.Model);
            }
        }

        private void RestoreWindowSettings(Model.ImporterModel model)
        {
            try
            {
                if (model.WindowWidth >= MinWidth && model.WindowHeight >= MinHeight
                    && model.WindowWidth <= SystemParameters.VirtualScreenWidth
                    && model.WindowHeight <= SystemParameters.VirtualScreenHeight)
                {
                    Width = model.WindowWidth;
                    Height = model.WindowHeight;
                }

                if (model.WindowLeft > SystemParameters.VirtualScreenLeft - model.WindowWidth
                    && model.WindowTop > SystemParameters.VirtualScreenTop - model.WindowHeight
                    && model.WindowLeft < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
                    && model.WindowTop < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = model.WindowLeft;
                    Top = model.WindowTop;
                }
            }
            catch
            {
                // Geometría no válida: se mantiene el valor por defecto de la ventana
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var viewModel = DataContext as ImporterViewModel;
            if (viewModel == null)
                return;
            try
            {
                var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    viewModel.Model.WindowLeft = bounds.Left;
                    viewModel.Model.WindowTop = bounds.Top;
                    viewModel.Model.WindowWidth = bounds.Width;
                    viewModel.Model.WindowHeight = bounds.Height;
                }
            }
            catch
            {
                // No se pudo capturar la geometría: se conserva la última conocida
            }
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
