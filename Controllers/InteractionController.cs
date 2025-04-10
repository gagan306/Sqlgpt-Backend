using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApi.Data;
using ChatApi.Models;
using ChatApi.Services;

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
            if (string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.U_id))
            {
                return BadRequest("Question and U_id must be provided.");
            }

            // Optionally, fetch the employee to check allowed tables / limits.
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id.ToString() == request.U_id);
            if (employee == null)
            {
                return NotFound("Employee not found.");
            }

            // Save the Interaction with the question and additional info from employee.
            request.Post = employee.Post;
            _context.Interaction.Add(request);
            await _context.SaveChangesAsync();

            try
            {
                // Process the question using the QueryService.
                var (_, _, structuredAnswer) = await _queryService.ProcessQuestionAsync(request.Question);

                // Update the saved Interaction record with the answer.
                request.Answer = structuredAnswer;
                _context.Interaction.Update(request);
                await _context.SaveChangesAsync();

                // Return the final structured answer only.
                return Ok(new
                {
                    Answer = structuredAnswer
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing the query: {ex.Message}");
            }
        }
    }
}
