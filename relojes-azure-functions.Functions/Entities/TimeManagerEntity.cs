using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace relojes_azure_functions.Functions.Entities
{
    public class TimeManagerEntity : TableEntity
    {
        public int EmployeId { get; set; }
        public DateTime TimeMarker { get; set; }
        public int Type { get; set; }
        public bool IsConsolidated { get; set; }
    }
}
