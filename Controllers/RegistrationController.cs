using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("register")]
    public class RegistrationController : Controller
    {
        private readonly AppDbContext _context;

        public RegistrationController(AppDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        public IActionResult Post([FromBody] Employee request )
        {
            try
            {
                var employee = new Employee
                {
                    Name = request.Name,
                    Email = request.Email,
                    PasswordHash = request.PasswordHash,
                    IsActive = true,
                    PasswordResetTokens = new List<PasswordResetToken>(),
                    RegistrationKey = request.RegistrationKey,
                    IsRegistered = true,
                };
                _context.Employees.Add(employee);
                _context.SaveChanges();
                return Ok(new { message = "Registered sucesfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while registering." });
            }
        }
    }
}
