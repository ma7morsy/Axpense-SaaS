namespace Axpense.Data.Entities.SaasEntities
{
    public class AuditLog : TenantEntity
    {
        public Guid? UserId { get; set; }
        public string Action { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string? EntityId { get; set; }
        public string? Details { get; set; }
    }
}
