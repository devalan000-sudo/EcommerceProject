using System.ComponentModel.DataAnnotations;
using Catalog.Api.DTOs;
using Xunit;

public class ValidationTests
{
    private static List<ValidationResult> Validate(object value)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), errors, true);
        return errors;
    }

    [Theory]
    [InlineData("https://example.com/photo.png", true)]
    [InlineData("http://localhost:5173/images/products/1001.png", true)]
    [InlineData("/images/photo.png", false)]
    [InlineData("file:///photo.png", false)]
    [InlineData("javascript:alert(1)", false)]
    public void ProductAcceptsOnlyAbsoluteHttpImageUrls(string url, bool valid)
    {
        var dto = new ProductCreateDto { Name = "Playera", Price = 100, CategoryId = 1, ImageUrl = url };
        Assert.Equal(valid, Validate(dto).Count == 0);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("10.123", false)]
    [InlineData("0.01", true)]
    [InlineData("9999999999.99", true)]
    [InlineData("10000000000", false)]
    public void ProductValidatesPriceRangeAndPrecision(string price, bool valid)
    {
        var dto = new ProductUpdateDto { Name = "Playera", Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), CategoryId = 1, ImageUrl = "https://example.com/a.png" };
        Assert.Equal(valid, Validate(dto).Count == 0);
    }

    [Fact]
    public void ProfileRejectsAddressWhitespaceAndPhoneWithoutEnoughDigits()
    {
        var dto = new CustomerProfileDto { FirstName = "Ana", LastName = "López", Email = "ana@example.com", Address = "          ", Phone = "() - ()" };
        var errors = Validate(dto);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.Address)));
        dto.Address = "Calle Uno 123, Centro";
        // DataAnnotations no invoca IValidatableObject si fallan atributos de propiedades.
        Assert.Contains(Validate(dto), error => error.MemberNames.Contains(nameof(dto.Phone)));
        dto.Phone = null;
        Assert.Empty(Validate(dto));
    }
}
