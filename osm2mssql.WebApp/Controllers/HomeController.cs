using osm2mssql.InfoDAL;
using osm2mssql.WebApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Mvc;

namespace osm2mssql.WebApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var model = new ReverseGeocodingModel
            {
                SelectedTagTyp = 0,
                TagTypes = new List<tTagType>()
            };

            try
            {
                using (var db = new osm2Entities())
                {
                    model.TagTypes = db.tTagType.OrderBy(x => x.Typ).ToList();
                    if (model.TagTypes.Any())
                        model.SelectedTagTyp = model.TagTypes.First().Typ;
                }
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage =
                    "No se pudo conectar a la base de datos OSM. Revisa el connection string 'osm2Entities' en Web.config.";
            }

            return View(model);
        }
    }
}
