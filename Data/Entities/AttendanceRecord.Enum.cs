using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class AttendanceRecord
{
    [Column("status")]
    public AttendanceStatus Status { get; set; }
}
