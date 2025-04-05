using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers
{
    public class loginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
