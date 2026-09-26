using System;
using System.Diagnostics;

namespace osm2mssql.Importer.Classes
{
    /// <summary>
    /// Escribe el Trace a la consola con prefijo de hora [hh:mm:ss].
    /// Todos los accesos a la consola se envuelven en try/catch para no
    /// romper nunca la ejecución (WinExe, consolas adjuntas, redirecciones...).
    /// </summary>
    internal class CliConsoleTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            try
            {
                Console.Out.Write(message);
            }
            catch
            {
                // Sin consola disponible: se ignora
            }
        }

        public override void WriteLine(string message)
        {
            try
            {
                Console.Out.WriteLine("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), message);
            }
            catch
            {
                // Sin consola disponible: se ignora
            }
        }
    }
}
