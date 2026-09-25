namespace Axpense.Data.Entities.CatalogEntities
{
    /// <summary>
    /// A group in the organization's parts catalogue (Engine Group, Braking System…).
    /// The catalogue feeds vehicle parts, inspection templates, work-order tasks and issues.
    /// </summary>
    public class PartCategory : TenantEntity
    {
        /// <summary>Stable number used for codes: category C{Number}, its parts P{Number}{Sequence:00}.</summary>
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string? NameAr { get; set; }

        public ICollection<PresetPart> Parts { get; set; } = [];
    }
}
