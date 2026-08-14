using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDUTASK_1._1.Models
{
    public class TaskAssignment
    {
        public int AssignmentID { get; set; }
        public int TaskID { get; set; }
        public int TeacherID { get; set; }
        public DateTime Deadline { get; set; }
        public string Priority { get; set; }
        public DateTime AssignedAt { get; set; }

    }
}
