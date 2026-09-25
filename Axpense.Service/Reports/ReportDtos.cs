namespace Axpense.Service.Reports
{
    /// <summary>KPI tile. Tone: "" | ok | warn | bad | neutral.</summary>
    public sealed record StatDto(string Label, string Value, string Sub, string Tone = "");

    // ---- fuel
    public sealed record FuelVehicleRow(Guid VehicleId, string Vehicle, string Plate, string FuelType, string Unit, int Entries, decimal Quantity,
        decimal Cost, double SharePct, decimal? PerHundred, decimal? CostPerKm);
    public sealed record FuelLogRow(Guid Id, DateTime Date, Guid VehicleId, string Vehicle, string Plate, string? Station, string? FuelType,
        decimal Quantity, string Unit, decimal UnitPrice, decimal Total, decimal? Odometer);
    public sealed record FuelReportDto(int Days, DateTime From, DateTime To, IReadOnlyList<StatDto> Stats, IReadOnlyList<FuelVehicleRow> ByVehicle,
        IReadOnlyList<FuelLogRow> Log, int LogTotal);

    // ---- budget
    public sealed record BudgetPlanRow(int Month, string Label, decimal Budget, decimal Actual, decimal Variance, double? VariancePct, bool Over, bool Future);
    public sealed record BudgetCategoryRow(string Name, string Color, decimal SharePct, decimal Budget, decimal Actual, decimal Remaining, double? UtilizationPct);
    public sealed record BudgetLogRow(Guid Id, string Name, string Category, string Period, int Month, decimal Limit, decimal Actual, decimal Remaining,
        double UtilizationPct, string Status);
    public sealed record BudgetReportDto(int Year, bool HasAnnual, IReadOnlyList<StatDto> Stats, IReadOnlyList<BudgetPlanRow> Plan,
        IReadOnlyList<BudgetCategoryRow> Categories, IReadOnlyList<BudgetLogRow> Log);

    // ---- PM compliance
    public sealed record PmComplianceRow(Guid VehicleId, string Vehicle, string Plate, string Trigger, decimal Interval, string Unit, decimal ConsumedPct,
        decimal? Reading, string ReadingUnit, string Label, DateTime? LastService, string Status, decimal? PreAlertPct, bool DispatchBlocked);
    public sealed record PmTaskRow(Guid Id, string Name, string? PartCategory, string Trigger, string Interval, decimal DurationHours, decimal EstimatedCost, string Role);
    public sealed record PmReportDto(IReadOnlyList<StatDto> Stats, IReadOnlyList<PmComplianceRow> Rows, IReadOnlyList<PmTaskRow> Tasks, int NoSchedule);

    // ---- work orders
    public sealed record TechnicianRow(string Technician, int Assigned, int Closed, int Open, int Overdue, decimal Cost);
    public sealed record OrderTypeRow(string Type, int Orders, int Tasks, double SharePct, decimal Cost);
    public sealed record OrderRegisterRow(Guid Id, string Code, string Vehicle, string Plate, string Type, string Priority, string? Technician,
        DateTime DueDate, int? AgeDays, decimal Cost, string Status);
    public sealed record WorkOrderReportDto(IReadOnlyList<StatDto> Stats, IReadOnlyList<TechnicianRow> Technicians, IReadOnlyList<OrderTypeRow> Types,
        IReadOnlyList<OrderRegisterRow> Register);

    // ---- cost
    public sealed record CostVehicleRow(Guid VehicleId, string Vehicle, string Plate, decimal Expenses, decimal Fuel, decimal WorkOrders, decimal Total,
        double SharePct, decimal? Distance, decimal? CostPerKm);
    public sealed record CostCategoryRow(string Name, string Color, int Entries, decimal Total, double SharePct, decimal? Budget, double? VariancePct);
    public sealed record CostReportDto(int Days, IReadOnlyList<StatDto> Stats, IReadOnlyList<CostVehicleRow> ByVehicle, IReadOnlyList<CostCategoryRow> ByCategory);

    // ---- issues
    public sealed record IssuePartRow(string Part, int Occurrences, int Vehicles, int Open, DateTime LastSeen, decimal LinkedCost, bool Recurring);
    public sealed record IssueVehicleRow(Guid VehicleId, string Vehicle, string Plate, int Issues, int Open);
    public sealed record IssueSourceRow(string Source, int Issues, double SharePct);
    public sealed record IssueReportDto(IReadOnlyList<StatDto> Stats, IReadOnlyList<IssuePartRow> Parts, IReadOnlyList<IssueVehicleRow> ByVehicle,
        IReadOnlyList<IssueSourceRow> BySource);

    // ---- uptime
    public sealed record UptimeRow(Guid VehicleId, string Vehicle, string Plate, string Status, int WorkOrders, int DowntimeDays, decimal? Distance, double UptimePct);
    public sealed record UptimeReportDto(int Days, IReadOnlyList<StatDto> Stats, IReadOnlyList<UptimeRow> Rows);

    // ---- drivers
    public sealed record DriverRow(Guid DriverId, string Driver, string LicenseClass, string Status, string? Vehicle, string? Plate, decimal Rating,
        int? LicenseDays, int ExpiredDocuments, int Assignments, decimal VehicleSpend, int Score);
    public sealed record DriverReportDto(int Days, IReadOnlyList<StatDto> Stats, IReadOnlyList<DriverRow> Rows);

    // ---- odometer
    public sealed record OdometerRow(Guid VehicleId, string Vehicle, string Plate, decimal? Current, string Unit, int Readings, decimal? Distance,
        decimal? AvgPerDay, DateTime? LastLogged, string? Source, bool Stale);
    public sealed record OdometerReportDto(int Days, IReadOnlyList<StatDto> Stats, IReadOnlyList<OdometerRow> Rows);

    // ---- expenses & forecast
    public sealed record ForecastMonth(string Key, string Label, decimal Total, bool Current);
    public sealed record ExpenseTypeTrendRow(string Name, string Color, int Entries, decimal Lifetime, decimal Last30, decimal Prior30, int TrendPct);
    public sealed record ExpenseReportDto(IReadOnlyList<StatDto> Stats, IReadOnlyList<ForecastMonth> Months, decimal Projection,
        IReadOnlyList<ExpenseTypeTrendRow> ByType);
}
