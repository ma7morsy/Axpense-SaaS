namespace Axpense.Data.Entities.VehicleEntities
{
    /// <summary>An odometer (or engine-hour) reading. The latest reading is the vehicle's current value.</summary>
    public class OdometerReading : TenantEntity
    {
        public Guid VehicleId { get; set; }
        public DateTime ReadingDate { get; set; }
        public decimal Value { get; set; }
        public string Source { get; set; } = Constants.VehicleConstants.ReadingManual;
        public string? RecordedBy { get; set; }

        public Vehicle Vehicle { get; set; } = null!;
    }
}
