using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Data.Entities.DriverEntities
{
    public class VehicleAssignment : TenantEntity {
        public Guid VehicleId { get; set; } 
        public Guid DriverId { get; set; } 
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; } 
        public string AssignmentType { get; set; } = "Primary";
        public string? Notes { get; set; } 
        public Vehicle Vehicle { get; set; } = null!;
        public Driver Driver { get; set; } = null!; }

}
