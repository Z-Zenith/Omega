using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("whitelist_requests")]
public partial class WhitelistRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("requested_by")]
    public Guid RequestedBy { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [ForeignKey("RequestedBy")]
    [InverseProperty("WhitelistRequestRequestedByNavigations")]
    public virtual User RequestedByNavigation { get; set; } = null!;

    [ForeignKey("ReviewedBy")]
    [InverseProperty("WhitelistRequestReviewedByNavigations")]
    public virtual User? ReviewedByNavigation { get; set; }
}
