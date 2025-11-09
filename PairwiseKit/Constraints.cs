using System.Collections.Generic;

namespace PairwiseKit
{
    public static class Constraints
    {
        public static bool ViolatesForbid(Dictionary<string,string> a, List<Dictionary<string,string>> forbids)
        {
            if (forbids == null || forbids.Count == 0) return false;
            foreach (var f in forbids)
            {
                bool ok = true;
                foreach (var kv in f)
                    if (!a.TryGetValue(kv.Key, out var v) || v != kv.Value) { ok = false; break; }
                if (ok) return true;
            }
            return false;
        }

        public static bool SatisfiesRequire(Dictionary<string,string> a, List<Dictionary<string,string>> requires)
        {
            if (requires == null || requires.Count == 0) return true;
            foreach (var r in requires)
            {
                bool ok = true;
                foreach (var kv in r)
                    if (!a.TryGetValue(kv.Key, out var v) || v != kv.Value) { ok = false; break; }
                if (ok) return true;
            }
            return false;
        }
    }
}