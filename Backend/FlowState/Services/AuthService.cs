using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FlowState.Data;
using FlowState.DTOs.Auth;
using FlowState.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FlowState.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(UserManager<ApplicationUser> userManager, AppDbContext db, IConfiguration config)
    {
        _userManager = userManager;
        _db = db;
        _config = config;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException();

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException();

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
    {
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

        if (stored is null || stored.IsRevoked || stored.Expiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException();

        stored.IsRevoked = true;
        await _db.SaveChangesAsync();

        return await IssueTokensAsync(stored.User);
    }

    public async Task LogoutAsync(LogoutRequest request)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

        if (stored is null || stored.IsRevoked)
            return;

        stored.IsRevoked = true;
        await _db.SaveChangesAsync();
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var (jwt, expiry) = GenerateJwt(user);
        var refreshToken = await GenerateRefreshTokenAsync(user);
        return new AuthResponse(jwt, refreshToken.Token, expiry);
    }

    private (string Token, DateTime Expiry) GenerateJwt(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = _config.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15);
        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }

    private async Task<RefreshToken> GenerateRefreshTokenAsync(ApplicationUser user)
    {
        var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);
        var token = new RefreshToken
        {
            UserId = user.Id,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Expiry = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTime.UtcNow,
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();
        return token;
    }
}
