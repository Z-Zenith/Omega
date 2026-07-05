using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class User
{
    [Column("account_type")]
    public AccountType AccountType { get; set; }
}
