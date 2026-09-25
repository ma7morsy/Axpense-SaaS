namespace Axpense.Data.Constants
{
    public static class MaintenanceConstants
    {
        public const string TypePreventive = "Preventive";
        public const string TypeCorrective = "Corrective";
        public const string TypeInspection = "Inspection";
        public static readonly string[] Types = [TypePreventive, TypeCorrective, TypeInspection];

        public const string PriorityLow = "Low";
        public const string PriorityMedium = "Medium";
        public const string PriorityHigh = "High";
        public const string PriorityCritical = "Critical";
        public static readonly string[] Priorities = [PriorityLow, PriorityMedium, PriorityHigh, PriorityCritical];

        public const string StatusScheduled = "Scheduled";
        public const string StatusInProgress = "In progress";
        public const string StatusCompleted = "Completed";
        /// <summary>Derived: still Scheduled after the scheduled date. Never stored.</summary>
        public const string StatusOverdue = "Overdue";
        public static readonly string[] Statuses = [StatusScheduled, StatusInProgress, StatusCompleted];
        /// <summary>Statuses shown in filters (stored + derived).</summary>
        public static readonly string[] DisplayStatuses = [StatusScheduled, StatusInProgress, StatusOverdue, StatusCompleted];

        public const string SourceManual = "Manual";
        public const string SourceIssue = "Issue";
        public const string SourceInspection = "Inspection";
        public const string SourcePmEngine = "PM engine";
        public const string SourcePart = "Part replacement";

        public const int MaxTasks = 40;
    }
}
