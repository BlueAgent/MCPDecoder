using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace MCPDecoder
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string[] fileNames =
            {
                "fields.csv",
                "methods.csv",
                "mappings.tiny"
            };

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var filePaths = fileNames.Select(fileName => Path.Combine(baseDirectory, fileName));

            if (args.Length == 1 && args[0] == "print")
            {
                foreach (var path in filePaths)
                {
                    var debugPath = path + ".log";
                    using (StreamWriter stream = File.CreateText(debugPath))
                    {
                        if (path.EndsWith(".csv"))
                        {
                            Print(stream, ProcessCSVAsync(path).Result);
                        }
                        else if (path.EndsWith(".tiny"))
                        {
                            Print(stream, Tiny.ProcessAsync(path).Result);
                        }
                    }
                    Console.WriteLine($"Written {Path.GetFileName(debugPath)}");
                }

                return;
            }

            var replacements = filePaths
                .Select(ProcessCSVAsync)
                .SelectMany(t => t?.Result?.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, Dictionary<string, string>>>())
                .ToDictionary(kv => kv.Key, kv => kv.Value["name"]);
            var keyRegex = BuildRegex(replacements.Keys);

            var replacementData = new ReplacerCache()
            {
                Replacements = replacements,
                Regex = keyRegex,
            };

            if (args.Length == 0)
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    text = ReplaceFieldsAndMethods(text, replacementData);
                    Clipboard.SetText(text);
                    Console.WriteLine("Processed clipboard");
                }
                else
                {
                    Console.WriteLine("Have text in clipboard OR argument 'print' OR files");
                }
                return;
            }

            bool replace = false;
            var tasks = new List<Task>();

            foreach (string name in args)
            {
                if (name == "r")
                {
                    replace = !replace;
                    continue;
                }
                tasks.Add(ProcessPathAsync(name, replacementData, replace));
            }
            Task.WhenAll(tasks).Wait();
        }

        private static async Task ProcessPathAsync(string name, ReplacerCache replacementData, bool replace = false)
        {
            if (Directory.Exists(name))
            {
                Console.Out.WriteLine("Recursing into {0}", name);
                // Recursively call for all files and directories inside
                await Task.WhenAll(Directory.EnumerateFileSystemEntries(name)
                    .Select(sub => ProcessPathAsync(sub, replacementData, replace)));
                return;
            }

            if (File.Exists(name) && name.EndsWith(".java"))
            {
                Console.Out.WriteLine("Processing {0}", name);
                string document;
                using (var streamReader = File.OpenText(name))
                {
                    document = await streamReader.ReadToEndAsync();
                }

                document = ReplaceFieldsAndMethods(document, replacementData);

                FileInfo target = new FileInfo(name);
                while (target.Exists)
                {
                    string path = target.FullName;
                    if (!replace)
                    {
                        path = Path.ChangeExtension(path, '.' + Path.GetExtension(path));
                    }
                    else
                    {
                        File.Delete(path);
                    }

                    target = new FileInfo(path);
                }
                using (StreamWriter stream = target.CreateText())
                {
                    stream.Write(document);
                }
                return;
            }

            Console.Error.WriteLine("Skipping {0}", name);
        }

        static Regex BuildRegex(IEnumerable<string> keys)
        {
            var trie = new Trie();
            foreach (var key in keys)
            {
                trie.Add(key);
            }
            //Console.WriteLine(trie);

            StringBuilder sb = new StringBuilder();
            sb.Append(@"(?<!"")");
            sb.Append(trie.ToRegex());
            return new Regex(sb.ToString(), RegexOptions.Compiled);
        }

        static string ReplaceFieldsAndMethods(string input, ReplacerCache replacementData)
        {
            var replacements = replacementData.Replacements;
            return replacementData.Regex.Replace(
                input,
                (match) => replacements.TryGetValue(match.Value, out string replacement) ? replacement : match.Value
            );
        }

        static void Print(TextWriter stream, object o, int depth = 0)
        {
            if (o is IDictionary)
            {
                IDictionary dictionary = (IDictionary)o;
                foreach (DictionaryEntry pair in dictionary)
                {
                    //Console.WriteLine(pair.GetType().ToString());
                    Print(stream, pair.Key, depth + 1);
                    //Console.WriteLine();
                    Print(stream, pair.Value, depth + 1);
                    //Console.WriteLine();
                }
                return;
            }

            // Is it better to do this or just write it? idk
            StringBuilder build = new StringBuilder();
            for (int i = 0; i < depth; i++)
            {
                build.Append('\t');
            }

            string indent = build.ToString();
            build.Clear();

            stream.Write(indent);

            stream.WriteLine(o);
        }

        static async Task<Dictionary<string, Dictionary<string, string>>> ProcessCSVAsync(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Missing {Path.GetFileName(path)}");
                return null;
            }

            try
            {
                using (StreamReader stream = File.OpenText(path))
                {
                    Dictionary<string, Dictionary<string, string>> output = new Dictionary<string, Dictionary<string, string>>();
                    string[] headers = null;
                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        string[] processed = ProcessLine(line);
                        if (processed.Length == 0) continue; // Ignores empty or broken lines
                        if (headers == null) // Use first row as column headers
                        {
                            headers = processed;
                            continue;
                        }
                        if (processed.Length != headers.Length)
                        {
                            Console.Error.WriteLine($"Line length ({processed.Length}) does not match header length ({headers.Length}): {string.Join(", ", processed)}");
                            continue; // Ignore
                        }
                        string key = processed[0]; // Use the first column as the lookup
                        Dictionary<string, string> valuesByHeader = new Dictionary<string, string>();

                        for (int i = 1; i < headers.Length; i++)
                        {
                            valuesByHeader.Add(headers[i], processed[i]);
                        }
                        if (output.ContainsKey(key))
                        {
                            //if (output[key].Keys != valuesByHeader.Keys && output[key].Any(kv => valuesByHeader[kv.Key] != kv.Value))
                            if (output[key].Keys != valuesByHeader.Keys && output[key].Any(kv => valuesByHeader[kv.Key] != kv.Value))
                            {
                                Console.Error.WriteLine($"Skipping duplicate key '{key}'.");
                                Console.Error.WriteLine($"Existing: [{string.Join(", ", output[key].Values)}].");
                                Console.Error.WriteLine($"Duplicate: [{string.Join(", ", valuesByHeader.Values)}].");
                            }
                            continue;
                        }

                        output.Add(key, valuesByHeader);
                    }

                    return output;
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"Missing {Path.GetFileName(path)}");
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Splits line into tokens, trims whitespace
        /// Does not escape, taken literally.
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        static string[] ProcessLine(string line)
        {
            LinkedList<string> tokens = new LinkedList<string>();
            if (line.Length == 0) return new string[0];

            bool quote = false;
            bool escaped = false;
            StringBuilder build = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (escaped)
                {
                    //Does not actually escape, but treats literally
                    //Potentially add a switch here for proper escaping
                    build.Append(c);
                    escaped = false;
                }
                else if (quote)
                {
                    //if(build.Length != 0)
                    //{
                    //    Console.Error.WriteLine("Unexpected opening quotes");
                    //    return new string[0];
                    //}
                    switch (c)
                    {
                        case '"':
                            quote = false;
                            break;
                        case '\\': //Escaped next character
                            escaped = true;
                            break;
                        default:
                            build.Append(c);
                            break;
                    }
                    continue;
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            quote = true;
                            break;
                        case ',':
                            tokens.AddLast(build.ToString());
                            build.Clear();
                            break;
                        default:
                            build.Append(c);
                            break;
                    }
                    continue;
                }
            }

            if (quote)
            {
                Console.Error.WriteLine("Incorrect Format: Closing quotes missing");
                return new string[0];
            }

            tokens.AddLast(build.ToString());

            return tokens.ToArray<string>();
        }

        private sealed class ReplacerCache
        {
            public Dictionary<string, string> Replacements { get; set; }
            public Regex Regex { get; set; }
        }
    }
}
