using Axpense.Data.Constants;
using Axpense.Data.Entities;
using Axpense.Service.Fleet;
using Axpense.Service.Settings;
using Axpense.Service.Vehicles.Dtos;

namespace Axpense.Service.Vehicles
{
    public sealed record PartWearInfo(Guid PartId, string Code, string Name, PartWear Wear);

    /// <summary>
    /// Renewal and maintenance notices for one vehicle. The same rules feed the profile banners, the
    /// register's alert count and the notification generator, so they never disagree.
    /// <list type="bullet">
    /// <item>Preventive maintenance: overdue (critical; dispatch-blocked when the PM engine rule is on) or due within the pre-alert.</item>
    /// <item>Insurance / registration: expired (critical) or renewing within <see cref="VehicleConstants.RenewalReminderDays"/> days (warning).</item>
    /// <item>Fitted parts: past their service life (critical) or ≥ 85 % worn (warning); worn parts still under warranty are flagged to claim.</item>
    /// </list>
    /// Retired vehicles raise nothing.
    /// </summary>
    public static class VehicleAlerts
    {
        public static List<VehicleAlertDto> For(Vehicle v, PmState pm, bool blocked, PmRules rules, IEnumerable<PartWearInfo> parts, DateTime today)
        {
            var list = new List<VehicleAlertDto>();
            if (v.Status == VehicleConstants.StatusRetired) return list;
            var name = $"{v.Make} {v.Model}".Trim();

            if (pm.Status == "overdue")
            {
                var escalated = rules.EscalatedTo(pm.DaysOverdue ?? 0);
                list.Add(new VehicleAlertDto("pm", "critical",
                    blocked ? "Dispatch blocked — preventive maintenance overdue" : "Preventive maintenance overdue",
                    $"{pm.Label}.{(blocked ? " Safety rule blocks usage until the work order is closed." : "")} Escalated to {escalated}.",
                    "raise", null, $"pm:{v.Id}:overdue:{v.PmLastServiceDate:yyyyMMdd}:{v.PmLastServiceReading}"));
            }
            else if (pm.Status == "due")
            {
                list.Add(new VehicleAlertDto("pm", "warning", "Preventive maintenance due soon",
                    $"{pm.Label} — pre-alert fires at {rules.DistancePreAlertKm:N0} km or {rules.TimePreAlertDays} days ahead.",
                    "schedule", null, $"pm:{v.Id}:due:{v.PmLastServiceDate:yyyyMMdd}:{v.PmLastServiceReading}"));
            }

            Renewal(list, v, "insurance", "Insurance", v.InsuranceRenewalDate,
                $"{v.InsuranceProvider ?? "Insurer not set"}{(v.InsurancePolicyNumber is null ? "" : $" · policy {v.InsurancePolicyNumber}")}", today);
            Renewal(list, v, "registration", "Registration", v.RegistrationRenewalDate,
                $"{v.RegistrationAuthority ?? "Authority not set"}{(v.RegistrationNumber is null ? "" : $" · {v.RegistrationNumber}")}", today);

            foreach (var p in parts)
            {
                var w = p.Wear;
                if (w.Status is not ("due" or "expired")) continue;
                if (w.ClaimWarranty)
                    list.Add(new VehicleAlertDto("warranty", "warning", $"{p.Name} — claim while covered",
                        $"{p.Code} worn to {Math.Round(w.Percent)}% · {w.Warranty.Label}", "claim", null, $"part:{p.PartId}:claim"));
                else
                    list.Add(new VehicleAlertDto("part", w.Status == "expired" ? "critical" : "warning",
                        w.Status == "expired" ? $"{p.Name} past its service life" : $"{p.Name} near the end of its service life",
                        $"{p.Code} · {w.Label}{(w.Detail is null ? "" : $" ({w.Detail})")}", "replace", null, $"part:{p.PartId}:{w.Status}"));
            }

            return list
                .OrderBy(a => a.Severity == "critical" ? 0 : a.Severity == "warning" ? 1 : 2)
                .ToList();

            void Renewal(List<VehicleAlertDto> target, Vehicle veh, string kind, string label, DateTime? date, string detail, DateTime day)
            {
                if (date is null) return;
                var days = (date.Value.Date - day).Days;
                if (days < 0)
                    target.Add(new VehicleAlertDto(kind, "critical", $"{label} expired {Math.Abs(days)} day{(days == -1 ? "" : "s")} ago",
                        detail, "renew", date, $"{kind}:{veh.Id}:{date:yyyyMMdd}:expired"));
                else if (days <= VehicleConstants.RenewalReminderDays)
                    target.Add(new VehicleAlertDto(kind, "warning",
                        days == 0 ? $"{label} renews today" : $"{label} renews in {days} day{(days == 1 ? "" : "s")}",
                        $"{detail} · {date:dd MMM yyyy}", "renew", date, $"{kind}:{veh.Id}:{date:yyyyMMdd}:due"));
            }
        }
    }
}
