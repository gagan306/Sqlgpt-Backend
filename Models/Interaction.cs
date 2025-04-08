using System.ComponentModel.DataAnnotations;

namespace ChatApi.Models
{
    public class Interaction
    {
        [Key]
        public Guid Q_Id { get; set; } = Guid.NewGuid();

        public string? Question { get; set; }

        public string? Answer { get; set; }

        
        public string? U_id { get; set; }
        
        public string? Post { get; set; }


        public DateTime Timestamp { get; set; }
    }
}
