using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using osm2mssql.Importer.Classes.CommandLine;
using osm2mssql.Importer.Enums;
using osm2mssql.Importer.Model;
using osm2mssql.Importer.Tasks;
using osm2mssql.Importer.ViewModel;
using osm2mssql.Library;

namespace osm2mssql.Importer.Classes
{
    /// <summary>
    /// Ejecuta la importación en modo headless (sin ventana WPF) a partir de
    /// los argumentos de línea de comandos. Construye el mismo pipeline de
    /// tareas que la interfaz gráfica (TaskRunner) y reporta progreso a consola
    /// y siempre a archivo de log.
    /// </summary>
    internal static class CliImportRunner
    {
        private const string SettingsFile = "osm2mssql.xml";
        private const string DefaultLogFile = "OsmCliLog.txt";

        private static readonly string[] AllTaskNames =
        {
            "CreateDatabase", "InstallDbExtension",
            "NodeReader", "WayReader", "RelationReader",
            "NodeIndices", "WayIndices", "RelationIndices",
            "AttributeWriter", "CreateLineInDB", "CreateRelationInDB",
            "CreateSpatialIndices", "ExecuteSqlCommands"
        };

        private static readonly Dictionary<string, string[]> TaskGroups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "init", new[] { "CreateDatabase", "InstallDbExtension" } },
            { "reader", new[] { "NodeReader", "WayReader", "RelationReader" } },
            { "indices", new[] { "NodeIndices", "WayIndices", "RelationIndices" } },
            { "attribute", new[] { "AttributeWriter" } },
            { "line", new[] { "CreateLineInDB" } },
            { "relation", new[] { "CreateRelationInDB" } },
            { "spatial", new[] { "CreateSpatialIndices" } },
            { "sql", new[] { "ExecuteSqlCommands" } }
        };

        public static async Task<int> RunAsync(string[] args)
        {
            AttachConsoleIfNeeded();

            var options = CliOptions.Parse(args);
            ConfigureTrace(options.LogFile);

            if (options.Help)
            {
                WriteLine(CliOptions.Usage);
                Trace.WriteLine("CLI: se mostró la ayuda (--help).");
                return (int)CliExitCode.InvalidArguments;
            }
            if (options.Error != null)
            {
                WriteError(options.Error);
                WriteLine(string.Empty);
                WriteLine(CliOptions.Usage);
                return (int)CliExitCode.InvalidArguments;
            }

            if (string.IsNullOrWhiteSpace(options.OsmFile))
            {
                WriteError("Falta el parámetro obligatorio --file <archivo.osm|.osm.pbf>.");
                WriteLine(string.Empty);
                WriteLine(CliOptions.Usage);
                return (int)CliExitCode.InvalidArguments;
            }

            var fileError = ValidateInputFile(options.OsmFile);
            if (fileError != null)
            {
                WriteError(fileError);
                return (int)CliExitCode.InvalidInputFile;
            }

            ImporterModel model;
            var modelError = BuildModel(options, out model);
            if (modelError != null)
            {
                WriteError(modelError);
                return (int)CliExitCode.InvalidArguments;
            }

            HashSet<string> selectedTasks;
            string tasksError;
            if (!TrySelectTasks(options, out selectedTasks, out tasksError))
            {
                WriteError(tasksError);
                return (int)CliExitCode.InvalidArguments;
            }

            var osmFullPath = Path.GetFullPath(options.OsmFile);
            WriteLine("osm2mssql CLI - importación headless");
            WriteLine("  Archivo: " + osmFullPath);
            WriteLine("  Destino: " + model.Host + " / " + model.Database);
            WriteLine("  Autenticación: " + (string.IsNullOrEmpty(model.Username) ? "Windows integrada" : "SQL (" + model.Username + ")"));
            Trace.WriteLine(string.Format("CLI start - archivo: {0}, destino: {1}/{2}", osmFullPath, model.Host, model.Database));

            var connectionStringBuilder = new OsmConnectionStringBuilder().CreateSqlConnectionStringBuilder(model);
            var availability = await new SqlDbAvailableChecker().CheckDatabaseAvailability(connectionStringBuilder);
            if (availability == ConnectionResult.Error)
            {
                WriteError(string.Format("No se pudo conectar a SQL Server '{0}'. Revisa el servidor, las credenciales y la red.", model.Host));
                return (int)CliExitCode.ConnectionError;
            }
            if (availability == ConnectionResult.DbAlreadyExists && !options.Replace)
            {
                WriteError(string.Format("La base de datos '{0}' ya existe en '{1}'.", model.Database, model.Host));
                WriteError("La importación es destructiva: vuelve a lanzar el comando añadiendo --replace para eliminarla y recrearla, o elige otro nombre con --database.");
                return (int)CliExitCode.DatabaseExistsWithoutReplace;
            }

            using (var runner = new TaskRunner())
            {
                runner.FillTaskList();
                if (selectedTasks != null)
                {
                    foreach (var task in runner.Tasks)
                        task.IsEnabled = selectedTasks.Contains(GetTaskShortName(task));
                }
                AttachTaskObservers(runner.Tasks);

                WriteLine("Iniciando importación...");
                var watch = Stopwatch.StartNew();
                try
                {
                    await runner.RunTasks(connectionStringBuilder, osmFullPath);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
                watch.Stop();

                var failedTask = runner.Tasks.FirstOrDefault(t => t.IsEnabled && t.Result == TaskResult.Error);
                if (failedTask != null)
                {
                    WriteError(string.Format("Falló la tarea '{0}': {1}",
                        failedTask.Name, failedTask.LastError != null ? failedTask.LastError.Message : "error desconocido"));
                    return (int)CliExitCode.ImportFailed;
                }

                WriteLine(string.Format("[{0}] Importación completada correctamente en {1}.",
                    DateTime.Now.ToString("HH:mm:ss"), FormatDuration(watch.Elapsed)));
                return (int)CliExitCode.Success;
            }
        }

        private static string ValidateInputFile(string osmFile)
        {
            var extension = Path.GetExtension(osmFile).ToLowerInvariant();
            if (extension != ".osm" && extension != ".pbf")
            {
                return string.Format("Extensión de archivo no soportada '{0}'. Se espera un archivo .osm o .osm.pbf.", extension);
            }
            if (!File.Exists(osmFile))
            {
                return "No existe el archivo de entrada: " + Path.GetFullPath(osmFile);
            }
            return null;
        }

        private static string BuildModel(CliOptions options, out ImporterModel model)
        {
            model = null;
            if (File.Exists(SettingsFile))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(ImporterModel));
                    using (var file = File.OpenRead(SettingsFile))
                    {
                        model = (ImporterModel)serializer.Deserialize(file);
                    }
                }
                catch
                {
                    // Configuración de la UI ilegible: se ignora y se parte de un modelo vacío
                }
            }
            if (model == null)
                model = new ImporterModel();

            if (!string.IsNullOrWhiteSpace(options.Host))
                model.Host = options.Host;
            if (!string.IsNullOrWhiteSpace(options.Database))
                model.Database = options.Database;

            if (options.Trusted && (!string.IsNullOrEmpty(options.User) || !string.IsNullOrEmpty(options.Password)))
                return "No mezcles --trusted con --user/--password: elige autenticación integrada de Windows o autenticación SQL.";

            if (options.Trusted)
            {
                model.Username = null;
                model.Password = null;
            }
            else if (!string.IsNullOrEmpty(options.User))
            {
                if (string.IsNullOrEmpty(options.Password))
                    return "Falta --password cuando se usa --user.";
                model.Username = options.User;
                model.Password = options.Password;
            }

            // En modo CLI nunca se guarda el modelo: osm2mssql.xml sigue siendo
            // propiedad exclusiva de la interfaz gráfica.

            if (string.IsNullOrWhiteSpace(model.Host))
                return "Falta el servidor SQL: usa --host o guarda la configuración desde la interfaz gráfica (osm2mssql.xml).";
            if (string.IsNullOrWhiteSpace(model.Database))
                return "Falta la base de datos: usa --database o guarda la configuración desde la interfaz gráfica (osm2mssql.xml).";
            return null;
        }

        private static bool TrySelectTasks(CliOptions options, out HashSet<string> selectedTasks, out string error)
        {
            selectedTasks = null;
            error = null;
            if (options.TaskKeys == null || options.TaskKeys.Count == 0)
                return true;

            selectedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawKey in options.TaskKeys)
            {
                var key = rawKey.Trim();
                if (key.Length == 0)
                    continue;
                if (key.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var name in AllTaskNames)
                        selectedTasks.Add(name);
                    continue;
                }
                string[] group;
                if (TaskGroups.TryGetValue(key, out group))
                {
                    foreach (var name in group)
                        selectedTasks.Add(name);
                    continue;
                }
                if (AllTaskNames.Any(n => n.Equals(key, StringComparison.OrdinalIgnoreCase)))
                {
                    selectedTasks.Add(key);
                    continue;
                }
                error = "Tarea desconocida en --tasks: '" + key + "'. Válidas: all, init, reader, indices, attribute, line, relation, spatial, sql o nombres individuales (" + string.Join(", ", AllTaskNames) + ").";
                return false;
            }
            return true;
        }

        private static string GetTaskShortName(TaskBase task)
        {
            var name = task.GetType().Name;
            return name.StartsWith("Task", StringComparison.Ordinal) ? name.Substring(4) : name;
        }

        private static void AttachTaskObservers(IEnumerable<TaskBase> tasks)
        {
            foreach (var task in tasks)
            {
                if (!task.IsEnabled)
                    continue;
                task.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != "Result")
                        return;
                    var t = (TaskBase)s;
                    if (t.Result == TaskResult.InProgress)
                        WriteLine(string.Format("[{0}] {1} ... iniciada", DateTime.Now.ToString("HH:mm:ss"), t.Name));
                    else if (t.Result == TaskResult.Successful)
                        WriteLine(string.Format("[{0}] {1} ... completada ({2})", DateTime.Now.ToString("HH:mm:ss"), t.Name, FormatDuration(t.Duration)));
                    else if (t.Result == TaskResult.Error)
                        WriteLine(string.Format("[{0}] {1} ... ERROR ({2})", DateTime.Now.ToString("HH:mm:ss"), t.Name, FormatDuration(t.Duration)));
                };
            }
        }

        private static void ConfigureTrace(string logFile)
        {
            try
            {
                foreach (var listener in Trace.Listeners.OfType<OsmTextWriterTraceListener>().ToList())
                    Trace.Listeners.Remove(listener);
                Trace.Listeners.Add(new OsmTextWriterTraceListener(string.IsNullOrEmpty(logFile) ? DefaultLogFile : logFile));
                if (!Trace.Listeners.OfType<CliConsoleTraceListener>().Any())
                    Trace.Listeners.Add(new CliConsoleTraceListener());
                Trace.AutoFlush = true;
            }
            catch (Exception ex)
            {
                WriteError("No se pudo inicializar el archivo de log: " + ex.Message);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        private static void AttachConsoleIfNeeded()
        {
            try
            {
                // ATTACH_PARENT_PROCESS: reutiliza la consola desde la que se lanzó.
                if (AttachConsole(-1))
                    return;
                // Sin consola padre: si la salida está redirigida (tarea programada
                // con > log.txt) basta con escribir a Console.Out; si no, se crea una.
                if (!Console.IsOutputRedirected)
                    AllocConsole();
            }
            catch
            {
                // Nunca debe impedir la ejecución
            }
        }

        private static void WriteLine(string message)
        {
            try
            {
                Console.Out.WriteLine(message);
            }
            catch
            {
                // Nunca debe impedir la ejecución
            }
        }

        private static void WriteError(string message)
        {
            try
            {
                Console.Error.WriteLine(message);
            }
            catch
            {
                // Nunca debe impedir la ejecución
            }
            Trace.WriteLine("CLI ERROR: " + message);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return string.Format("{0:00}:{1:00}:{2:00}", (int)duration.TotalHours, duration.Minutes, duration.Seconds);
        }
    }
}
