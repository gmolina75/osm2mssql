using System;
using System.Linq;
using System.Windows;

namespace osm2mssql.Importer.Languages
{
    public class Language
    {
        public const string English = "en";
        public const string Spanish = "es";

        private const string DictionaryUriFormat = "pack://application:,,,/Languages/Languages{0}.xaml";

        public static event EventHandler LanguageChanged;

        public string this[object key]
        {
            get
            {
                if (App.Current == null)
                    return string.Empty;
                return App.Current.Resources[key] as string ?? string.Empty;
            }
        }

        public static string CurrentCulture { get; private set; }

        public static Language CurrentLanguage { get; private set; }
        static Language()
        {
            CurrentLanguage = new Language();
        }

        public static void SetLanguage(string culture)
        {
            if (string.IsNullOrEmpty(culture))
                culture = English;

            var app = App.Current;
            if (app == null)
                return;

            if (culture.Equals(CurrentCulture, StringComparison.OrdinalIgnoreCase))
                return;

            RemoveDictionary(app, GetDictionaryUri(English));
            RemoveDictionary(app, GetDictionaryUri(culture));

            app.Resources.MergedDictionaries.Add(LoadDictionary(GetDictionaryUri(English)));
            if (!culture.Equals(English, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    app.Resources.MergedDictionaries.Add(LoadDictionary(GetDictionaryUri(culture)));
                }
                catch
                {
                    //No dictionary for that culture - english is kept as fallback
                }
            }

            CurrentCulture = culture;
            var handler = LanguageChanged;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        private static Uri GetDictionaryUri(string culture)
        {
            var suffix = culture.Equals(English, StringComparison.OrdinalIgnoreCase) ? string.Empty : "." + culture;
            return new Uri(string.Format(DictionaryUriFormat, suffix), UriKind.Absolute);
        }

        private static ResourceDictionary LoadDictionary(Uri uri)
        {
            return new ResourceDictionary { Source = uri };
        }

        private static void RemoveDictionary(Application app, Uri uri)
        {
            var dictionary = app.Resources.MergedDictionaries
                .FirstOrDefault(x => x.Source != null && x.Source.Equals(uri));
            if (dictionary != null)
                app.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
