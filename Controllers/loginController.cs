using ChatApi.Data;
using ChatApi.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("login")]
    public class LoginController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult Post([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PasswordHash))
                return BadRequest("Email and password are required.");

            var employee = _context.Employees.FirstOrDefault(e => e.Email == request.Email);

            if (employee == null || !employee.IsRegistered)
                return Unauthorized("Invalid credentials or employee not registered.");

            var hashedPassword = request.PasswordHash;

            if (employee.PasswordHash != hashedPassword)
                return Unauthorized("Invalid credentials.");

            return Ok(new
            {
                message = "Login successful haha bhenchod",
                employeeId = employee.Id,
                employee.Name,
                employee.Email
            });
        }
    }
}
