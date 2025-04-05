using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers
{
    public class SignInController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
