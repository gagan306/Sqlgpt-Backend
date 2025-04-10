using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApi.Data;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.Data.SqlClient;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("Interaction")]
    public class InteractionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly QueryService _queryService;

        public InteractionController(AppDbContext context, QueryService queryService)
        {
            _context = context;
            _queryService = queryService;
        }

        // POST api/interaction
        [HttpPost]
        public async Task<IActionResult> PostInteraction([FromBody] Interaction request)
        {
            // Validate required fields.
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest("Question is required.");
            }

            // Validate U_id is a valid GUID
            if (request.U_id == Guid.Empty)
            {
                return BadRequest("A valid U_id is required.");
            }

            // Trim the input fields to remove any extra spaces.
            request.Question = request.Question?.Trim();

            // Optionally, fetch the employee to check allowed tables / limits.
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.U_id);
            if (employee == null)
            {
                return NotFound("Employee not found.");
            }

            // Save the Interaction with the question, additional info from employee, and timestamp.
            request.Post = employee.Post;
            request.Timestamp = DateTime.UtcNow;  // Set the timestamp to the current UTC time.
            _context.Interaction.Add(request);
            await _context.SaveChangesAsync();  // Save the initial interaction record.

            try
            {
                // Process the question using the QueryService.
                var (_, _, structuredAnswer) = await _queryService.ProcessQuestionAsync(request.Question);

                // Trim the answer to avoid extra spaces.
                structuredAnswer = structuredAnswer?.Trim();

                // Update the saved Interaction record with the answer.
                request.Answer = structuredAnswer;
                _context.Interaction.Update(request);

                await _context.SaveChangesAsync();  // Save the updated interaction record with the answer.

                // Return the final structured answer.
                return Ok(new
                {
                    Answer = structuredAnswer
                });
            }
            catch (SqlException sqlEx)
            {
                // Log and handle the SQL error.
                return StatusCode(500, $"Database error: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                // Log and handle any other unexpected errors.
                return StatusCode(500, $"Error processing the query: {ex.Message}");
            }
        }
    }
}
