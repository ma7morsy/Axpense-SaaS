using Axpense.Data.Entities.CatalogEntities;

namespace Axpense.Data.Entities.VehicleEntities
{
    /// <summary>
    /// A part fitted to a vehicle, typed by the organization's parts catalogue. Wear is counted from
    /// the activation reading (distance) or activation date (time); warranty holds while both the date
    /// and the optional distance cap hold.
    /// </summary>
    public class VehiclePart : TenantEntity
    {
        public Guid VehicleId { get; set; }
        public Guid PresetPartId { get; set; }
        /// <summary>Organization-wide sequence number, shown as PRT-0001.</summary>
        public int Number { get; set; }
        public string? Serial { get; set; }
        public decimal? UnitCost { get; set; }
        public int? LifespanKm { get; set; }
        public int? LifespanMonths { get; set; }
        public decimal InstalledReading { get; set; }
        public DateTime InstalledDate { get; set; }
        public DateTime? WarrantyUntil { get; set; }
        public int? WarrantyKm { get; set; }
        public string Status { get; set; } = Constants.VehicleConstants.PartInService;
        public DateTime? RetiredDate { get; set; }
        public string? Notes { get; set; }

        public Vehicle Vehicle { get; set; } = null!;
        public PresetPart PresetPart { get; set; } = null!;
    }
}
