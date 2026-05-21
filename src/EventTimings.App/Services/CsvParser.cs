using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventTimings.Contracts;

namespace EventTimings.App.Services;

public static class CsvParser
{
    public static (List<RiderImportDto> Riders, List<RiderContactImportDto> Contacts, List<string> Errors) ParseImportContent(string content)
    {
        var riders = new List<RiderImportDto>();
        var contacts = new List<RiderContactImportDto>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return (riders, contacts, errors);
        }

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (lines.Count == 0) return (riders, contacts, errors);

        // detect header tokens from first non-empty line
        var firstTokens = SplitLine(lines[0].Trim());
        var headerJoined = string.Join(" ", firstTokens).ToLowerInvariant();

        var looksLikeContacts = headerJoined.Contains("email") || headerJoined.Contains("phone") || headerJoined.Contains("phonenumber");
        var looksLikeRiders = headerJoined.Contains("bib") || (headerJoined.Contains("name") && headerJoined.Contains("category"));

        if (looksLikeContacts && !looksLikeRiders)
        {
            // parse as contacts
            var map = BuildContactHeaderMap(firstTokens);
            // if a contact-style header is present, start from the next line
            var startIdx = (LooksLikeHeader(firstTokens) || looksLikeContacts) ? 1 : 0;
            var lineNo = 1 + startIdx;
            for (int i = startIdx; i < lines.Count; i++)
            {
                var tokens = SplitLine(lines[i]);
                var email = tokens.ElementAtOrDefault(map.email)?.Trim() ?? string.Empty;
                var name = tokens.ElementAtOrDefault(map.name)?.Trim() ?? string.Empty;
                var phone = tokens.ElementAtOrDefault(map.phone)?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
                {
                    errors.Add($"Line {lineNo}: Missing required fields (email, name).");
                    lineNo++;
                    continue;
                }

                contacts.Add(new RiderContactImportDto(name, string.IsNullOrWhiteSpace(email) ? null : email, string.IsNullOrWhiteSpace(phone) ? null : phone));
                lineNo++;
            }

            return (riders, contacts, errors);
        }

        // fallback to rider import parsing
        var (dtos, errs) = ParseRiderImportDtos(content);
        return (dtos, new List<RiderContactImportDto>(), errs);
    }

    private static (int email, int name, int phone) BuildContactHeaderMap(IReadOnlyList<string> tokens)
    {
        int email = -1, name = -1, phone = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i].Trim().ToLowerInvariant();
            if (t.Contains("email") && email == -1) email = i;
            else if (t.Contains("name") && name == -1) name = i;
            else if ((t.Contains("phone") || t.Contains("phonenumber")) && phone == -1) phone = i;
        }

        if (email == -1) email = 0;
        if (name == -1) name = Math.Min(1, tokens.Count - 1);
        if (phone == -1) phone = Math.Min(2, tokens.Count - 1);

        return (email, name, phone);
    }
    public static (List<RiderImportDto> Dtos, List<string> Errors) ParseRiderImportDtos(string content)
    {
        var dtos = new List<RiderImportDto>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return (dtos, errors);
        }

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (lines.Count == 0) return (dtos, errors);

        // Detect header
        bool hasHeader = false;
        int bibIndex = 0, nameIndex = 1, categoryIndex = 2, assignedRouteIndex = 3;

        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(l)) continue;
            var tokens = SplitLine(l);
            if (LooksLikeHeader(tokens))
            {
                hasHeader = true;
                var map = BuildHeaderMap(tokens);
                bibIndex = map.bib;
                nameIndex = map.name;
                categoryIndex = map.category;
                assignedRouteIndex = map.assignedRoute;
                // remove header and parse remaining
                lines = lines.Skip(i + 1).ToList();
            }
            break;
        }

        var lineNo = hasHeader ? 2 : 1;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                lineNo++;
                continue;
            }

            var tokens = SplitLine(line);

            string bib = tokens.ElementAtOrDefault(bibIndex)?.Trim() ?? string.Empty;
            string name = tokens.ElementAtOrDefault(nameIndex)?.Trim() ?? string.Empty;
            string category = tokens.ElementAtOrDefault(categoryIndex)?.Trim() ?? string.Empty;
            string assignedRoute = tokens.ElementAtOrDefault(assignedRouteIndex)?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(bib) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
            {
                errors.Add($"Line {lineNo}: Missing required fields (bib, name, category).");
                lineNo++;
                continue;
            }

            dtos.Add(new RiderImportDto(null, bib, name, category, string.IsNullOrWhiteSpace(assignedRoute) ? null : assignedRoute));
            lineNo++;
        }

        return (dtos, errors);
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> tokens)
    {
        if (tokens == null || tokens.Count == 0) return false;
        var joined = string.Join(" ", tokens).ToLowerInvariant();
        // crude header detection: contains both name and category or bib
        return joined.Contains("bib") || (joined.Contains("name") && joined.Contains("category"));
    }

    private static (int bib, int name, int category, int assignedRoute) BuildHeaderMap(IReadOnlyList<string> tokens)
    {
        int bib = -1, name = -1, category = -1, assignedRoute = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i].Trim().ToLowerInvariant();
            if (t.Contains("bib")) bib = i;
            else if (t.Contains("name") && name == -1) name = i;
            else if (t.Contains("category")) category = i;
            else if ((t.Contains("route") || t.Contains("assigned")) && assignedRoute == -1) assignedRoute = i;
        }

        if (bib == -1) bib = 0;
        if (name == -1) name = Math.Min(1, tokens.Count - 1);
        if (category == -1) category = Math.Min(2, tokens.Count - 1);
        if (assignedRoute == -1) assignedRoute = tokens.Count; // out of range => treated as missing

        return (bib, name, category, assignedRoute);
    }

    private static List<string> SplitLine(string line)
    {
        var result = new List<string>();
        if (line is null) return result;
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (!inQuotes && (c == ',' || c == '\t'))
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
