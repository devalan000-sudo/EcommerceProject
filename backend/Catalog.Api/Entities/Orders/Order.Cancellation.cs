namespace Catalog.Api.Entities;

public partial class Order
{
    public bool CancellationPending { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancellationRequestedAtUtc { get; set; }
    public int? CancellationAdminId { get; set; }
}
