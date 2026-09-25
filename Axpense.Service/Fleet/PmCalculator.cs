using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Service.Settings;

namespace Axpense.Service.Fleet
{
    public sealed record PmLeg(string Kind, decimal Used, decimal Total, decimal Remaining, decimal Percent, int AlertAt, string Unit);

    /// <summary>The vehicle's preventive-maintenance runway.</summary>
    public sealed record PmState(
        // none | ok | due | overdue
        string Status,
        decimal Percent,
        string Label,
        string? Detail,
        string Unit,
        decimal? PreAlertPercent,
        string Trigger,
        int? DaysOverdue,
        // The leg that fires first (null when the vehicle has no PM schedule).
        PmLeg? Lead = null);

    /// <summary>
    /// Vehicle PM schedule: each configured interval (distance, engine hours, time) is a "leg"; the leg
    /// with the highest used share leads ("whichever comes first"). Overdue when the lead leg runs out;
    /// due when it is within the PM engine pre-alert (km, hours or days).
    /// </summary>
    public static class PmCalculator
    {
        public static PmState Evaluate(Vehicle v, DateTime today, PmRules rules)
        {
            var legs = new List<PmLeg>();
            var reading = v.CurrentOdometer ?? 0;
            var used = Math.Max(0, reading - (v.PmLastServiceReading ?? 0));
            var usage = v.PmTrigger is VehicleConstants.PmUsage or VehicleConstants.PmHybrid;
            var hoursUnit = v.ReadingUnit == VehicleConstants.UnitHours;

            if (usage && !hoursUnit && v.PmIntervalKm is > 0)
                legs.Add(Leg("distance", used, v.PmIntervalKm.Value, rules.DistancePreAlertKm, "km"));
            if ((v.PmTrigger == VehicleConstants.PmHours || (usage && hoursUnit)) && v.PmIntervalHours is > 0)
                legs.Add(Leg("engine hours", used, v.PmIntervalHours.Value, rules.EngineHourPreAlert, "hr"));
            if (v.PmTrigger is VehicleConstants.PmTime or VehicleConstants.PmHybrid && v.PmIntervalDays is > 0 && v.PmLastServiceDate is not null)
                legs.Add(Leg("time", Math.Max(0, (today - v.PmLastServiceDate.Value.Date).Days), v.PmIntervalDays.Value, rules.TimePreAlertDays, "days"));

            if (legs.Count == 0)
                return new PmState("none", 0, "No PM schedule", null, hoursUnit ? "hr" : "km", null, v.PmTrigger, null);

            var lead = legs.OrderByDescending(l => l.Percent).First();
            var status = lead.Remaining <= 0 ? "overdue" : lead.Remaining <= lead.AlertAt ? "due" : "ok";
            var label = status == "overdue"
                ? $"Overdue by {Math.Abs(Math.Round(lead.Remaining)):N0} {lead.Unit}"
                : $"{Math.Round(lead.Remaining):N0} {lead.Unit} to service";
            var detail = $"{lead.Kind} interval · {lead.Used:N0} of {lead.Total:N0} {lead.Unit} used" +
                         (v.PmTrigger == VehicleConstants.PmHybrid ? " · hybrid, first to hit" : "");
            var tick = lead.Total > 0 ? Math.Clamp((lead.Total - lead.AlertAt) / lead.Total * 100, 0, 100) : (decimal?)null;

            // Days overdue drives escalation; for distance/hours legs use the time since the last service.
            int? daysOverdue = null;
            if (status == "overdue")
                daysOverdue = lead.Unit == "days"
                    ? (int)Math.Abs(lead.Remaining)
                    : v.PmLastServiceDate is null ? 0 : Math.Max(0, (today - v.PmLastServiceDate.Value.Date).Days - (v.PmIntervalDays ?? 0));

            return new PmState(status, Math.Round(lead.Percent, 1), label, detail, lead.Unit, tick, v.PmTrigger, daysOverdue, lead);
        }

        public static bool DispatchBlocked(Vehicle v, PmState pm, PmRules rules) =>
            rules.BlockDispatchOnCriticalOverdue && pm.Status == "overdue" && v.Status != VehicleConstants.StatusRetired;

        private static PmLeg Leg(string kind, decimal used, int total, int alertAt, string unit) =>
            new(kind, used, total, total - used, Math.Min(140, used / total * 100), alertAt, unit);
    }
}
