using Axpense.Data.Constants;
using Axpense.Data.Entities.VehicleEntities;

namespace Axpense.Service.Fleet
{
    public sealed record WarrantyState(
        bool Covered,
        // covered | expired | none
        string State,
        string Label,
        bool EndingSoon,
        int? DaysLeft,
        decimal? KmLeft);

    public sealed record PartWear(
        // ok | due | expired | retired | untracked
        string Status,
        decimal Percent,
        string Label,
        string? Detail,
        decimal DistanceRun,
        int DaysRun,
        WarrantyState Warranty,
        /// <summary>Covered and worn: replace free under warranty now.</summary>
        bool ClaimWarranty);

    /// <summary>
    /// Wear of a fitted part: distance since its activation reading (or months since activation)
    /// against its lifespan; due from 85%, expired at 100%. Warranty holds while the date has not
    /// passed and the optional distance cap has not been reached.
    /// </summary>
    public static class PartWearCalculator
    {
        public static PartWear Evaluate(VehiclePart p, decimal? currentReading, string unit, DateTime today)
        {
            var run = Math.Max(0, (currentReading ?? p.InstalledReading) - p.InstalledReading);
            var daysRun = Math.Max(0, (today - p.InstalledDate.Date).Days);

            int? wDays = p.WarrantyUntil is null ? null : (p.WarrantyUntil.Value.Date - today).Days;
            decimal? wKmLeft = p.WarrantyKm is null ? null : p.WarrantyKm - run;
            var covered = p.Status != VehicleConstants.PartRetired && wDays is >= 0 && (wKmLeft is null || wKmLeft > 0);
            var wLabel = wDays is null ? "No cover"
                : covered ? $"Covered to {p.WarrantyUntil:dd MMM yyyy}{(wKmLeft is null ? "" : $" · {wKmLeft:N0} {unit} left")}"
                : wKmLeft is <= 0 && wDays >= 0 ? $"Distance cap reached ({p.WarrantyKm:N0} {unit})"
                : $"Expired {p.WarrantyUntil:dd MMM yyyy}";
            var ending = covered && (wDays <= VehicleConstants.WarrantyEndingDays || wKmLeft <= VehicleConstants.WarrantyEndingKm);
            var warranty = new WarrantyState(covered, covered ? "covered" : wDays is null ? "none" : "expired", wLabel, ending, wDays, wKmLeft);

            if (p.Status == VehicleConstants.PartRetired)
                return new PartWear("retired", 100, "Retired", $"{run:N0} {unit} run", run, daysRun, warranty, false);

            string status;
            decimal pct;
            string label, detail;
            if (p.LifespanKm is > 0)
            {
                pct = run / p.LifespanKm.Value * 100;
                label = pct >= 100 ? $"Past life by {run - p.LifespanKm.Value:N0} {unit}" : $"{p.LifespanKm.Value - run:N0} {unit} left";
                detail = $"{run:N0} / {p.LifespanKm.Value:N0} {unit}";
            }
            else if (p.LifespanMonths is > 0)
            {
                var total = p.LifespanMonths.Value * 30m;
                pct = daysRun / total * 100;
                label = pct >= 100 ? $"Past life by {Math.Round((daysRun - total) / 30)} mo" : $"{Math.Round((total - daysRun) / 30)} mo left";
                detail = $"{Math.Round(daysRun / 30m)} / {p.LifespanMonths.Value} months";
            }
            else
            {
                return new PartWear("untracked", 0, "No lifespan set", $"{run:N0} {unit} run", run, daysRun, warranty, false);
            }

            status = pct >= 100 ? "expired" : pct >= VehicleConstants.PartDueThresholdPercent ? "due" : "ok";
            return new PartWear(status, Math.Round(pct, 1), label, detail, run, daysRun, warranty, covered && status is "due" or "expired");
        }
    }
}
