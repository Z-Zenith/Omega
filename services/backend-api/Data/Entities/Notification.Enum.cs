using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class Notification
{
    [Column("type")]
    public NotificationType Type { get; set; }
}
