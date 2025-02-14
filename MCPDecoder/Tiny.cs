using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPDecoder
{
    internal static class Tiny
    {
        internal static async Task<List<TinyClass>> ProcessAsync(string path)
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
                    string[] header = null;
                    string line;

                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if (header == null)
                        {
                            header = line.Split('\t');
                            continue;
                        }
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
    }

    internal interface ITinyHasOfficialMapping
    {
        string Official { get; set; }
        string Intermediary { get; set; }
        string Named { get; set; }
    }

    internal interface ITinyHasComment
    {
        string Comment { get; set; }
    }

    internal class TinyClass : ITinyHasOfficialMapping, ITinyHasComment
    {
        public string Official { get; set; }
        public string Intermediary { get; set; }
        public string Named { get; set; }
        public string Comment { get; set; }
        public List<TinyField> Fields { get; set; }
        public List<TinyMethod> Methods { get; set; }
    }

    internal class TinyField : ITinyHasOfficialMapping, ITinyHasComment
    {
        public string Descriptor { get; set; }
        public string Official { get; set; }
        public string Intermediary { get; set; }
        public string Named { get; set; }
        public string Comment { get; set; }
    }

    internal class TinyMethod : ITinyHasOfficialMapping, ITinyHasComment
    {
        public string Descriptor { get; set; }
        public string Official { get; set; }
        public string Intermediary { get; set; }
        public string Named { get; set; }
        public string Comment { get; set; }
        public List<TinyParameter> Parameters { get; set; }
    }

    internal class TinyParameter : ITinyHasComment
    {
        public int Index { get; set; }
        public string Named { get; set; }
        public string Comment { get; set; }
    }
}
