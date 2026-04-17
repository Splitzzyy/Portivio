namespace Portivio.Application.DTOs.Profile
{
    public class CreateProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public string BaseCurrency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public string BaseCurrency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ProfileResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BaseCurrency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
