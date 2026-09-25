namespace Axpense.Data.Entities.DriverEntities
{
    /// <summary>
    /// A document kept on file for a driver (national ID, training certificate, permit…).
    /// The optional scan is stored in file storage; only its metadata lives here.
    /// </summary>
    public class DriverDocument : TenantEntity
    {
        public Guid DriverId { get; set; }
        public string Name { get; set; } = "";
        public DateTime ExpiryDate { get; set; }

        // ---- Attachment metadata (null when no scan was uploaded) ----
        public string? FileKey { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }

        public Driver Driver { get; set; } = null!;
    }
}
