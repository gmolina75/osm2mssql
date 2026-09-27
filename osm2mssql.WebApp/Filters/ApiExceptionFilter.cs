using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace osm2mssql.WebApp.Filters
{
    public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        private const string MensajeConexion =
            "No se pudo conectar a la base de datos OSM. Revisa el connection string 'osm2Entities' en Web.config.";

        public override void OnException(HttpActionExecutedContext context)
        {
            var ex = context.Exception;
            var mensaje = "Se produjo un error inesperado en el servidor.";

            while (ex != null)
            {
                if (ex is SqlException || ex is EntityException)
                {
                    mensaje = MensajeConexion;
                    break;
                }
                ex = ex.InnerException;
            }

            context.Response = context.Request.CreateResponse(
                HttpStatusCode.InternalServerError,
                new { error = mensaje });
        }
    }
}
