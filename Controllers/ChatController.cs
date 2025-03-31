using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChatApi.Controllers
{
    [ApiController]
    [Route("chat")]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChatController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult Post([FromBody] ChatRequest request)
        {
            try
            {
                var question = new Question
                {
                    QuestionText = request.Question,
                    Timestamp = DateTime.UtcNow
                };
                _context.Questions.Add(question);
                _context.SaveChanges();
                return Ok(new { message = "Question received and stored successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while storing the question." });
            }
        }
    }
}