using System.Windows;

namespace osm2mssql.Importer.Views
{
    public partial class ConfirmReplaceDatabaseView : Window
    {
        public ConfirmReplaceDatabaseView(string databaseName)
        {
            InitializeComponent();
            WarningText.Text = string.Format(osm2mssql.Importer.Languages.Language.CurrentLanguage["ReplaceDatabaseWarning"], databaseName);
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
