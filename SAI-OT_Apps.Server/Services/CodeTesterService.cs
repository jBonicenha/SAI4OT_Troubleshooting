using Microsoft.AspNetCore.Mvc;

namespace SAI_OT_Apps.Server.Services
{
    public class CodeTesterController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
