using System;
using System.Collections.Generic;
using System.Text;

namespace Axpense.Data.Entities
{
    public class Notification : TenantEntity
    {
        public string Type { get; set; } = "Reminder"; 
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = "Info";
        public bool IsRead { get; set; } = false;
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
