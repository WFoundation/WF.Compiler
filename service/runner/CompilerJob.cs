// Service adapter for WF.Compiler (LGPL-3.0-or-later, matching the compiler).
// Always run untrusted cartridges inside the isolated container, never the API host.
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using WF.Compiler;
using DeviceType = Groundspeak.Wherigo.ZonesLinker.DeviceType;

public static class CompilerJob
{
    static readonly string[] Allowed = {"Emulator", "Garmin", "iPad", "iPhone", "OpenWIG", "PPC2003", "WFPlayer", "WhereYouGo", "XMarksTheSpot"};
    public static int Main(string[] args)
    {
        if (args.Length != 4) return 2;
        string source = args[0], device = args[1], output = args[2], report = args[3];
        var messages = new List<string>();
        bool ok = false;
        try
        {
            if (Array.IndexOf(Allowed, device) < 0) throw new InvalidDataException("Unsupported device.");
            ValidateArchive(source);
            using (MemoryStream result = Compiler.Download(source, (DeviceType)Enum.Parse(typeof(DeviceType), device), "Test Compiler", ""))
            {
                if (result == null || result.Length < 10 || result.Length > 20000000)
                    throw new InvalidDataException("The compiled file is empty or larger than 20 MB.");
                byte[] bytes = result.ToArray();
                byte[] signature = {2, 10, 67, 65, 82, 84, 0};
                for (int i = 0; i < signature.Length; i++)
                    if (bytes[i] != signature[i]) throw new InvalidDataException("Invalid GWC header.");
                File.WriteAllBytes(output, bytes);
            }
            ok = true;
        }
        catch (CompilerLuaException error)
        {
            messages.Add(error.Message);
            if (error.CodeBefore != null) messages.Add("Line " + (error.Line - 1) + ": " + error.CodeBefore);
            if (error.Code != null) messages.Add("Line " + error.Line + ": " + error.Code);
            if (error.CodeAfter != null) messages.Add("Line " + (error.Line + 1) + ": " + error.CodeAfter);
        }
        catch (Exception error) { messages.Add(error.Message); }
        File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { device = device, ok = ok, messages = messages }));
        return ok ? 0 : 1;
    }

    static void ValidateArchive(string source)
    {
        if (new FileInfo(source).Length > 20000000) throw new InvalidDataException("The source package is larger than 20 MB.");
        using (var input = File.OpenRead(source))
        using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
        {
            if (zip.Entries.Count > 1000) throw new InvalidDataException("The package contains too many files.");
            long total = 0;
            int luaCount = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("/") || name.Contains(":") || Array.IndexOf(name.Split('/'), "..") >= 0 || !names.Add(name))
                    throw new InvalidDataException("The package contains an unsafe or duplicate filename.");
                total += entry.Length;
                if (total > 128000000 || entry.Length > 20000000)
                    throw new InvalidDataException("The expanded package exceeds the compiler's size limits.");
                if (name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) luaCount++;
            }
            if (luaCount != 1) throw new InvalidDataException("The package must contain exactly one Lua source file.");
        }
    }
}
