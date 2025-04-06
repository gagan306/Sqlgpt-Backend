using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> RegisterUser([FromBody] SignInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.PasswordHash) ||
                string.IsNullOrWhiteSpace(request.RegistrationKey))
            {
                return BadRequest("Email, password, and registration key are required.");
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == request.Email);

            if (employee == null)
            {
                return Unauthorized("Employee is not registered in the system.");
            }

            if (employee.IsRegistered)
            {
                return Unauthorized("Employee is already registered.");
            }

            if (!string.Equals(employee.RegistrationKey, request.RegistrationKey, StringComparison.Ordinal))
            {
                return Unauthorized("Invalid registration key.");
            }

            
            employee.PasswordHash = request.PasswordHash; 
            employee.IsRegistered = true;

            try
            {
                await _context.SaveChangesAsync();
                return Ok("Employee registered successfully.");
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"Error registering employee: {ex.Message}");
            }
        }
    }
}
