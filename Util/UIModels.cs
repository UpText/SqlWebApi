using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace sqlwebapi;

public class SqlSiteDocument
{
    public required string Name { get; set; }
    public  required List<UiResource> Resources { get; set; }
    public string Serialize()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("SqlSiteDocument.Name is required.");

        var payload = new
        {
            name = Name,
            resources = (Resources ?? new()).Select(r => new
            {
                name = r.Name,
                recordRepresentation = r.RecordRepresentation,
                icon = r.Icon ?? string.Empty,
                options = new
                {
                    label = r.Options?.Label ?? r.Name,
                    hasEdit = r.Options?.HasEdit ?? false,
                    hasDelete = r.Options?.HasDelete ?? false,
                    hasCreate = r.Options?.HasCreate ?? false,
                    hasPagination = r.Options?.HasPagination ?? false,
                    hasSearch = r.Options?.HasSearch ?? false,
                    hasSort = r.Options?.HasSort ?? false
                   
                    
                },
                ui = r.ui,
                fields = (r.Fields ?? new()).Select(f => new
                {
                    source = f.Source,
                    type = f.Type,
                    view = (IEnumerable<string>)(f.View ?? new List<string>()),
                    validators = (IEnumerable<string>)(f.Validators ?? new List<string>()),
                    reference = f.Reference
                }).ToList()
            }).ToList()
        };

        var jsonOptions = new JsonSerializerOptions
        {
            // property names already camel-cased above
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(payload, jsonOptions);
    }
    
}
public class UiResource
{
    public required string Name { get; set; }
    public required string RecordRepresentation { get; set; }
    public string Icon { get; set; } = "PostIcon";
    public required Options Options { get; set; }
    public required JsonElement ui { get; set; }
    public required List<Field> Fields { get; set; }
}

public class Options
{
    public required string Label { get; set; }
    public required bool HasEdit { get; set; }
    public required bool HasCreate { get; set; }
    public required bool HasDelete { get; set; }
    public required bool HasPagination { get; set; }
    public bool HasSearch { get; set; }
    public bool HasSort { get; set; } 

}

public class Field
{
    public required string Source { get; set; }
    public required string Type { get; set; }
    public required List<string> View { get; set; }
    public required List<string> Validators { get; set; }
    public string? Reference { get; set; } = null;
}