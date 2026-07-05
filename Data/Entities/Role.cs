using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("roles")]
public partial class Role
{
    [Key]
    [Column("code")]
    public string Code { get; set; } = null!;

    [InverseProperty("RoleCodeNavigation")]
    public virtual ICollection<RoleBinding> RoleBindings { get; set; } = new List<RoleBinding>();

    [ForeignKey("RoleCode")]
    [InverseProperty("RoleCodes")]
    public virtual ICollection<Permission> PermissionCodes { get; set; } = new List<Permission>();
}
