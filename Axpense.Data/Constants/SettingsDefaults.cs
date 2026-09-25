namespace Axpense.Data.Constants
{
    /// <summary>Defaults every organization starts with for Settings → Expense types, Maintenance and Budget.</summary>
    public static class SettingsDefaults
    {
        public const string SystemFuel = "fuel";
        public const string SystemMaintenance = "maintenance";
        public const string SystemOther = "other";

        public sealed record DefaultExpenseType(string Name, string Color, string? SystemKey);

        public static readonly DefaultExpenseType[] ExpenseTypes =
        [
            new("Fuel", "#19B394", SystemFuel),
            new("Maintenance", "#1F7BD8", SystemMaintenance),
            new("Parts", "#7CC0F5", null),
            new("Insurance", "#1D4E89", null),
            new("Tires", "#F2A93B", null),
            new("Other", "#B8C4D0", SystemOther)
        ];

        /// <summary>Palette offered when adding or recolouring an expense type.</summary>
        public static readonly string[] Palette =
            ["#19B394", "#1F7BD8", "#7CC0F5", "#1D4E89", "#F2A93B", "#B8C4D0", "#8E6CD8", "#E0533D", "#2E86DE", "#16A085"];

        public static readonly string[] TaskCategories = ["Mechanical", "Electrical", "Body", "Safety", "Performance", "Other"];

        public static readonly string[] EscalationRoles = ["Technician", "Supervisor", "Fleet manager", "Admin"];

        /// <summary>Default PM task library (name, part category, task category, trigger, km, days, hours, duration h, cost EGP, role).</summary>
        public static readonly (string Name, string PartCategory, string TaskCategory, string Trigger, int? Km, int? Days, int? Hours, decimal Duration, decimal Cost, string Role)[] PmTasks =
        [
            ("Engine oil & filter change", "Engine Group", "Mechanical", "hybrid", 10000, 180, null, 1.5m, 5500, "Technician"),
            ("Brake pad inspection & replace", "Braking System", "Safety", "usage", 20000, null, null, 2.5m, 12800, "Technician"),
            ("Tyre rotation & pressure check", "Body & Consumables", "Mechanical", "usage", 15000, null, null, 1m, 2800, "Technician"),
            ("Timing belt replacement", "Engine Group", "Mechanical", "usage", 120000, null, null, 6m, 38000, "Senior technician"),
            ("Hydraulic system service", "Transmission & Drivetrain", "Mechanical", "hours", null, null, 500, 4m, 23000, "Heavy equipment tech"),
            ("Battery & charging system test", "Electrical & Sensors", "Electrical", "time", null, 365, null, 0.75m, 1800, "Electrician"),
        ];
        public static readonly string[] PmTaskTriggers = ["usage", "time", "hours", "hybrid"];

        public static readonly (int AfterDays, string Role)[] EscalationChain = [(0, "Technician"), (3, "Supervisor"), (7, "Fleet manager")];
    }
}
