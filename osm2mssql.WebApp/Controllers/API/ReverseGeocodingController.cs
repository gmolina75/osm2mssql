using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace osm2mssql.WebApp.Controllers.API
{
    public class ReverseGeocodingController : ApiController
    {
        [HttpGet]
        [Route("api/ReverseGeocoding")]
        public GeoPoint SearchNodeTags(int tagType, string text)
        {
            var db = new InfoDAL.osm2Entities();
            var query = db.tNodeTag.Where(x => x.tTagType.Typ == tagType)
                                   .Where(x => x.Info.Contains(text));
            var result = query.FirstOrDefault();
            if (result == null)
                return null;
            return new GeoPoint
            {
                Latitude = result.tNode.Latitude,
                Longitude = result.tNode.Longitude
            };
        }

        [HttpGet]
        [Route("api/ReverseGeocoding/search")]
        public IHttpActionResult Search(int tagType, string text)
        {
            if (tagType <= 0)
                return Content(System.Net.HttpStatusCode.BadRequest, new { error = "Selecciona un tipo de elemento para buscar." });
            if (string.IsNullOrWhiteSpace(text))
                return Content(System.Net.HttpStatusCode.BadRequest, new { error = "El parámetro 'text' es obligatorio." });

            var db = new InfoDAL.osm2Entities();
            var query = db.tNodeTag.Where(x => x.Typ == tagType)
                                   .Where(x => x.Info.Contains(text))
                                   .OrderBy(x => x.Info)
                                   .Take(20);
            var resultados = query.ToList().Select(x => new NodeTagSearchResult
            {
                Name = x.Info,
                TagType = x.Typ,
                Lat = x.tNode.Latitude,
                Lon = x.tNode.Longitude
            }).ToList();
            return Ok(resultados);
        }

        [HttpGet]
        [Route("api/ReverseGeocoding/reverse")]
        public IHttpActionResult Reverse(double lat, double lon)
        {
            if (!ModelState.IsValid)
                return Content(System.Net.HttpStatusCode.BadRequest, new { error = "Los parámetros 'lat' y 'lon' deben ser numéricos." });
            if (lat < -90 || lat > 90)
                return Content(System.Net.HttpStatusCode.BadRequest, new { error = "El parámetro 'lat' debe estar entre -90 y 90." });
            if (lon < -180 || lon > 180)
                return Content(System.Net.HttpStatusCode.BadRequest, new { error = "El parámetro 'lon' debe estar entre -180 y 180." });

            using (var access = new InfoDAL.OsmAccess(GetProviderConnectionString()))
            {
                var info = access.GetAdminInformation(new InfoDAL.OsmPoint { Latitude = lat, Longitude = lon });
                if (info == null)
                    return Ok(new AdminReverseResult());
                return Ok(new AdminReverseResult
                {
                    Name = info.Name,
                    AdminLevel = info.AdminLevel,
                    Place = info.Place,
                    PostalCode = info.PostalCode
                });
            }
        }

        private static string GetProviderConnectionString()
        {
            var entity = ConfigurationManager.ConnectionStrings["osm2Entities"].ConnectionString;
            return new EntityConnectionStringBuilder(entity).ProviderConnectionString;
        }
    }

    public class NodeTagSearchResult
    {
        public string Name { get; set; }
        public int TagType { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class AdminReverseResult
    {
        public string Name { get; set; }
        public int? AdminLevel { get; set; }
        public string Place { get; set; }
        public string PostalCode { get; set; }
    }
}
