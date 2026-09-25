namespace Axpense.Data.Constants
{
    /// <summary>Allowed values and thresholds for vehicles, tracking and parts.</summary>
    public static class VehicleConstants
    {
        // ---- Status
        public const string StatusActive = "Active";
        public const string StatusInTransit = "In transit";
        public const string StatusUnderMaintenance = "Under maintenance";
        public const string StatusDown = "Inoperable / Down";
        public const string StatusRetired = "Retired";
        public static readonly string[] Statuses = [StatusActive, StatusInTransit, StatusUnderMaintenance, StatusDown, StatusRetired];

        // ---- Category
        public const string CategoryTruck = "Truck";
        public const string CategorySedan = "Sedan";
        public const string CategoryBus = "Bus";
        public const string CategoryVan = "Van";
        public const string CategoryPickup = "Pickup";
        public const string CategoryMachinery = "Machinery";
        public const string CategoryMotorcycle = "Motorcycle";
        public const string CategoryTrailer = "Trailer";
        public static readonly string[] Categories =
            [CategoryTruck, CategorySedan, CategoryBus, CategoryVan, CategoryPickup, CategoryMachinery, CategoryMotorcycle, CategoryTrailer];

        // ---- Ownership
        public const string OwnerCompany = "Company-owned";
        public const string OwnerClient = "Client";
        public const string OwnerProvider = "Provider";
        public static readonly string[] OwnerTypes = [OwnerCompany, OwnerClient, OwnerProvider];

        // ---- Usage & power
        public const string UnitKm = "km";
        public const string UnitHours = "hr";
        public static readonly string[] ReadingUnits = [UnitKm, UnitHours];

        public const string EngineMechanic = "Mechanic";
        public const string EngineElectric = "Electric";
        public const string EngineHybrid = "Hybrid";
        public static readonly string[] EngineTypes = [EngineMechanic, EngineElectric, EngineHybrid];

        public const string FuelDiesel = "Diesel";
        public const string FuelPetrol92 = "Petrol 92";
        public const string FuelPetrol95 = "Petrol 95";
        public const string FuelElectric = "Electric";
        public const string FuelCng = "CNG";
        public const string FuelHybrid = "Hybrid";
        public static readonly string[] FuelTypes = [FuelDiesel, FuelPetrol92, FuelPetrol95, FuelElectric, FuelCng, FuelHybrid];

        // ---- PM trigger
        public const string PmUsage = "usage";
        public const string PmTime = "time";
        public const string PmHours = "hours";
        public const string PmHybrid = "hybrid";
        public static readonly string[] PmTriggers = [PmUsage, PmTime, PmHours, PmHybrid];

        /// <summary>Insurance and registration reminders fire this many days ahead.</summary>
        public const int RenewalReminderDays = 30;

        // ---- Parts
        public const string PartInService = "In service";
        public const string PartNeedsAttention = "Needs attention";
        public const string PartRetired = "Retired";
        public static readonly string[] PartStatuses = [PartInService, PartNeedsAttention, PartRetired];
        /// <summary>A fitted part is "due" once this share of its service life is used.</summary>
        public const decimal PartDueThresholdPercent = 85m;
        /// <summary>Warranty "ending soon" window.</summary>
        public const int WarrantyEndingDays = 45;
        public const int WarrantyEndingKm = 1500;

        // ---- Issues
        public const string PriorityLow = "Low";
        public const string PriorityMedium = "Medium";
        public const string PriorityHigh = "High";
        public static readonly string[] Priorities = [PriorityLow, PriorityMedium, PriorityHigh];
        public const string IssueOpen = "Open";
        public const string IssueInProgress = "In progress";
        public const string IssueResolved = "Resolved";
        public static readonly string[] IssueStatuses = [IssueOpen, IssueInProgress, IssueResolved];
        public const string IssueSourceManual = "Manual report";
        public const string IssueSourceDriver = "Driver report";
        public const string IssueSourceInspection = "Inspection";
        public static readonly string[] IssueSources = [IssueSourceManual, IssueSourceDriver, IssueSourceInspection];

        // ---- Odometer sources
        public const string ReadingManual = "Manual reading";
        public const string ReadingInitial = "Vehicle registered";
        public const string ReadingEdited = "Vehicle edited";
        public const string ReadingFuel = "Fuel entry";
        public const string ReadingInspection = "Inspection";
        public const string ReadingWorkOrder = "Work order";
        public const string ReadingTelematics = "Telematics";
        /// <summary>Sources a user can pick when logging a reading by hand.</summary>
        public static readonly string[] ManualReadingSources = [ReadingManual, ReadingFuel, ReadingInspection, ReadingWorkOrder, ReadingTelematics];

        /// <summary>Operational speed limit (km/h) per category, used for the live speed gauge.</summary>
        public static int SpeedLimitFor(string category) => category switch
        {
            CategoryTruck => 80,
            CategoryBus => 70,
            CategoryMachinery => 20,
            CategorySedan => 90,
            CategoryMotorcycle => 80,
            _ => 60
        };
    }
}
