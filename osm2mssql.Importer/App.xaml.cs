using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Serialization;
using osm2mssql.Importer.Classes;
using osm2mssql.Importer.Languages;
using osm2mssql.Importer.Model;
using osm2mssql.Library;
using osm2mssql.Library.OpenStreetMapTypes;

namespace osm2mssql.Importer
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {

        public App()
        {
            if (!Trace.Listeners.OfType<OsmTextWriterTraceListener>().Any())
            {
                Trace.Listeners.Add(new OsmTextWriterTraceListener("OsmServiceLog.txt"));
            }
            Trace.AutoFlush = true;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ApplySavedLanguage();
            base.OnStartup(e);
        }

        private static void ApplySavedLanguage()
        {
            try
            {
                var ser = new XmlSerializer(typeof(ImporterModel));
                using (var file = File.OpenRead("osm2mssql.xml"))
                {
                    var model = (ImporterModel)ser.Deserialize(file);
                    Language.SetLanguage(model.Language);
                    return;
                }
            }
            catch
            {
                //No saved settings - keep english as default
            }
            Language.SetLanguage(Language.English);
        }

        public static string GetResourceFileText(string resourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            if (resourceName.Contains("osm2mssql.Library"))
                asm = typeof (OsmTextWriterTraceListener).Assembly;

            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }

}
