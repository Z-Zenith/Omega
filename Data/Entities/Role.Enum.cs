using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class Role
{
    [Column("default_scope_kind")]
    public ScopeKind DefaultScopeKind { get; set; }
}
