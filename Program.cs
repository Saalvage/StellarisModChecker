using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

const string LOCAL_HASH_FILE = "my_mods.csv";

string[] MOTDS = [
    "\"But- I- just- got- promoted\" - Unknown Battle Droid Commander",
    "\"It's not over until the fat Wookie sings\" - Unknown Clone Trooper",
    "\"Just like the simulations\" - Unknown Clone Trooper",
    "\"I still can't seem to hit anything\" - Unknown Battle Droid",
    "\"Uh, do we take prisoners?\" - Unknown Battle Droid",
    "\"Roger, roger\" - Unknown Battle Droid",
    "\"Buildings are just buildings\" - CC-2224 \"Cody\"",
    "\"And where there’s a farm, there’s usually a farmer\" - CT-5597 \"Jesse\"",
    "\"What the hell was that?!\" - CT-782 \"Hevy\"",
    "\"Experience outranks everything\" - CT-7567 \"Rex\"",
    "\"Good soldiers follow orders\" - CT-5385 \"Tup\"",
    "\"Ooh, meteor shower!\" - CT-782 \"Hevy\"",
    "\"Well you know what I say. Speak softly...and drive a big tank!\" - Hondo Ohnaka",
    "\"I don’t accept unnecessary losses of any kind\" - Grand Admiral Thrawn",
    "\"A surprise to be sure, but a welcome one\" - Chancellor Palpatine",
    "\"Kill him, kill him now\" - Chancellor Palpatine",
    "\"Dew it\" - Chancellor Palpatine",
    "\"I am the Senate\" - Elected Chancellor Palpatine",
    "\"I love democracy\" - Totally Democratic Chancellor Palpatine",
    "\"Unlimited power!\" - Darth Sidious",
    "\"Good, GOOOOD!\" - Emperor Palpatine",
    "\"This is where the fun begins\" - Anakin Skywalker",
    "\"Hello there!\" - Obi Wan Kenobi",
    "\"Sorry about the mess\" - Han Solo",
    "\"Massive amounts of Ketamine, I have consumed\" - Master Yoda",
    "\"Yousa in big doo-doo this time\" - Roos Tarpals",
];

Dictionary<long, string> mods;
if (!File.Exists(LOCAL_HASH_FILE)) {
    Console.WriteLine("First time setup, computing hash file..");
    try {
        mods = await GenerateHashFile();
    } catch (Exception e) {
        Console.WriteLine($"Failed to generate hash file: {e.Message}");
        return;
    }
} else {
    Console.WriteLine("Parsing hash file..");
    mods = await ParseHashFile(LOCAL_HASH_FILE);
}

Console.WriteLine($"""
                  
                  Initialization successful!
                  >> Welcome to the Stellaris Mod Checker <<
                  MOTD: {MOTDS[Random.Shared.Next(MOTDS.Length)]}
                  
                  """);

while (true) {
    Console.WriteLine("""
                      
                      What would you like to do?
                      [1] Recompute hashes
                      [2] Compare hashes with given hash file
                      [3] Find your own hash file
                      """);

    var input = Console.ReadLine();
    switch (input) {
        case "1":
            Console.WriteLine("Regenerating mod list..");
            try {
                mods = await GenerateHashFile();
            } catch (Exception e) {
                Console.WriteLine($"Failed to generate hash file: {e.Message}");
            }
            break;
        case "2":
            Console.WriteLine("Please enter the path of the hash file to compare to: ");
            var file = Console.ReadLine();
            if (!File.Exists(file)) {
                Console.WriteLine("That file does not exist!");
                break;
            }
            var otherMods = await ParseHashFile(file);
            var brokenMods = new List<long>();
            var currColor = Console.ForegroundColor;
            foreach (var (modId, hash) in otherMods) {
                if (mods.TryGetValue(modId, out var myHash) && myHash != hash) {
                    brokenMods.Add(modId);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Broken mod found: {modId}");
                } else {
                    Console.WriteLine($"Mod is fine: {modId}");
                }
                Console.ForegroundColor = currColor;
            }
            Console.WriteLine();
            Console.WriteLine(brokenMods.Count == 0 ? "All mods seem to be correct!" : $"{brokenMods.Count} broken mods found:");
            foreach (var id in brokenMods) {
                Console.WriteLine(id);
            }
            break;
        case "3":
            Process.Start("explorer.exe", "/select, \"" + Path.Combine(Directory.GetCurrentDirectory(), LOCAL_HASH_FILE) + "\"");
            break;
        default:
            Console.WriteLine($"Sorry, I did not understand \"{input}\"");
            break;
    }
}

static async Task<Dictionary<long, string>> ParseHashFile(string file) {
    var dic = new Dictionary<long, string>();
    await foreach (var line in File.ReadLinesAsync(file)) {
        var split = line.Split(':');
        if (split.Length != 2 && line != "" || !long.TryParse(split[0], out var modId)) {
            Console.WriteLine($"Encountered invalid line: \"{line}\"");
            continue;
        }

        if (!dic.TryAdd(modId, split[1])) {
            Console.WriteLine($"Duplicate hash for {modId}?");
        }
    }
    return dic;
}

async Task<Dictionary<long, string>> GenerateHashFile() {
    var installLoc = (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null)
                      ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null)) as string;

    if (installLoc == null) {
        throw new("Failed to get Steam install location!");
    }

    await using var stream = File.OpenRead(Path.Join(installLoc, "steamapps/libraryfolders.vdf"));
    using var reader = new StreamReader(stream);
    var location = VdfConvert.Deserialize(reader).Value.OfType<VProperty>()
        .Select(x => x.Value)
        .First(x => x["apps"].OfType<VProperty>().Any(x => x.Key == "281990"))["path"]?.ToString();
    if (location == null) {
        throw new("Failed to find Stellaris install location!");
    }

    location = Path.Combine(location, "steamapps/workshop/content/281990");

    await using var fs = File.Create(LOCAL_HASH_FILE);
    await using var writer = new StreamWriter(fs);

    var dic = new Dictionary<long, string>();
    var modList = Directory.GetDirectories(location);
    foreach (var (i, mod, hashTask) in modList
                 .Select((x, i) => (i, Path.GetRelativePath(location, x), HashDirectory(x)))) {
        if (!long.TryParse(mod, out var modId)) {
            Console.WriteLine($"Encountered non-mod folder {mod}, ignoring");
            continue;
        }
        var hash = await hashTask;
        Console.WriteLine($"Computed hash for {mod} ({(double)(i + 1) / modList.Length * 100:F}%)");
        await writer.WriteLineAsync($"{mod}:{hash}");
        dic.Add(modId, hash);
    }
    return dic;
}

async Task<string> HashDirectory(string directory) {
    using var hash = MD5.Create();
    await using var cs = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order()) {
        var cool = Path.GetRelativePath(directory, file).Replace('\\', '/');
        await cs.WriteAsync(Encoding.UTF8.GetBytes(cool));

        await using var fs = File.OpenRead(file);
        await fs.CopyToAsync(cs);
    }
    await cs.FlushFinalBlockAsync();
    return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
}
