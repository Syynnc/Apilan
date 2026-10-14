using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    public class LoginViewModel
    {
        // Members sign in with their student ID. The same field still takes a
        // username, for officers who have no student ID.
        [Required(ErrorMessage = "Enter your student ID.")]
        [Display(Name = "Student ID")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}
