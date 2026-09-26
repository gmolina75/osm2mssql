using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using System.Web.Routing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using osm2mssql.Importer.Classes;
using osm2mssql.Importer.Enums;
using osm2mssql.Importer.Languages;
using osm2mssql.Importer.Model;
using osm2mssql.Importer.Tasks;
using osm2mssql.Importer.Views;
using osm2mssql.Library.OpenStreetMapTypes;
using osm2mssql.Library.OsmReader;

namespace osm2mssql.Importer.ViewModel
{
    public class ImporterViewModel : ViewModelBase
    {
        public ICommand TryToConnect { get { return new RelayCommand(TryConnectingToDatabase); } }
        public ICommand StartImport { get { return new RelayCommand(StartImporting); } }

        public ConnectionResult LastTryConnectionResult
        {
            get { return _lastTryConnectionResult; }
            set
            {
                _lastTryConnectionResult = value;
                RaisePropertyChanged();
            }
        }

        public bool IsNotProcessing
        {
            get { return _isNotProcessing; }
            set
            {
                _isNotProcessing = value;
                RaisePropertyChanged();
            }
        }

        public IEnumerable<TaskBase> Tasks { get { return _runner.Tasks; } }
        public ImporterModel Model { get; set; }
        public ObservableCollection<string> LogRows { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> AvailableLanguages
        {
            get
            {
                return new[]
                {
                    new KeyValuePair<string, string>(Language.English, "English"),
                    new KeyValuePair<string, string>(Language.Spanish, "Español"),
                };
            }
        }

        private bool _isNotProcessing = true;
        private ConnectionResult _lastTryConnectionResult;

        private readonly TaskRunner _runner = new TaskRunner();
        private readonly SqlDbAvailableChecker _dbChecker = new SqlDbAvailableChecker();
        private readonly OsmConnectionStringBuilder _connStringBuilder = new OsmConnectionStringBuilder();
        private const string ViewModelSettings = "osm2mssql.xml";

        public ImporterViewModel()
        {
            Model = LoadModelFromFile<ImporterModel>(ViewModelSettings);
            if (!AvailableLanguages.Any(x => x.Key == Model.Language))
                Model.Language = Language.English;
            var dispatcher = Dispatcher.CurrentDispatcher;
            LogRows = new ObservableCollection<string>();
            if (!Trace.Listeners.OfType<WpfTraceListener>().Any())
            {
                Trace.Listeners.Add(new WpfTraceListener(x => dispatcher.BeginInvoke(new Action(() => LogRows.Add(x)))));
            }
            dispatcher.ShutdownStarted += (o, e) => SaveModelToFile<ImporterModel>(ViewModelSettings, Model);
            _runner.FillTaskList();
        }

        private async void TryConnectingToDatabase()
        {
            LastTryConnectionResult = await _dbChecker.CheckDatabaseAvailability(_connStringBuilder.CreateSqlConnectionStringBuilder(Model));
        }

        public void ResetConnectionResult()
        {
            LastTryConnectionResult = ConnectionResult.Unknown;
        }

        private async void StartImporting()
        {
            try
            {
                IsNotProcessing = false;
                LastTryConnectionResult = await _dbChecker.CheckDatabaseAvailability(_connStringBuilder.CreateSqlConnectionStringBuilder(Model));

                if (LastTryConnectionResult == ConnectionResult.DbAlreadyExists)
                {
                    var confirmView = new ConfirmReplaceDatabaseView(Model.Database)
                    {
                        Owner = Application.Current != null ? Application.Current.MainWindow : null
                    };
                    if (confirmView.ShowDialog() != true)
                        return;
                }

                if (LastTryConnectionResult != ConnectionResult.DbAlreadyExists &&
                    LastTryConnectionResult != ConnectionResult.Successful)
                    return;

                var fd = new OpenFileDialog();
                fd.Filter = "OpenStreetMap Files (*.xml, *.pbf)|*.xml;*.pbf|All files (*.*)|*.*";
                if (!string.IsNullOrEmpty(Model.LastImportDirectory) && Directory.Exists(Model.LastImportDirectory))
                    fd.InitialDirectory = Model.LastImportDirectory;
                if (fd.ShowDialog() != true)
                    return;
                Model.LastImportDirectory = Path.GetDirectoryName(fd.FileName);
                var con = _connStringBuilder.CreateSqlConnectionStringBuilder(Model);
                await RunImportWithFeedback(con, fd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsNotProcessing = true;
            }
        }

        private async Task RunImportWithFeedback(SqlConnectionStringBuilder con, string fileName)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await _runner.RunTasks(con, fileName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            watch.Stop();

            var failedTask = _runner.Tasks.FirstOrDefault(x => x.Result == TaskResult.Error);
            if (failedTask != null)
            {
                System.Media.SystemSounds.Hand.Play();
                var message = string.Format(Language.CurrentLanguage["ImportFailedMessage"],
                                            failedTask.Name, failedTask.LastError != null ? failedTask.LastError.Message : string.Empty);
                MessageBox.Show(message, Language.CurrentLanguage["ImportFailedTitle"],
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            System.Media.SystemSounds.Asterisk.Play();
            var summaryView = new ImportSummaryView(watch.Elapsed, _runner.Tasks.Where(x => x.IsEnabled))
            {
                Owner = Application.Current != null ? Application.Current.MainWindow : null
            };
            summaryView.ShowDialog();
        }


    }
}
