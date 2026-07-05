using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("colleges")]
public partial class College
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("College")]
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    [InverseProperty("College")]
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    [InverseProperty("College")]
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    [InverseProperty("College")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    [InverseProperty("College")]
    public virtual ICollection<WhitelistSite> WhitelistSites { get; set; } = new List<WhitelistSite>();
}
