using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Tokens;

namespace SqlWebApi;

public static class ParseSecurityHeader
{
    public static JwtSecurityToken? Parse(HttpHeadersCollection headers)
    {
        if (headers.TryGetValues("Authorization", out var header))
        {
            var data = header.First();
            var stringToken = data.Substring(7); // Skip Bearer
            
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new Exception("JWT_SECRET environment variable is not set.");
            }
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            if (string.IsNullOrWhiteSpace(issuer))
            {
                throw new Exception("JWT_ISSUER environment variable is not set.");
            }
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
            if (string.IsNullOrWhiteSpace(audience))        {
                throw new Exception("JWT_AUDIENCE environment variable is not set.");
            }

            var principal = JwtValidator.ValidateToken(
                token: stringToken,
                issuer: issuer,
                audience: audience,
                secretKey: secretKey
            ); 
            
            if (principal == null)
                return null;
            
            var handler = new JwtSecurityTokenHandler();
            var decoded = handler.ReadJwtToken(stringToken);
            return decoded;
        }
        else
        {
            return null;
        }
    }

    public static string? GetUserEmail(HttpHeadersCollection headers)
    {
        var token = Parse(headers);
        if (token == null)
            return null;
        var claimValue = token.Claims.FirstOrDefault(c => c.Type == "https://uptext.com/upemail")?.Value;
        if (claimValue == null)
            claimValue = token.Claims.FirstOrDefault(c => c.Type == "useremail")?.Value;
        if (claimValue == null)
            claimValue = token.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        return claimValue;
    }
    
    public static string? GetValue(string name,HttpHeadersCollection headers)
    {
        var token = Parse(headers);
        if (token == null)
            return null;
        
        var claimValue = token.Claims.FirstOrDefault(c => c.Type == "https://uptext.com/" + name)?.Value;
        if (claimValue == null)
            claimValue = token.Claims.FirstOrDefault(c => c.Type == name)?.Value;
        return claimValue;
    }
}

public static class ParseAndGetToken
{
    public static JwtSecurityToken? Parse(string jsonString, IEnumerable<Claim>? additionalClaims = null)
    {
        using JsonDocument document = JsonDocument.Parse(jsonString);
        JsonElement element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
            element = element.EnumerateArray().First();
        
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new Exception("JWT_SECRET environment variable is not set.");
        }
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new Exception("JWT_ISSUER environment variable is not set.");
        }
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new Exception("JWT_AUDIENCE environment variable is not set.");
        }
        var hours = Environment.GetEnvironmentVariable("JWT_HOURS");
        if (string.IsNullOrWhiteSpace(hours))
        {
            throw new Exception("JWT_HOURS environment variable is not set.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        if (element.ValueKind == JsonValueKind.Array  && !element.EnumerateArray().Any())
            return null;
 
        int countAttr = 0;
        foreach (var property in element.EnumerateObject())
            countAttr++;
        countAttr += additionalClaims?.Count() ?? 0;

        Claim[] claims = new Claim[countAttr];
        countAttr = 0;
        foreach (var property in element.EnumerateObject())
            claims[countAttr++] = new Claim(property.Name, property.Value.ToString());

        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
                claims[countAttr++] = claim;
        }

       
        var token = new JwtSecurityToken(
            issuer: issuer,               
            audience: audience,          
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(int.Parse(hours)),
            signingCredentials: signingCredentials
        );

        return token;
         
            
    }
}
