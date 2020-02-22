using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using System.Windows;

namespace MCPDecoder
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string fieldsPath = AppDomain.CurrentDomain.BaseDirectory + "fields.csv";
            string methodsPath = AppDomain.CurrentDomain.BaseDirectory + "methods.csv";

            if (!File.Exists(fieldsPath))
            {
                Console.WriteLine("Missing fields.csv");
                return;
            }

            if (!File.Exists(methodsPath))
            {
                Console.WriteLine("Missing methods.csv");
                return;
            }

            Dictionary<string, Dictionary<string, string>> fields = null;
            Dictionary<string, Dictionary<string, string>> methods = null;
            try
            {
                using (StreamReader stream = File.OpenText(fieldsPath))
                {
                    fields = ProcessCSV(stream);
                }

                using (StreamReader stream = File.OpenText(methodsPath))
                {
                    methods = ProcessCSV(stream);
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Missing fields.csv or methods.csv");
                Console.WriteLine(e.ToString());
                return;
            }

            if(fields == null || methods == null)
            {
                Console.WriteLine("Unable to process fields.csv and/or methods.csv");
                return;
            }

            if(args.Length == 0)
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    text = ReplaceFieldsAndMethods(text, fields, methods);
                    Clipboard.SetText(text);
                    Console.WriteLine("Processed clipboard");
                }
                else
                {
                    Console.WriteLine("Have text in clipboard OR argument 'print' OR files");
                }
                return;
            }
            
            if(args.Length == 1 && args[0] == "print")
            {
                using (StreamWriter stream = File.CreateText("fields.csv.log"))
                {
                    Print(stream, fields);
                }

                Console.WriteLine("Written fields.csv.log");

                using (StreamWriter stream = File.CreateText("methods.csv.log"))
                {
                    Print(stream, methods);
                }

                Console.WriteLine("Written methods.csv.log");
                return;
            }

            bool replace = false;

            foreach (string name in args)
            {
                if(name == "r")
                {
                    replace = !replace;
                    continue;
                }
                ProcessPath(name, fields, methods, replace);
            }
        }

        private static void ProcessPath(string name, Dictionary<string, Dictionary<string, string>> fields, Dictionary<string, Dictionary<string, string>> methods, bool replace = false)
        {
            if(Directory.Exists(name))
            {
                Console.Out.WriteLine("Recursing into {0}", name);
                //Recursively call for all files and directories inside
                foreach(string sub in Directory.EnumerateFileSystemEntries(name))
                {
                    ProcessPath(sub, fields, methods, replace);
                }
                return;
            }

            if (File.Exists(name) && name.EndsWith(".java"))
            {
                Console.Out.WriteLine("Processing {0}", name);
                string document = File.ReadAllText(name);

                document = ReplaceFieldsAndMethods(document, fields, methods);

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

            //Console.Error.WriteLine("Not a java source file or directory {0}", name);
            Console.Error.WriteLine("Skipping {0}", name);
        }

        static Regex fieldMatch = new Regex(@"(?<!"")field_\d+?_\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static Regex methodMatch = new Regex(@"(?<!"")func_\d+?_\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static string ReplaceFieldsAndMethods(string input, Dictionary<string, Dictionary<string, string>> fields, Dictionary<string, Dictionary<string, string>> methods)
        {
            input = fieldMatch.Replace(input, SpecialMatchEvaluator(fields));
            input = methodMatch.Replace(input, SpecialMatchEvaluator(methods));

            return input;
        }


        static MatchEvaluator SpecialMatchEvaluator(Dictionary<string, Dictionary<string, string>> dictionary)
        {
            return (match) => dictionary.ContainsKey(match.Value) ? dictionary[match.Value]["name"] : match.Value;
        }

        static void Print(TextWriter stream, object o, int depth = 0)
        {
            if(o is IDictionary)
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

            //Is it better to do this or just write it? idk
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

        static Dictionary<string, Dictionary<string, string>> ProcessCSV(StreamReader stream)
        {
            Dictionary<string, Dictionary<string, string>> output = new Dictionary<string, Dictionary<string, string>>();
            string[] columns = null;
            string line;
            while ((line = stream.ReadLine()) != null)
            {
                string[] processed = ProcessLine(line);
                if (processed.Length == 0) continue; //Ignores empty lines
                if (columns == null)
                {
                    columns = processed;
                    continue;
                }
                if (processed.Length != columns.Length)
                {
                    Console.Error.WriteLine("Line Lengths do not match");
                    continue; //Ignore
                }
                string key = processed[0];
                Dictionary<string, string> values = new Dictionary<string, string>();

                for(int i = 1; i < columns.Length; i++)
                {
                    values.Add(columns[i], processed[i]);
                }
                while(output.ContainsKey(key)) key = key + "_";
                output.Add(key, values);
            }

            return output;
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

    }

}
