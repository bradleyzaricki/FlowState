using System.ComponentModel.DataAnnotations;

namespace FlowState.DTOs.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest(
    [Required] string RefreshToken);

public record LogoutRequest(
    [Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
