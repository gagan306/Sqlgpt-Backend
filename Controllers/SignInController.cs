using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("signUp")]

    public class SignInController : Controller
    {

        private readonly AppDbContext _context;

        public SignInController(AppDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        public IActionResult Post([FromBody] Employee request)
        {
            return View();
        }
    }
}
