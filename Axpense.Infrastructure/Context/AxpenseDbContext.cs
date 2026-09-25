using Axpense.Data.Entities;
using Axpense.Data.Entities.CatalogEntities;
using Axpense.Data.Entities.CostEntities;
using Axpense.Data.Entities.DriverEntities;
using Axpense.Data.Entities.InspectionEntities;
using Axpense.Data.Entities.MaintenanceRecord;
using Axpense.Data.Entities.SaasEntities;
using Axpense.Data.Entities.SettingsEntities;
using Axpense.Data.Entities.VehicleEntities;
using Axpense.Data.UserApplication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Axpense.Infrastructure.Context
{
    public class AxpenseDbContext(DbContextOptions<AxpenseDbContext> options)
        : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
    {
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<VehiclePart> VehicleParts => Set<VehiclePart>();
        public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();
        public DbSet<VehicleIssue> VehicleIssues => Set<VehicleIssue>();
        public DbSet<PartCategory> PartCategories => Set<PartCategory>();
        public DbSet<PresetPart> PresetParts => Set<PresetPart>();
        public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
        public DbSet<TaskCategory> TaskCategories => Set<TaskCategory>();
        public DbSet<PmEngineSettings> PmEngineSettings => Set<PmEngineSettings>();
        public DbSet<PmEscalationStep> PmEscalationSteps => Set<PmEscalationStep>();
        public DbSet<PmTask> PmTasks => Set<PmTask>();
        public DbSet<AnnualBudget> AnnualBudgets => Set<AnnualBudget>();
        public DbSet<BudgetMonthAllocation> BudgetMonthAllocations => Set<BudgetMonthAllocation>();
        public DbSet<BudgetCategoryShare> BudgetCategoryShares => Set<BudgetCategoryShare>();
        public DbSet<Maintenance> MaintenanceRecords => Set<Maintenance>();
        public DbSet<MaintenanceTask> MaintenanceTasks => Set<MaintenanceTask>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<FuelTransaction> FuelTransactions => Set<FuelTransaction>();
        public DbSet<Budget> Budgets => Set<Budget>();
        public DbSet<Driver> Drivers => Set<Driver>();
        public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
        public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Inspection> Inspections => Set<Inspection>();
        public DbSet<InspectionItem> InspectionItems => Set<InspectionItem>();
        public DbSet<InspectionTemplate> InspectionTemplates => Set<InspectionTemplate>();
        public DbSet<InspectionTemplateItem> InspectionTemplateItems => Set<InspectionTemplateItem>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<SubscriptionInvoice> SubscriptionInvoices => Set<SubscriptionInvoice>();

        protected override void OnModelCreating(ModelBuilder m)
        {
            base.OnModelCreating(m);

            m.Entity<Organization>().HasKey(x => x.Id);

            m.Entity<ApplicationUser>()
                .HasOne(x => x.Organization)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<Vehicle>()
                .HasOne(x => x.Organization)
                .WithMany(x => x.Vehicles)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
            m.Entity<Vehicle>().HasIndex(x => new { x.OrganizationId, x.PlateNumber }).IsUnique();
            m.Entity<Vehicle>(e =>
            {
                e.Property(x => x.Make).HasMaxLength(60).IsRequired();
                e.Property(x => x.Model).HasMaxLength(60).IsRequired();
                e.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
                e.Property(x => x.Vin).HasMaxLength(17);
                e.Property(x => x.Category).HasMaxLength(30).IsRequired();
                e.Property(x => x.OwnerType).HasMaxLength(30).IsRequired();
                e.Property(x => x.OwnerName).HasMaxLength(120);
                e.Property(x => x.Status).HasMaxLength(30).IsRequired();
                e.Property(x => x.ReadingUnit).HasMaxLength(4).IsRequired();
                e.Property(x => x.EngineType).HasMaxLength(20).IsRequired();
                e.Property(x => x.FuelType).HasMaxLength(20).IsRequired();
                e.Property(x => x.PmTrigger).HasMaxLength(10).IsRequired();
                e.Property(x => x.CurrentOdometer).HasPrecision(12, 1);
                e.Property(x => x.PmLastServiceReading).HasPrecision(12, 1);
                e.Property(x => x.TankCapacity).HasPrecision(8, 1);
                e.Property(x => x.PurchasePrice).HasPrecision(18, 2);
                e.Property(x => x.InsuranceAnnualPremium).HasPrecision(18, 2);
                e.HasIndex(x => new { x.OrganizationId, x.Vin }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.Status });
            });

            m.Entity<PartCategory>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.NameAr).HasMaxLength(100);
                e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
                e.HasMany(x => x.Parts)
                    .WithOne(x => x.Category)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            m.Entity<PresetPart>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(12).IsRequired();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.NameAr).HasMaxLength(100);
                e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.CategoryId, x.Name }).IsUnique();
            });

            m.Entity<ExpenseType>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(60).IsRequired();
                e.Property(x => x.Color).HasMaxLength(9).IsRequired();
                e.Property(x => x.SystemKey).HasMaxLength(20);
                e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            });

            m.Entity<TaskCategory>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(60).IsRequired();
                e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            });

            m.Entity<PmEngineSettings>(e =>
            {
                e.HasIndex(x => x.OrganizationId).IsUnique();
                e.HasMany(x => x.EscalationSteps)
                    .WithOne(x => x.Settings)
                    .HasForeignKey(x => x.PmEngineSettingsId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            m.Entity<PmEscalationStep>().Property(x => x.Role).HasMaxLength(40).IsRequired();

            m.Entity<AnnualBudget>(e =>
            {
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.HasIndex(x => new { x.OrganizationId, x.Year }).IsUnique();
                e.HasMany(x => x.Months).WithOne(x => x.AnnualBudget).HasForeignKey(x => x.AnnualBudgetId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.CategoryShares).WithOne(x => x.AnnualBudget).HasForeignKey(x => x.AnnualBudgetId).OnDelete(DeleteBehavior.Cascade);
            });
            m.Entity<BudgetMonthAllocation>(e =>
            {
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.HasIndex(x => new { x.AnnualBudgetId, x.Month }).IsUnique();
            });
            m.Entity<BudgetCategoryShare>(e =>
            {
                e.Property(x => x.SharePercent).HasPrecision(5, 1);
                e.HasIndex(x => new { x.AnnualBudgetId, x.ExpenseTypeId }).IsUnique();
                // Deleting an expense type drops its budget shares; the NoAction side avoids a
                // multiple-cascade-path error on SQL Server (budget → shares ← type).
                e.HasOne(x => x.ExpenseType).WithMany().HasForeignKey(x => x.ExpenseTypeId).OnDelete(DeleteBehavior.NoAction);
            });

            m.Entity<VehiclePart>(e =>
            {
                e.Property(x => x.Serial).HasMaxLength(60);
                e.Property(x => x.Status).HasMaxLength(30).IsRequired();
                e.Property(x => x.UnitCost).HasPrecision(18, 2);
                e.Property(x => x.InstalledReading).HasPrecision(12, 1);
                e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.Status });
                e.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
                // A catalogue part that is fitted to a vehicle can't be deleted from the catalogue.
                e.HasOne(x => x.PresetPart).WithMany().HasForeignKey(x => x.PresetPartId).OnDelete(DeleteBehavior.Restrict);
            });

            m.Entity<OdometerReading>(e =>
            {
                e.Property(x => x.Value).HasPrecision(12, 1);
                e.Property(x => x.Source).HasMaxLength(60).IsRequired();
                e.Property(x => x.RecordedBy).HasMaxLength(120);
                e.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.ReadingDate });
                e.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
            });

            m.Entity<VehicleIssue>(e =>
            {
                e.Property(x => x.Title).HasMaxLength(160).IsRequired();
                e.Property(x => x.Note).HasMaxLength(1000);
                e.Property(x => x.Priority).HasMaxLength(20).IsRequired();
                e.Property(x => x.Source).HasMaxLength(60).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.Status });
                e.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PresetPart).WithMany().HasForeignKey(x => x.PresetPartId).OnDelete(DeleteBehavior.Restrict);
            });

            m.Entity<Maintenance>(e =>
            {
                e.Property(x => x.Type).HasMaxLength(30);
                e.Property(x => x.Priority).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.TechnicianName).HasMaxLength(120);
                e.Property(x => x.Notes).HasMaxLength(2000);
                e.Property(x => x.Source).HasMaxLength(30);
                e.Property(x => x.SourceRef).HasMaxLength(60);
                e.Property(x => x.EstimatedCost).HasPrecision(18, 2);
                e.Property(x => x.ActualCost).HasPrecision(18, 2);
                e.Property(x => x.OdometerAtRaise).HasPrecision(12, 1);
                e.Property(x => x.OdometerAtService).HasPrecision(12, 1);
                e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.Status });
                e.HasMany(x => x.Tasks).WithOne(x => x.Maintenance).HasForeignKey(x => x.MaintenanceId).OnDelete(DeleteBehavior.Cascade);
            });
            m.Entity<MaintenanceTask>(e =>
            {
                e.Property(x => x.Description).HasMaxLength(200).IsRequired();
                e.Property(x => x.PartCategoryName).HasMaxLength(120);
                e.Property(x => x.TaskCategoryName).HasMaxLength(120);
                e.Property(x => x.Cost).HasPrecision(18, 2);
                e.HasIndex(x => new { x.OrganizationId, x.MaintenanceId, x.Sort });
            });
            m.Entity<Maintenance>().HasIndex(x => new { x.OrganizationId, x.DueDate });
            m.Entity<Maintenance>()
                .HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<Expense>().HasIndex(x => new { x.OrganizationId, x.ExpenseDate });
            m.Entity<Expense>()
                .HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);

            m.Entity<FuelTransaction>().HasIndex(x => new { x.OrganizationId, x.TransactionDate });
            m.Entity<FuelTransaction>()
                .HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<Budget>().HasIndex(x => new { x.OrganizationId, x.Year, x.Month, x.Category });

            m.Entity<Driver>(e =>
            {
                e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
                e.Property(x => x.EmployeeNumber).HasMaxLength(30).IsRequired();
                e.Property(x => x.Status).HasMaxLength(30).IsRequired();
                e.Property(x => x.Phone).HasMaxLength(30);
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.NationalId).HasMaxLength(30);
                e.Property(x => x.LicenseNumber).HasMaxLength(50);
                e.Property(x => x.LicenseClass).HasMaxLength(50).IsRequired();
                e.Property(x => x.Rating).HasPrecision(2, 1);
                e.HasIndex(x => new { x.OrganizationId, x.FullName });
                e.HasIndex(x => new { x.OrganizationId, x.Status });
                e.HasIndex(x => new { x.OrganizationId, x.EmployeeNumber }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.LicenseNumber }).IsUnique();
                e.HasMany(x => x.Documents)
                    .WithOne(x => x.Driver)
                    .HasForeignKey(x => x.DriverId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            m.Entity<DriverDocument>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.FileKey).HasMaxLength(300);
                e.Property(x => x.FileName).HasMaxLength(255);
                e.Property(x => x.ContentType).HasMaxLength(100);
                e.HasIndex(x => new { x.OrganizationId, x.DriverId });
                e.HasIndex(x => new { x.OrganizationId, x.ExpiryDate });
            });

            m.Entity<VehicleAssignment>().HasIndex(x => new { x.OrganizationId, x.VehicleId, x.StartDate });
            m.Entity<VehicleAssignment>()
                .HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
            m.Entity<VehicleAssignment>()
                .HasOne(x => x.Driver)
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<Notification>().HasIndex(x => new { x.OrganizationId, x.IsRead, x.CreatedAt });

            m.Entity<InspectionTemplate>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Scope).HasMaxLength(40).IsRequired();
                e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
                e.HasMany(x => x.Items).WithOne(x => x.Template).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            });
            m.Entity<InspectionTemplateItem>(e =>
            {
                e.Property(x => x.Label).HasMaxLength(150).IsRequired();
                e.Property(x => x.FieldType).HasMaxLength(20).IsRequired();
                e.Property(x => x.Unit).HasMaxLength(20);
                e.Property(x => x.Min).HasPrecision(12, 2);
                e.Property(x => x.Max).HasPrecision(12, 2);
                e.HasIndex(x => new { x.OrganizationId, x.TemplateId, x.Sort });
                // A catalogue part used by a template check can't be deleted from the catalogue.
                e.HasOne(x => x.PresetPart).WithMany().HasForeignKey(x => x.PresetPartId).OnDelete(DeleteBehavior.Restrict);
            });
            m.Entity<Inspection>(e =>
            {
                e.Property(x => x.TemplateName).HasMaxLength(150);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.InspectorName).HasMaxLength(120);
                e.Property(x => x.Odometer).HasPrecision(12, 1);
                e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
                e.HasIndex(x => new { x.OrganizationId, x.VehicleId, x.Status });
                // Deleting a template keeps past runs (they carry a snapshot of the checklist).
                e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
            });
            m.Entity<InspectionItem>(e =>
            {
                e.Property(x => x.ChecklistItem).HasMaxLength(150);
                e.Property(x => x.PartName).HasMaxLength(150);
                e.Property(x => x.FieldType).HasMaxLength(20);
                e.Property(x => x.Unit).HasMaxLength(20);
                e.Property(x => x.Result).HasMaxLength(10);
                e.Property(x => x.FailureReason).HasMaxLength(1000);
                e.Property(x => x.Min).HasPrecision(12, 2);
                e.Property(x => x.Max).HasPrecision(12, 2);
                e.Property(x => x.Value).HasPrecision(12, 2);
            });
            m.Entity<Inspection>().HasIndex(x => new { x.OrganizationId, x.InspectionDate });
            m.Entity<Inspection>()
                .HasOne(x => x.Vehicle)
                .WithMany()
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
            m.Entity<Inspection>()
                .HasMany(x => x.Items)
                .WithOne(x => x.Inspection)
                .HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);
            m.Entity<InspectionItem>().HasIndex(x => new { x.OrganizationId, x.InspectionId });

            m.Entity<AuditLog>().HasIndex(x => new { x.OrganizationId, x.CreatedAt });

            m.Entity<OrganizationSettings>().HasIndex(x => x.OrganizationId).IsUnique();
            m.Entity<PmTask>(e =>
            {
                e.HasIndex(x => x.OrganizationId);
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Trigger).HasMaxLength(20);
                e.Property(x => x.Role).HasMaxLength(80);
                e.Property(x => x.DurationHours).HasPrecision(6, 2);
                e.Property(x => x.EstimatedCost).HasPrecision(18, 2);
                e.HasOne(x => x.PartCategory).WithMany().HasForeignKey(x => x.PartCategoryId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.TaskCategory).WithMany().HasForeignKey(x => x.TaskCategoryId).OnDelete(DeleteBehavior.SetNull);
            });
            m.Entity<Subscription>(e =>
            {
                e.HasIndex(x => x.OrganizationId).IsUnique();
                e.Property(x => x.PlanKey).HasMaxLength(30);
                e.Property(x => x.BillingCycle).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.CardBrand).HasMaxLength(30);
                e.Property(x => x.CardLast4).HasMaxLength(4);
                e.Property(x => x.CardExpiry).HasMaxLength(5);
                e.Property(x => x.CardHolder).HasMaxLength(120);
                e.Property(x => x.BillingEmail).HasMaxLength(200);
            });
            m.Entity<SubscriptionInvoice>(e =>
            {
                e.HasIndex(x => new { x.OrganizationId, x.Year, x.Number }).IsUnique();
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.Property(x => x.Description).HasMaxLength(200);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.Currency).HasMaxLength(3);
            });
        }
    }
}
