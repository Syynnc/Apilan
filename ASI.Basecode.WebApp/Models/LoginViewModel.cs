using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    public class LoginViewModel
    {
        // A student ID or a username; nothing else signs in.
        [Required(ErrorMessage = "Enter your student ID.")]
        [Display(Name = "Student ID")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}
