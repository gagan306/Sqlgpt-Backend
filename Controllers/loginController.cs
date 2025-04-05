using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("login")]
    public class loginController : Controller
    {
        private readonly AppDbContext _context;

        public loginController(AppDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        public IActionResult Post([FromBody] Employee request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.PasswordHash))
            {
                return BadRequest("Email and password are required.");
            }

            var employee = _context.Employees.FirstOrDefault(e => e.Email == request.Email);

            if (employee == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            if (!employee.IsRegistered)
            {
                return Unauthorized("Employee has not completed registration.");
            }

            var hashedPassword = request.PasswordHash;

            if (employee.PasswordHash != hashedPassword)
            {
                return Unauthorized("Invalid email or password.");
            }

            // At this point, login is successful
            return Ok(new { message = "Login successful", employeeId = employee.Id });
        }
    }
}
