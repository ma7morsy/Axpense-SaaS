namespace Axpense.Data.Entities.SaasEntities
{
    /// <summary>
    /// The organization's plan. Renewal is processed when the subscription is read (no scheduler yet):
    /// each elapsed period with auto-renew on rolls the period forward and issues an invoice.
    /// </summary>
    public class Subscription : TenantEntity
    {
        public string PlanKey { get; set; } = Constants.SubscriptionPlans.Business;
        /// <summary>monthly | annual</summary>
        public string BillingCycle { get; set; } = Constants.SubscriptionPlans.CycleMonthly;
        /// <summary>Active | Cancelling | Cancelled</summary>
        public string Status { get; set; } = Constants.SubscriptionPlans.StatusActive;
        public bool AutoRenew { get; set; } = true;
        public DateTime CustomerSince { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }

        /// <summary>Payment method as reported by the payment provider — never the card number.</summary>
        public string? CardBrand { get; set; }
        public string? CardLast4 { get; set; }
        public string? CardExpiry { get; set; }
        public string? CardHolder { get; set; }
        public string? BillingEmail { get; set; }
    }

    /// <summary>A subscription invoice (INV-2026-0148).</summary>
    public class SubscriptionInvoice : TenantEntity
    {
        public int Year { get; set; }
        public int Number { get; set; }
        public DateTime IssueDate { get; set; }
        public string Description { get; set; } = "";
        public string PlanKey { get; set; } = "";
        public string BillingCycle { get; set; } = "";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EGP";
        /// <summary>Issued (awaiting the payment provider) | Paid | Void</summary>
        public string Status { get; set; } = Constants.SubscriptionPlans.InvoiceIssued;
    }
}
