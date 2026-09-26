using System;
using System.Collections.Generic;

namespace osm2mssql.Importer.Classes.CommandLine
{
    public class CliOptions
    {
        public string OsmFile { get; set; }
        public string Host { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public bool Trusted { get; set; }
        public bool Replace { get; set; }
        public bool Help { get; set; }
        public string LogFile { get; set; }
        public List<string> TaskKeys { get; private set; }
        public string Error { get; set; }

        public CliOptions()
        {
            TaskKeys = new List<string>();
        }

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "-h" || arg == "-?" || arg == "/?")
                {
                    options.Help = true;
                    continue;
                }

                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    if (options.OsmFile == null)
                    {
                        options.OsmFile = arg;
                        continue;
                    }
                    SetError(options, "Argumento posicional inesperado: '" + arg + "'. Usa --file para indicar el archivo.");
                    continue;
                }

                var token = arg.Substring(2);
                string inlineValue = null;
                var equalsIndex = token.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    inlineValue = token.Substring(equalsIndex + 1);
                    token = token.Substring(0, equalsIndex);
                }

                switch (token.ToLowerInvariant())
                {
                    case "help":
                        options.Help = true;
                        break;
                    case "trusted":
                        options.Trusted = true;
                        break;
                    case "replace":
                        options.Replace = true;
                        break;
                    case "file":
                        options.OsmFile = RequireValue(args, ref i, inlineValue, options, "--file");
                        break;
                    case "host":
                        options.Host = RequireValue(args, ref i, inlineValue, options, "--host");
                        break;
                    case "database":
                        options.Database = RequireValue(args, ref i, inlineValue, options, "--database");
                        break;
                    case "user":
                        options.User = RequireValue(args, ref i, inlineValue, options, "--user");
                        break;
                    case "password":
                        options.Password = RequireValue(args, ref i, inlineValue, options, "--password");
                        break;
                    case "logfile":
                        options.LogFile = RequireValue(args, ref i, inlineValue, options, "--logfile");
                        break;
                    case "tasks":
                        var tasksValue = RequireValue(args, ref i, inlineValue, options, "--tasks");
                        if (tasksValue != null)
                        {
                            options.TaskKeys.AddRange(tasksValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                        }
                        break;
                    default:
                        SetError(options, "Opción desconocida: '--" + token + "'. Usa --help para ver la sintaxis.");
                        break;
                }
            }
            return options;
        }

        private static string RequireValue(string[] args, ref int index, string inlineValue, CliOptions options, string optionName)
        {
            if (inlineValue != null)
                return inlineValue;
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
                return args[++index];
            SetError(options, "Falta el valor de " + optionName + ".");
            return null;
        }

        private static void SetError(CliOptions options, string message)
        {
            if (options.Error == null)
                options.Error = message;
        }

        public const string Usage = @"osm2mssql - Importador headless de OpenStreetMap a SQL Server

USO:
  osm2mssql --file <archivo.osm|.osm.pbf> --host <servidor> --database <base>
            [--user <usuario> --password <clave>] [--trusted] [--replace]
            [--tasks <all|init,reader,...>] [--logfile <ruta>] [--help]

PARAMETROS:
  --file       Ruta del extracto OSM (.osm o .osm.pbf). Obligatorio
               (también se acepta como primer argumento posicional).
  --host       Servidor SQL Server (p. ej. localhost, srv\INSTANCIA).
  --database   Nombre de la base de datos de destino.
  --user       Usuario SQL (autenticación SQL). Requiere --password.
  --password   Contraseña del usuario SQL.
  --trusted    Autenticación integrada de Windows. Excluyente con --user.
  --replace    Elimina y recrea la base de datos si ya existe.
               OBLIGATORIO cuando la BD existe: la importación es destructiva
               y este modo nunca pregunta interactivamente.
  --tasks      Subconjunto de tareas (por defecto all):
                 all        Todas (comportamiento por defecto)
                 init       CreateDatabase + InstallDbExtension
                 reader     NodeReader + WayReader + RelationReader
                 indices    NodeIndices + WayIndices + RelationIndices
                 attribute  AttributeWriter
                 line       CreateLineInDB
                 relation   CreateRelationInDB
                 spatial    CreateSpatialIndices
                 sql        ExecuteSqlCommands
               También se admiten nombres individuales de tarea
               (CreateDatabase, NodeReader, ...), separados por comas.
  --logfile    Ruta del archivo de log. Por defecto escribe siempre en
               Logfiles\OsmCliLog_yyyyMMdd.txt (relativo al directorio actual).
  --help       Muestra esta ayuda.

CONFIGURACIÓN:
  Si no se pasa --trusted ni --user/--password, se lee osm2mssql.xml (la
  configuración guardada por la interfaz gráfica) como origen de valores
  por defecto para host, base de datos y credenciales. Los argumentos
  explícitos siempre tienen prioridad. En modo CLI el archivo nunca se
  sobrescribe.

CÓDIGOS DE SALIDA:
  0  Éxito
  1  Argumentos inválidos (o --help mostrado)
  2  No se pudo conectar al servidor SQL
  3  La base de datos existe y no se indicó --replace
  4  Falló una tarea durante la importación
  5  Archivo de entrada inválido o inexistente

EJEMPLOS:
  osm2mssql --file ecuador-latest.osm.pbf --host localhost --database OSM --trusted --replace
  osm2mssql --file data.osm.pbf --host 192.168.1.10 --database OSM --user sa --password ""Clave!"" --replace
  osm2mssql ecuador-latest.osm.pbf --database OSM --replace --tasks init,reader
";
    }
}
