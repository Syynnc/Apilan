using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Enter your username, student number, or email.")]
        [Display(Name = "Username, student number, or email")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}
