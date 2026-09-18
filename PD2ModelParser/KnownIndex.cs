using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PD2ModelParser
{
    public class KnownIndex
    {
        private readonly Dictionary<ulong, string> hashes = [];

        public string GetString(ulong hash)
        {
            if (hashes.TryGetValue(hash, out string value))
            {
                return value;
            }
            return Convert.ToString(hash);
        }

        public bool Contains(ulong hash)
        {
            return hashes.ContainsKey(hash);
        }

        private static void CheckCollision(Dictionary<ulong, string> item, ulong hash, string value)
        {
            if ( item.TryGetValue(hash, out string value1) && (value1 != value) )
            {
                Log.Default.Warn("Hash collision: {0:x} : {1} == {2}", hash, value1, value);
            }
        }

        private bool loaded = false;
        private bool reloadRequested = false;

        public void RequestReload()
        {
            reloadRequested = true;
        }

        public bool Load()
        {
            if (loaded && !reloadRequested)
                return true;

            hashes.Clear();

            loaded = false;
            reloadRequested = false;

            foreach (var name in GetHashfileNames())
            {
                loaded |= TryLoad(name);
            }

            return loaded;
        }

        public bool TryLoad(string filename)
        {
            try
            {
                using var sr = new StreamReader(filename);
                string line = sr.ReadLine();
                while (line != null)
                {
                    Hint(line);
                    line = sr.ReadLine();
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Default.Warn("Couldn't read hashlist file \"{0}\": {1}", filename, e.Message);
                return false;
            }
        }

        private static IEnumerable<string> GetHashfileNames()
        {
            var exepath = System.Reflection.Assembly.GetEntryAssembly().Location;
            var exedir = Path.GetDirectoryName(exepath);
            var cwd = Directory.GetCurrentDirectory();

            var hashregex = new Regex(@"hash(list|es)(-\d+)?(\.txt)?", RegexOptions.IgnoreCase);
            var names = Directory.GetFiles(cwd).Where(i=>hashregex.IsMatch(i));
            if(exedir != cwd)
            {
                names = names.Concat(Directory.GetFiles(exedir).Where(i => hashregex.IsMatch(i)));
            }
            return names;
        }

        public void Hint(string line)
        {
            ulong hash = Hash64.HashString(line);
            CheckCollision(hashes, hash, line);
            hashes[hash] = line;
        }
    }
}
