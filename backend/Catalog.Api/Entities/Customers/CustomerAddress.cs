namespace Catalog.Api.Entities;

public class CustomerAddress
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Label { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string Street { get; set; } = "";
    public string ExteriorNumber { get; set; } = "";
    public string InteriorNumber { get; set; } = "";
    public string Neighborhood { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
    public string References { get; set; } = "";
    public bool IsDefault { get; set; }
    public Guid Version { get; set; }
}
