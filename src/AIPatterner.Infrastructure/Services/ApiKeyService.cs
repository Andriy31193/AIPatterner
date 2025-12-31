// Service for API key generation and hashing
namespace AIPatterner.Infrastructure.Services;

using AIPatterner.Application.Handlers;
using System.Security.Cryptography;
using System.Text;

public class ApiKeyService : IApiKeyService
{
    public string GenerateApiKey()
    {
        // Generate a secure random API key
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var keyBuilder = new StringBuilder();
        
        // Generate 64 character key
        for (int i = 0; i < 64; i++)
        {
            keyBuilder.Append(chars[random.Next(chars.Length)]);
        }
        
        return $"ak_{keyBuilder}";
    }

    public string HashApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool VerifyApiKey(string apiKey, string keyHash)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(keyHash))
            return false;

        var computedHash = HashApiKey(apiKey);
        return computedHash == keyHash;
    }

    public string GetKeyPrefix(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 8)
            return "****";
        
        return apiKey.Substring(0, 8);
    }
}

