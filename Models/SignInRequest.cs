namespace ChatApi.Models
{
    public class SignInRequest
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public string RegistrationKey { get; set; }


    }
}
