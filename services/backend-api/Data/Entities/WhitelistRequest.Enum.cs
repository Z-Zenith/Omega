using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class WhitelistRequest
{
    [Column("status")]
    public WhitelistRequestStatus Status { get; set; }
}
