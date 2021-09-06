using System;

namespace relojes_azure_functions.Common.Models
{
    public class Consolidated
    {
        public int EmployeId { get; set; }
        public DateTime DateJob { get; set; }
        public int MinutesWorked { get; set; }
    }
}
