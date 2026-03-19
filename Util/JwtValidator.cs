using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

public static class JwtValidator
{
    public static ClaimsPrincipal? ValidateToken(
        string token,
        string issuer,
        string audience,
        string secretKey) // same key used to create the token
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        // key must be same as used when generating token
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,

            ValidateLifetime = true,        // validate exp / nbf
            ClockSkew = TimeSpan.Zero,      // no extra leeway

            // Optional: restrict algorithms
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };

        try
        {
            SecurityToken validatedToken;
            var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

            // extra safety: ensure it's actually a JWT
            if (validatedToken is JwtSecurityToken jwt &&
                jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return principal; // success
            }

            return null;
        }
        catch (SecurityTokenException e)
        {
            // signature, lifetime, issuer, audience, etc. failed
            
            return null;
        }
        catch (Exception e)
        {
            // other errors (bad format, etc.)
            return null;
        }
    }
}