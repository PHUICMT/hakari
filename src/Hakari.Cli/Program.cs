using System.Diagnostics;
using Hakari.Core;

var root = args.Length > 0 ? args[0] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
var pricing = PricingTable.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pricing.json")));

var sw = Stopwatch.StartNew();
var seen = new HashSet<string>();
var byModel = new SortedDictionary<string, (long msgs, long input, long output, long w5m, long w1h, long read, decimal cost)>();
var unpriced = new HashSet<string>();

foreach (var file in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
{
    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
    using var reader = new StreamReader(stream);
    while (reader.ReadLine() is { } line)
    {
        var r = UsageLineParser.TryParse(System.Text.Encoding.UTF8.GetBytes(line));
        if (r is null || !seen.Add(r.DedupeKey)) continue;
        var cost = pricing.Cost(r);
        if (cost is null) unpriced.Add(r.Model);
        var t = byModel.GetValueOrDefault(r.Model);
        byModel[r.Model] = (t.msgs + 1, t.input + r.InputTokens, t.output + r.OutputTokens,
            t.w5m + r.CacheWrite5mTokens, t.w1h + r.CacheWrite1hTokens, t.read + r.CacheReadTokens, t.cost + (cost ?? 0));
    }
}

Console.WriteLine($"{"model",-22} {"msgs",8} {"input",10} {"output",12} {"write5m",14} {"write1h",14} {"read",16} {"cost $",10}");
foreach (var (model, t) in byModel)
    Console.WriteLine($"{model,-22} {t.msgs,8:N0} {t.input,10:N0} {t.output,12:N0} {t.w5m,14:N0} {t.w1h,14:N0} {t.read,16:N0} {t.cost,10:N2}");
Console.WriteLine($"TOTAL ${byModel.Values.Sum(v => v.cost):N2} · {seen.Count:N0} unique messages · {sw.Elapsed.TotalSeconds:N1}s");
if (unpriced.Count > 0) Console.WriteLine($"unpriced: {string.Join(", ", unpriced)}");
