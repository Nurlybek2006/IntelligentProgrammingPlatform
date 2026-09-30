using IntelligentProgrammingPlatform.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace IntelligentProgrammingPlatform.Controllers
{
    public class HomeController : Controller
    {
        // Қолданбаның бастапқы бетін көрсетеді.
        public IActionResult Index()
        {
            return View();
        }

        // Құпиялық туралы ақпарат бетін көрсетеді.
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        // Қауіпсіз қате бетін сұрау идентификаторымен көрсетеді.
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
