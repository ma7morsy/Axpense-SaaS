namespace Axpense.Data.Entities.SettingsEntities
{
    /// <summary>Preventive-maintenance engine rules, one row per organization.</summary>
    public class PmEngineSettings : TenantEntity
    {
        /// <summary>Warn when a service item has this many km (or fewer) left.</summary>
        public int DistancePreAlertKm { get; set; } = 500;
        public int TimePreAlertDays { get; set; } = 7;
        public int EngineHourPreAlert { get; set; } = 25;
        public bool AutoGenerateWorkOrders { get; set; } = true;
        public bool BlockDispatchOnCriticalOverdue { get; set; } = true;

        public ICollection<PmEscalationStep> EscalationSteps { get; set; } = [];
    }

    /// <summary>Who an overdue PM work order escalates to, after how many days overdue.</summary>
    public class PmEscalationStep : TenantEntity
    {
        public Guid PmEngineSettingsId { get; set; }
        public int AfterDaysOverdue { get; set; }
        public string Role { get; set; } = "";

        public PmEngineSettings Settings { get; set; } = null!;
    }
}
