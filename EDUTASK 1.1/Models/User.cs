using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDUTASK_1._1.Models
{
    public class User
    {
        public int User_id { get; set; }
        public string First_name { get; set; } = string.Empty;
        public string Last_name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Contact_number { get; set; } = string.Empty;
        public DateTime Account_created { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Profile_photo { get; set; } = string.Empty;
        public string Role_name { get; set; } = string.Empty;
        public bool Is_active { get; set; }

    }
}
