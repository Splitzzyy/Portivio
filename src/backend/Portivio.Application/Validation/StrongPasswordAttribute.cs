using System.ComponentModel.DataAnnotations;

namespace Portivio.Application.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class StrongPasswordAttribute : ValidationAttribute
    {
        public int MinLength { get; set; } = 8;
        public int MaxLength { get; set; } = 128;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireNonAlphanumeric { get; set; } = false;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string password || string.IsNullOrEmpty(password))
                return ValidationResult.Success;

            var errors = new List<string>();

            if (password.Length < MinLength)
                errors.Add($"must be at least {MinLength} characters long");
            if (password.Length > MaxLength)
                errors.Add($"must be at most {MaxLength} characters long");
            if (RequireUppercase && !password.Any(char.IsUpper))
                errors.Add("must contain at least one uppercase letter");
            if (RequireLowercase && !password.Any(char.IsLower))
                errors.Add("must contain at least one lowercase letter");
            if (RequireDigit && !password.Any(char.IsDigit))
                errors.Add("must contain at least one digit");
            if (RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
                errors.Add("must contain at least one non-alphanumeric character");

            if (errors.Count == 0)
                return ValidationResult.Success;

            var fieldName = validationContext.DisplayName;
            return new ValidationResult($"{fieldName} {string.Join(", ", errors)}.");
        }
    }
}
