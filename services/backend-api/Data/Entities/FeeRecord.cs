using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("fee_records")]
public partial class FeeRecord
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("due_date")]
    public DateOnly DueDate { get; set; }

    [Column("payment_link")]
    public string? PaymentLink { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [InverseProperty("FeeRecord")]
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    [ForeignKey("StudentId")]
    [InverseProperty("FeeRecords")]
    public virtual User Student { get; set; } = null!;
}
