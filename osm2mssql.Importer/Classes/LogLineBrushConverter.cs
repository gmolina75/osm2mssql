using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace osm2mssql.Importer.Classes
{
    /// <summary>
    /// Devuelve un pincel según el nivel aparente de una línea de traza
    /// (error / advertencia / éxito / información). Los pinceles se
    /// resuelven del diccionario de temas de la aplicación.
    /// </summary>
    public class LogLineBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var app = Application.Current;
            var fallback = Brushes.White;
            if (app == null)
                return fallback;

            var text = (value as string ?? string.Empty).ToLowerInvariant();
            string resourceKey;
            if (ContainsAny(text, "error", "exception", "failed"))
                resourceKey = "LogErrorBrush";
            else if (ContainsAny(text, "warn"))
                resourceKey = "LogWarningBrush";
            else if (ContainsAny(text, "success"))
                resourceKey = "LogSuccessBrush";
            else
                resourceKey = "LogInfoBrush";

            return app.TryFindResource(resourceKey) as Brush ?? fallback;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword))
                    return true;
            }
            return false;
        }
    }
}
