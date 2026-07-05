using System.ComponentModel.DataAnnotations.Schema;

namespace BackendApi.Data.Entities;

public partial class Document
{
    [Column("doc_type")]
    public DocType DocType { get; set; }
}
