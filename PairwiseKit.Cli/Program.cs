using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PairwiseKit;
using PairwiseKit.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.Json;

namespace PairwiseKit.Cli
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help") { Help(); return; }
            var cmd = args[0];
            if (cmd.Equals("demo", StringComparison.OrdinalIgnoreCase)) { Demo(); return; }
            if (cmd.Equals("gen", StringComparison.OrdinalIgnoreCase)) { Gen(args.Skip(1).ToArray()); return; }
            Console.Error.WriteLine("Unknown command: " + cmd); Help();
        }

        static void Help()
        {
            Console.WriteLine("pairwise (C#):");
            Console.WriteLine("  gen -i <spec.yml> [-o <out.csv|out.json>] [--show]");
            Console.WriteLine("  demo");
        }

        static void Demo()
        {
            var spec = new Spec {
                Parameters = new() {
                    ["Browser"] = new(){"Chrome","Firefox","Safari"},
                    ["OS"] = new(){"Windows","macOS","Linux"},
                    ["Auth"] = new(){"SAML","Basic","OAuth"}
                },
                Forbid = new(){ new(){ ["Browser"]="Safari",["OS"]="Windows" }, new(){ ["Auth"]="SAML",["OS"]="Linux" } }
            };
            var rows = PairwiseGenerator.GeneratePairwise(spec.Parameters, spec.Forbid, spec.Require);
            Print(rows);
            Console.WriteLine($"Rows: {rows.Count}");
        }

        static void Gen(string[] args)
        {
            string? input = null, output = null; bool show = false;
            for (int i=0;i<args.Length;i++)
            {
                switch (args[i])
                {
                    case "-i": case "--input": input = (i+1<args.Length) ? args[++i] : null; break;
                    case "-o": case "--output": output = (i+1<args.Length) ? args[++i] : null; break;
                    case "--show": show = true; break;
                }
            }
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            { Console.Error.WriteLine("Use -i <spec.yml>"); return; }

            var spec = LoadSpec(input);
            var rows = PairwiseGenerator.GeneratePairwise(spec.Parameters, spec.Forbid, spec.Require);

            if (!string.IsNullOrWhiteSpace(output))
            {
                if (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions{WriteIndented=true}));
                else SaveCsv(rows, output);
                Console.WriteLine($"Saved {rows.Count} rows -> {output}");
            }
            if (show || string.IsNullOrWhiteSpace(output)) Print(rows);
        }

        static Spec LoadSpec(string path)
        {
            var txt = File.ReadAllText(path);
            var deser = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
            return deser.Deserialize<Spec>(txt) ?? new Spec();
        }

        static void SaveCsv(List<Dictionary<string,string>> rows, string path)
        {
            if (rows.Count==0) { File.WriteAllText(path,""); return; }
            var headers = rows[0].Keys.ToList();
            using var sw = new StreamWriter(path);
            sw.WriteLine(string.Join(",", headers));
            foreach (var r in rows)
                sw.WriteLine(string.Join(",", headers.Select(h => CsvEscape(r[h]))));
        }
        static string CsvEscape(string s) => (s.Contains('"')||s.Contains(',')||s.Contains('\\')) ? $"\"{s.Replace("\"","\"")}\"" : s;

        static void Print(List<Dictionary<string,string>> rows)
        {
            if (rows.Count==0) { Console.WriteLine("(no rows)"); return; }
            var headers = rows[0].Keys.ToList();
            var w = headers.Select(h=>h.Length).ToArray();
            foreach (var r in rows)
                for (int i=0;i<headers.Count;i++) w[i]=Math.Max(w[i], r[headers[i]].Length);
            for (int i=0;i<headers.Count;i++) Console.Write("| "+headers[i].PadRight(w[i])+" "); Console.WriteLine("|");
            Console.WriteLine("|"+string.Join("|", w.Select(n=>" "+new string('-',n)+" " ))+"|");
            foreach (var r in rows){ for (int i=0;i<headers.Count;i++) Console.Write("| "+r[headers[i]].PadRight(w[i])+" "); Console.WriteLine("|");}
        }
    }
}