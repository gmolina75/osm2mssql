using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using osm2mssql.Importer.Enums;
using osm2mssql.Importer.Tasks;

namespace osm2mssql.Importer.Views
{
    public partial class ImportSummaryView : Window
    {
        public ImportSummaryView(TimeSpan totalDuration, IEnumerable<TaskBase> tasks)
        {
            InitializeComponent();
            TotalDurationText.Text = string.Format(osm2mssql.Importer.Languages.Language.CurrentLanguage["ImportTotalDuration"],
                                                   totalDuration.ToString(@"hh\:mm\:ss"));

            foreach (var task in tasks)
            {
                var failed = task.Result == TaskResult.Error;
                DetailsText.Inlines.Add(new Run(task.Name)
                {
                    Foreground = (Brush)FindResource("TextPrimaryBrush")
                });
                DetailsText.Inlines.Add(new Run("   " + task.Duration.ToString(@"hh\:mm\:ss"))
                {
                    Foreground = (Brush)FindResource("TextMutedBrush")
                });
                DetailsText.Inlines.Add(new Run("   " + osm2mssql.Importer.Languages.Language.CurrentLanguage[failed ? "TaskStatusFailed" : "TaskStatusCompleted"])
                {
                    Foreground = (Brush)FindResource(failed ? "ErrorBrush" : "SuccessBrush")
                });
                DetailsText.Inlines.Add(new LineBreak());
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
