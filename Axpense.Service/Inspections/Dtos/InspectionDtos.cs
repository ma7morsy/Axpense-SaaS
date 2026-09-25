namespace Axpense.Service.Inspections.Dtos
{
    // ---------------------------------------------------------------- templates

    public sealed record TemplateItemDto(
        Guid Id, int Sort, string Label, Guid PresetPartId, string PartName, string? PartNameAr, Guid CategoryId,
        string FieldType, string FieldTypeLabel, bool Critical, string? Unit, decimal? Min, decimal? Max, bool IssueOnFail);

    public sealed record TemplateDto(
        Guid Id, string Code, string Name, string Scope, bool Active, bool IsSystem, int CheckCount, int CriticalCount, int TimesRun,
        IReadOnlyList<TemplateItemDto> Items);

    public sealed class TemplateItemRequest
    {
        public string? Label { get; set; }
        public Guid? PresetPartId { get; set; }
        public string? FieldType { get; set; }
        public bool Critical { get; set; }
        public string? Unit { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public bool IssueOnFail { get; set; } = true;
    }

    public sealed class TemplateUpsertRequest
    {
        public string Name { get; set; } = "";
        public string? Scope { get; set; }
        public bool Active { get; set; } = true;
        public List<TemplateItemRequest> Items { get; set; } = [];
    }

    public sealed record FieldTypeOption(string Value, string Label);

    public sealed record InspectionOptionsDto(IReadOnlyList<string> Scopes, IReadOnlyList<FieldTypeOption> FieldTypes);

    // ---------------------------------------------------------------- runs

    public sealed record InspectionSummaryDto(int Total, int Answered, int Passed, int Failed, int NotApplicable);

    public sealed record InspectionListItemDto(
        Guid Id, string Code, Guid VehicleId, string VehicleName, string PlateNumber, Guid? TemplateId, string TemplateName,
        DateTime Date, decimal? Odometer, string? InspectorName, string Status, InspectionSummaryDto Summary);

    public sealed record InspectionStatsDto(int Completed, int FailedItems, int Templates, int ActiveTemplates, int VehiclesNeverInspected, int FleetSize, int InProgress);

    public sealed record InspectionListResponse(IReadOnlyList<InspectionListItemDto> Items, InspectionStatsDto Stats);

    public sealed class InspectionListQuery
    {
        public Guid? VehicleId { get; set; }
        public Guid? TemplateId { get; set; }
        public string? Status { get; set; }
    }

    public sealed record InspectionItemDto(
        Guid Id, int Sort, string Label, Guid? PresetPartId, string? PartName, string FieldType, string FieldTypeLabel,
        bool Critical, string? Unit, decimal? Min, decimal? Max, bool IssueOnFail,
        string? Result, decimal? Value, string? Comment, bool HasPhoto, Guid? IssueId,
        /// <summary>What still blocks completion for this check (null when fine).</summary>
        string? Problem);

    public sealed record InspectionDetailDto(
        Guid Id, string Code, Guid VehicleId, string VehicleName, string PlateNumber, string ReadingUnit, Guid? TemplateId, string TemplateName,
        DateTime Date, decimal? Odometer, string? InspectorName, string Status, DateTime? CompletedAtUtc,
        InspectionSummaryDto Summary, bool CriticalFailed, bool CanComplete, IReadOnlyList<InspectionItemDto> Items);

    public sealed class InspectionStartRequest
    {
        public Guid? VehicleId { get; set; }
        public Guid? TemplateId { get; set; }
        public decimal? Odometer { get; set; }
        public string? InspectorName { get; set; }
    }

    public sealed class InspectionAnswerRequest
    {
        /// <summary>pass | fail | na | null (clear).</summary>
        public string? Result { get; set; }
        public decimal? Value { get; set; }
        public string? Comment { get; set; }
    }

    public sealed record InspectionCompleteResult(InspectionDetailDto Inspection, int IssuesRaised, bool VehicleGrounded, Guid? MaintenanceId);
}
