namespace Axpense.Data.Entities.CatalogEntities
{
    /// <summary>A part type in the catalogue (Brake Pads, Battery…). Code is P{category}{sequence}, e.g. P501.</summary>
    public class PresetPart : TenantEntity
    {
        public Guid CategoryId { get; set; }
        public int Sequence { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? NameAr { get; set; }

        public PartCategory Category { get; set; } = null!;
    }
}
