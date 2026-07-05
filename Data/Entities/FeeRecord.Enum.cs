using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class FeeRecord
{
    [Column("status")]
    public FeeStatus Status { get; set; }
}
