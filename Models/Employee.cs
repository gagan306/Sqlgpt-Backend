using System.ComponentModel.DataAnnotations;

namespace ChatApi.Models
{
    public class Employee
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; }

        public string? PasswordHash { get; set; }  // Nullable until password is set

        public bool IsActive { get; set; } = true;
        public string RegistrationKey { get; set; }
        public bool IsRegistered { get; set; }=false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    }
    public class EmployeeLogin
    {
        public string Name { get; set; }
        public string? PasswordHash { get; set; }
    }
}
