using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDUTASK_1._1.Models
{
    public class Teachers
    {
        public int TeacherID { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public DateTime AccountCreated { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ProfilePhotoPath { get; set; } = string.Empty;
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        /// <summary>Short free-text description shown on their profile.</summary>
        public string Bio { get; set; } = string.Empty;

    }
}
