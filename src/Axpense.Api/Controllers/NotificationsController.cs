using Axpense.Core.Features.BaseService.Commands.Models;
using Axpense.Core.Features.BaseService.Queries.Models;
using Axpense.Data.Entities;
using Axpense.Data.Entities.DriverEntities;
using Axpense.Data.Entities.MaintenanceRecord;
using Axpense.Data.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api.Controllers;

[Route("api/notifications")]
public class NotificationsController(IMediator mediator, Axpense.Service.Vehicles.IVehicleService vehicles) : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid organizationId, bool unreadOnly = false)
    {
        var list = await mediator.Send(new GetListwithFilterQuery<Notification>(x => x.OrganizationId == organizationId && x.CurrentState == (int)CurrentStatusType.Active));
        IEnumerable<Notification> q = list;
        if (unreadOnly) q = q.Where(x => !x.IsRead);
        return Ok(q.OrderByDescending(x => x.CreatedAt).Take(100));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Notification n)
    {
        var (success, id) = await mediator.Send(new AddAsyncGetIDCommand<Notification>(n, CurrentUserId));
        if (!success) return BadRequest("Could not create notification.");
        return Ok(n);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        var n = await mediator.Send(new GetEntityByIdQuery<Notification>(id));
        if (n is null) return NotFound();

        n.IsRead = true;
        var affected = await mediator.Send(new UpdateCommand<Notification>(n, CurrentUserId));
        return affected <= 0 ? NotFound() : Ok(n);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid organizationId)
    {
        var now = DateTime.UtcNow.Date;
        var until = now.AddDays(30);
        var created = 0;

        var existing = await mediator.Send(new GetListwithFilterQuery<Notification>(x => x.OrganizationId == organizationId));

        var maintenance = await mediator.Send(new GetListwithFilterQuery<Maintenance>(x => x.OrganizationId == organizationId && x.CompletedAtUtc == null && x.DueDate <= until));
        foreach (var m in maintenance)
        {
            if (!existing.Any(n => n.Type == "Maintenance" && n.DueDate == m.DueDate && n.Message.Contains(m.VehicleId.ToString())))
            {
                var notification = new Notification
                {
                    OrganizationId = organizationId,
                    Type = "Maintenance",
                    Title = "Maintenance due",
                    Message = $"Vehicle {m.VehicleId} has maintenance due on {m.DueDate:yyyy-MM-dd}. Reference: {m.VehicleId}",
                    Severity = m.DueDate < now ? "Critical" : "Warning",
                    DueDate = m.DueDate
                };
                await mediator.Send(new AddAsyncGetIDCommand<Notification>(notification, CurrentUserId));
                created++;
            }
        }

        var drivers = await mediator.Send(new GetListwithFilterQuery<Driver>(x => x.OrganizationId == organizationId && x.LicenseExpiryDate != null && x.LicenseExpiryDate <= until));
        foreach (var d in drivers)
        {
            if (!existing.Any(n => n.Type == "License" && n.Message.Contains(d.Id.ToString())))
            {
                var notification = new Notification
                {
                    OrganizationId = organizationId,
                    Type = "License",
                    Title = "Driver license expiring",
                    Message = $"Driver {d.FullName} has a license expiring on {d.LicenseExpiryDate:yyyy-MM-dd}. Reference: {d.Id}",
                    Severity = d.LicenseExpiryDate < now ? "Critical" : "Warning",
                    DueDate = d.LicenseExpiryDate
                };
                await mediator.Send(new AddAsyncGetIDCommand<Notification>(notification, CurrentUserId));
                created++;
            }
        }

        var documents = await mediator.Send(new GetListwithFilterQuery<DriverDocument>(x => x.OrganizationId == organizationId && x.CurrentState == (int)CurrentStatusType.Active && x.ExpiryDate <= until));
        if (documents.Count > 0)
        {
            var driverNames = (await mediator.Send(new GetListwithFilterQuery<Driver>(x => x.OrganizationId == organizationId)))
                .ToDictionary(d => d.Id, d => d.FullName);
            foreach (var doc in documents)
            {
                if (existing.Any(n => n.Type == "DriverDocument" && n.Message.Contains(doc.Id.ToString()))) continue;
                var notification = new Notification
                {
                    OrganizationId = organizationId,
                    Type = "DriverDocument",
                    Title = doc.ExpiryDate < now ? "Driver document expired" : "Driver document expiring",
                    Message = $"{driverNames.GetValueOrDefault(doc.DriverId, "Driver")} — {doc.Name} {(doc.ExpiryDate < now ? "expired" : "expires")} on {doc.ExpiryDate:yyyy-MM-dd}. Reference: {doc.Id}",
                    Severity = doc.ExpiryDate < now ? "Critical" : "Warning",
                    DueDate = doc.ExpiryDate
                };
                await mediator.Send(new AddAsyncGetIDCommand<Notification>(notification, CurrentUserId));
                created++;
            }
        }

        // Vehicle renewals (insurance, registration) and maintenance (PM runway, part wear, warranty claims).
        // Same rules as the vehicle profile banners; one notification per alert key.
        foreach (var (vehicleId, vehicleName, plate, alert) in await vehicles.FleetAlertsAsync(organizationId, HttpContext.RequestAborted))
        {
            if (existing.Any(n => n.Message.Contains($"Reference: {alert.Key}"))) continue;
            var notification = new Notification
            {
                OrganizationId = organizationId,
                Type = alert.Kind is "insurance" or "registration" ? "VehicleRenewal" : "VehicleMaintenance",
                Title = $"{plate} — {alert.Title}",
                Message = $"{vehicleName} · {alert.Detail} Reference: {alert.Key}",
                Severity = alert.Severity == "critical" ? "Critical" : alert.Severity == "warning" ? "Warning" : "Info",
                DueDate = alert.DueDate
            };
            await mediator.Send(new AddAsyncGetIDCommand<Notification>(notification, CurrentUserId));
            created++;
        }

        return Ok(new { created });
    }
}
