using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Data.Entities.CostEntities
{
    public class FuelTransaction : TenantEntity
    {
        public Guid VehicleId { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow.Date;
        public decimal QuantityLiters { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? Odometer { get; set; }
        public string? FuelType { get; set; }
        public string? Station { get; set; }
        public Vehicle Vehicle { get; set; } = null!;
    }
}
