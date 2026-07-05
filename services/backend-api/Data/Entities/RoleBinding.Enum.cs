using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class RoleBinding
{
    [Column("scope_type")]
    public ScopeKind ScopeType { get; set; }
}
