using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("whitelist_sites")]
[Index("CollegeId", "Url", Name = "whitelist_sites_college_id_url_key", IsUnique = true)]
public partial class WhitelistSite
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("college_id")]
    public Guid CollegeId { get; set; }

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("approved_at")]
    public DateTime ApprovedAt { get; set; }

    [ForeignKey("CollegeId")]
    [InverseProperty("WhitelistSites")]
    public virtual College College { get; set; } = null!;
}
