using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Tokens;
using SqlWebApi.Configuration;

namespace SqlWebApi;

public static class ParseSecurityHeader
{
    public static JwtSecurityToken? Parse(HttpHeadersCollection headers)
    {
        if (headers.TryGetValues("Authorization", out var header))
        {
            var data = header.First();
            var stringToken = data.Substring(7); // Skip Bearer
            
            var secretKey = ConfigDefaults.GetRequiredValue("JWT_SECRET");
            var issuer = ConfigDefaults.GetRequiredValue("JWT_ISSUER");
            var audience = ConfigDefaults.GetRequiredValue("JWT_AUDIENCE");

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
        
        var secretKey = ConfigDefaults.GetRequiredValue("JWT_SECRET");
        var issuer = ConfigDefaults.GetRequiredValue("JWT_ISSUER");
        var audience = ConfigDefaults.GetRequiredValue("JWT_AUDIENCE");
        var hours = ConfigDefaults.GetValue("JWT_HOURS") ?? "8";

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
