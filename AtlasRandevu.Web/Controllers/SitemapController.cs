
using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AtlasRandevu.Web.Controllers
{
    public class SitemapController : Controller
    {
        private readonly AtlasRandevuDbContext _db;

        public SitemapController(AtlasRandevuDbContext db)
        {
            _db = db;
        }

        [HttpGet("/sitemap.xml")]
        public async Task<IActionResult> Index()
        {
            const string siteAdresi =
                "https://randevu.atlassoftware.com.tr";

            XNamespace ns =
                "http://www.sitemaps.org/schemas/sitemap/0.9";

            var sitemap = new XElement(ns + "urlset");

            // Herkese açık keşfet sayfası
            sitemap.Add(
                new XElement(ns + "url",
                    new XElement(ns + "loc",
                        siteAdresi + Url.Action(
                            "Index", "Kesfet"))
                )
            );

            // Yalnızca onaylanmış işletmeler
            var isletmeIdleri = await _db.Isletmeler
                .AsNoTracking()
                .Where(x => x.Durum == "Onaylandi")
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync();

            foreach (var id in isletmeIdleri)
            {
                var yol = Url.Action(
                    "Detay",
                    "Kesfet",
                    new { id });

                if (string.IsNullOrWhiteSpace(yol))
                    continue;

                sitemap.Add(
                    new XElement(ns + "url",
                        new XElement(ns + "loc",
                            siteAdresi + yol)
                    )
                );
            }

            var belge = new XDocument(
                new XDeclaration(
                    "1.0", "utf-8", null),
                sitemap
            );

            return Content(
                belge.ToString(),
                "application/xml",
                Encoding.UTF8
            );
        }
    }
}
