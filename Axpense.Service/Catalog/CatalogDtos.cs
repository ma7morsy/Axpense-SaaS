namespace Axpense.Service.Catalog
{
    public sealed record PresetPartDto(Guid Id, Guid CategoryId, string Code, string Name, string? NameAr);

    public sealed record PartCategoryDto(Guid Id, string Code, int Number, string Name, string? NameAr, IReadOnlyList<PresetPartDto> Parts);

    public sealed record PartsCatalogDto(IReadOnlyList<PartCategoryDto> Categories, int TotalParts);

    /// <summary>Payload for creating / renaming a category or a part.</summary>
    public sealed class CatalogNameRequest
    {
        public string Name { get; set; } = "";
        public string? NameAr { get; set; }
    }
}
