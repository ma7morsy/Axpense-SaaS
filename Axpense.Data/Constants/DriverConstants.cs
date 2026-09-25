namespace Axpense.Data.Constants
{
    /// <summary>Allowed values and business thresholds for the Drivers module.</summary>
    public static class DriverConstants
    {
        public const string StatusActive = "Active";
        public const string StatusOnLeave = "On leave";
        public const string StatusSuspended = "Suspended";
        public static readonly string[] Statuses = [StatusActive, StatusOnLeave, StatusSuspended];

        public const string LicenseClassLight = "B — Light vehicle";
        public const string LicenseClassHeavy = "C — Heavy goods";
        public const string LicenseClassPassenger = "D — Passenger transport";
        public const string LicenseClassEquipment = "Heavy equipment operator";
        public static readonly string[] LicenseClasses = [LicenseClassLight, LicenseClassHeavy, LicenseClassPassenger, LicenseClassEquipment];

        /// <summary>Licence / document reminders fire this many days before expiry.</summary>
        public const int ExpiryReminderDays = 30;

        public const decimal MinRating = 0m;
        public const decimal MaxRating = 5m;

        public const string EmployeeNumberPrefix = "EMP-";
        public const int EmployeeNumberSeed = 1000;
    }
}
