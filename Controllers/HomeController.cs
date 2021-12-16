using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(new Models.HomeViewModel());
        }


        [HttpPost]
        public ActionResult Index(Models.HomeViewModel model)
        {
            model.SearchResults = LuceneSearch.StartSearch(model.SearchTerm, 10);

            return View(model);
        }
    }
}