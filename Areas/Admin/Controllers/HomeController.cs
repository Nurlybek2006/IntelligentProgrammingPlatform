using IntelligentProgrammingPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class HomeController : Controller
{
    [HttpGet]
    // Әкімшіге басқару бөлімдерінің сілтемелерін көрсетеді.
    public IActionResult Index() => View();
}
