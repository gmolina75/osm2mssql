using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace osm2mssql.Importer.Model
{
    public class ImporterModel
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Language { get; set; }

        public string BingApi { get; set; }
        public string WebHost { get; set; }
        public int WebPort { get; set; }
        public string WebDatabase { get; set; }
        public string WebUsername { get; set; }
        public string WebPassword { get; set; }

        private string _plainPassword;

        [OnSerializing]
        private void EncryptPassword(StreamingContext context)
        {
            _plainPassword = Password;
            if (string.IsNullOrEmpty(Password))
                return;
            try
            {
                Password = Convert.ToBase64String(
                    ProtectedData.Protect(Encoding.UTF8.GetBytes(Password), null, DataProtectionScope.CurrentUser));
            }
            catch
            {
                Password = _plainPassword;
            }
        }

        [OnSerialized]
        private void RestorePassword(StreamingContext context)
        {
            Password = _plainPassword;
        }

        [OnDeserialized]
        private void DecryptPassword(StreamingContext context)
        {
            if (string.IsNullOrEmpty(Password))
                return;
            try
            {
                var bytes = Convert.FromBase64String(Password);
                Password = Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
            }
            catch
            {
                // Valor heredado en texto plano: se mantiene y se re-cifrará al guardar
            }
        }
    }
}
