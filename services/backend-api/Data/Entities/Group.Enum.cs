using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class Group
{
    [Column("type")]
    public GroupType Type { get; set; }
}
