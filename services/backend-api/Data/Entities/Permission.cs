using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("permissions")]
public partial class Permission
{
    [Key]
    [Column("code")]
    public string Code { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [InverseProperty("PermissionCodeNavigation")]
    public virtual ICollection<PermissionGrant> PermissionGrants { get; set; } = new List<PermissionGrant>();

    [ForeignKey("PermissionCode")]
    [InverseProperty("PermissionCodes")]
    public virtual ICollection<Role> RoleCodes { get; set; } = new List<Role>();
}
