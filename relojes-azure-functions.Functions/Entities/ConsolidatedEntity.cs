using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace relojes_azure_functions.Functions.Entities
{
    public class ConsolidatedEntity : TableEntity
    {
        public int EmployeId { get; set; }
        public DateTime DateJob { get; set; }
        public int MinutesWorked { get; set; }
    }
}
