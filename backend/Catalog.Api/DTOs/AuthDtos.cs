using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.DTOs;

public record LoginRequestDto(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(256)] string Password);
public record LoginResponseDto(string Token, DateTime ExpiresAtUtc);
