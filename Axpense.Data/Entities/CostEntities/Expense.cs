using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Data.Entities.CostEntities
{
    public class Expense : TenantEntity
    {
        public Guid? VehicleId { get; set; }
        public string Category { get; set; } = "Other";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;
        public string? Vendor { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? ReceiptUrl { get; set; }
        public string? Notes { get; set; }
        public Vehicle? Vehicle { get; set; }
    }
}
