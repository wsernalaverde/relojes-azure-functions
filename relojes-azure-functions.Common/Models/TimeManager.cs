using System;

namespace relojes_azure_functions.Common.Models
{
    public class TimeManager
    {
        public int EmployeId { get; set; }
        public DateTime TimeMarker { get; set; }
        public int Type { get; set; }
        public bool IsConsolidated { get; set; }
    }
}
