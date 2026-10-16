namespace Catalog.Api.Entities;

public partial class Order
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Address { get; set; } = "";
}
