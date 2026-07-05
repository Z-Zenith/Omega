using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class Assignment
{
    [Column("type")]
    public AssignmentType Type { get; set; }
}
