
using Microsoft.AspNetCore.Mvc;

namespace AtlasRandevu.Web.Controllers
{
    public class YasalController : Controller
    {
        [HttpGet]
        public IActionResult KullanimSozlesmesi()
        {
            return View();
        }

        [HttpGet]
        public IActionResult KvkkAydinlatma()
        {
            return View();
        }
    }
}
