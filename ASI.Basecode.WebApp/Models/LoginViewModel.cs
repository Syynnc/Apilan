using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    public class LoginViewModel
    {
        // A student ID or a username; nothing else signs in.
        [Required(ErrorMessage = "Enter your student ID.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use your student ID, like 2022-00101.")]
        [Display(Name = "Student ID")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}
