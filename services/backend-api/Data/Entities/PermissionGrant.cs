using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("permission_grants")]
[Index("UserId", "PermissionCode", Name = "idx_permission_grants_user")]
public partial class PermissionGrant
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("permission_code")]
    public string PermissionCode { get; set; } = null!;

    [Column("granted")]
    public bool Granted { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("granted_by")]
    public Guid GrantedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("GrantedBy")]
    [InverseProperty("PermissionGrantGrantedByNavigations")]
    public virtual User GrantedByNavigation { get; set; } = null!;

    [ForeignKey("PermissionCode")]
    [InverseProperty("PermissionGrants")]
    public virtual Permission PermissionCodeNavigation { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("PermissionGrantUsers")]
    public virtual User User { get; set; } = null!;
}
