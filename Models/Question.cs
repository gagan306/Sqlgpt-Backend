namespace ChatApi.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string? QuestionText { get; set; }
        public DateTime Timestamp { get; set; }
    }
}