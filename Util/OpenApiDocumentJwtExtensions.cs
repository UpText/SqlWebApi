namespace sqlwebapi;

using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System.Linq;

public static class OpenApiDocumentJwtExtensions
{
    /// <summary>
    /// Adds a HTTP Bearer (JWT) security scheme and a global requirement to an existing OpenApiDocument.
    /// Optionally also tags every operation with the requirement (for tools that ignore top-level requirements).
    /// </summary>
    public static void AddJwtBearer(this OpenApiDocument doc,
                                    string schemeName = "Bearer",
                                    bool alsoApplyToOperations = false,
                                    string? description = "JWT Bearer authorization using the Bearer scheme.")
    {
        // Ensure components exists
        doc.Components ??= new OpenApiComponents();

        // Create or update the security scheme
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,      // proper OpenAPI v3 bearer auth
            Scheme = "bearer",                   // MUST be lowercase "bearer"
            BearerFormat = "JWT",
            Description = description
        };

        if (doc.Components.SecuritySchemes == null)
            doc.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>();

        doc.Components.SecuritySchemes[schemeName] = scheme;

        // Add a top-level security requirement if missing
        var requirement = new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = schemeName
                    }
                },
                new List<string>() // no specific scopes
            }
        };

        doc.SecurityRequirements ??= new List<OpenApiSecurityRequirement>();
        bool alreadyAdded = doc.SecurityRequirements.Any(r =>
            r.Keys.Any(k => k.Reference?.Id == schemeName));
        if (!alreadyAdded)
            doc.SecurityRequirements.Add(requirement);

        // (Optional) also stamp each operation with the requirement
        if (alsoApplyToOperations && doc.Paths != null)
        {
            foreach (var path in doc.Paths.Values)
            {
                foreach (var op in path.Operations.Values)
                {
                    op.Security ??= new List<OpenApiSecurityRequirement>();
                    bool opHas = op.Security.Any(r => r.Keys.Any(k => k.Reference?.Id == schemeName));
                    if (!opHas)
                        op.Security.Add(requirement);
                }
            }
        }
    }
}
