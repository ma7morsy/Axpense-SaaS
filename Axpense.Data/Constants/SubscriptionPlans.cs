namespace Axpense.Data.Constants
{
    /// <summary>Platform plans. Prices in EGP; the annual price is 12 months less 20 %.</summary>
    public static class SubscriptionPlans
    {
        public const string Starter = "starter";
        public const string Business = "business";
        public const string Enterprise = "enterprise";

        public const string CycleMonthly = "monthly";
        public const string CycleAnnual = "annual";
        public static readonly string[] Cycles = [CycleMonthly, CycleAnnual];

        public const string StatusActive = "Active";
        public const string StatusCancelling = "Cancelling";
        public const string StatusCancelled = "Cancelled";

        public const string InvoiceIssued = "Issued";
        public const string InvoicePaid = "Paid";
        public const string InvoiceVoid = "Void";

        public const decimal AnnualDiscount = 0.20m;

        /// <summary>Null limit = unlimited.</summary>
        public sealed record Plan(string Key, string Name, decimal Monthly, int? Vehicles, int? Seats, int StorageGb, bool Popular, string[] Features)
        {
            public decimal Price(string cycle) => cycle == CycleAnnual ? Math.Round(Monthly * 12 * (1 - AnnualDiscount), 0) : Monthly;
        }

        public static readonly Plan[] All =
        [
            new(Starter, "Starter", 1500, 10, 3, 5, false,
                ["Up to 10 vehicles", "3 team members", "Fuel & expense tracking", "Basic reports", "Email support"]),
            new(Business, "Business", 4500, 50, 15, 50, true,
                ["Up to 50 vehicles", "15 team members", "Preventive maintenance engine", "Inspections & work orders", "Analytics & budgets", "Roles & permissions", "Priority support"]),
            new(Enterprise, "Enterprise", 12000, null, null, 500, false,
                ["Unlimited vehicles", "Unlimited team members", "Custom roles & SSO", "API access", "Dedicated success manager", "99.9% uptime SLA"])
        ];

        public static Plan? Find(string key) => All.FirstOrDefault(p => p.Key == key);
        public static int Rank(string key) => Array.FindIndex(All, p => p.Key == key);
    }

    /// <summary>Allowed values for the company profile.</summary>
    public static class CompanyOptions
    {
        public static readonly string[] Industries = ["Transport & Logistics", "Construction", "Passenger transport", "Oil & gas", "Retail & distribution", "Public sector", "Other"];
        public static readonly string[] Sizes = ["1 – 10 employees", "11 – 50 employees", "51 – 200 employees", "201 – 1,000 employees", "1,000+ employees"];
        public static readonly (string Id, string Label)[] TimeZones =
            [("Africa/Cairo", "Africa/Cairo (GMT+2)"), ("Asia/Riyadh", "Asia/Riyadh (GMT+3)"), ("Asia/Dubai", "Asia/Dubai (GMT+4)"), ("Europe/London", "Europe/London (GMT+0)")];
        public static readonly (string Code, string Label)[] Currencies =
            [("EGP", "EGP — Egyptian Pound"), ("SAR", "SAR — Saudi Riyal"), ("AED", "AED — UAE Dirham"), ("USD", "USD — US Dollar")];
        public static readonly string[] DateFormats = ["DD/MM/YYYY", "MM/DD/YYYY", "YYYY-MM-DD"];
        public static readonly (string Code, string Label)[] DistanceUnits = [("km", "Kilometres"), ("mi", "Miles")];
    }
}
