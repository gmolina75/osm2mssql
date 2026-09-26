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
        public IEnumerable<NodeTagSearchResult> Search(int tagType, string text)
        {
            var db = new InfoDAL.osm2Entities();
            var query = db.tNodeTag.Where(x => x.Typ == tagType)
                                   .Where(x => x.Info.Contains(text))
                                   .OrderBy(x => x.Info)
                                   .Take(20);
            return query.ToList().Select(x => new NodeTagSearchResult
            {
                Name = x.Info,
                TagType = x.Typ,
                Lat = x.tNode.Latitude,
                Lon = x.tNode.Longitude
            });
        }

        [HttpGet]
        [Route("api/ReverseGeocoding/reverse")]
        public AdminReverseResult Reverse(double lat, double lon)
        {
            using (var access = new InfoDAL.OsmAccess(GetProviderConnectionString()))
            {
                var info = access.GetAdminInformation(new InfoDAL.OsmPoint { Latitude = lat, Longitude = lon });
                if (info == null)
                    return new AdminReverseResult();
                return new AdminReverseResult
                {
                    Name = info.Name,
                    AdminLevel = info.AdminLevel,
                    Place = info.Place,
                    PostalCode = info.PostalCode
                };
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
