using Microsoft.AspNetCore.Mvc;

namespace CRMBusiness.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
