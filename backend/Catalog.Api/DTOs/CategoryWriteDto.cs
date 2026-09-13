using System.ComponentModel.DataAnnotations;
namespace Catalog.Api.DTOs;
public record CategoryWriteDto([Required, StringLength(100)] string Name, [StringLength(2000)] string Description);
