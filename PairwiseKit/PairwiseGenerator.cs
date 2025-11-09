using System;
using System.Collections.Generic;
using System.Linq;

namespace PairwiseKit
{
    public static class PairwiseGenerator
    {
        static HashSet<((string,string),(string,string))> AllPairsToCover(Dictionary<string,List<string>> p)
        {
            var keys = p.Keys.ToList();
            var set = new HashSet<((string,string),(string,string))>();
            for (int i=0;i<keys.Count;i++)
                for (int j=i+1;j<keys.Count;j++)
                {
                    var ki = keys[i]; var kj = keys[j];
                    foreach (var vi in p[ki])
                        foreach (var vj in p[kj])
                        {
                            var a = ((ki,vi),(kj,vj));
                            var ord = string.Compare(a.Item1.Item1, a.Item2.Item1) <= 0 ? a : (a.Item2, a.Item1);
                            set.Add(ord);
                        }
                }
            return set;
        }

        static HashSet<((string,string),(string,string))> PairsFrom(Dictionary<string,string> row)
        {
            var items = row.OrderBy(kv=>kv.Key).ToList();
            var set = new HashSet<((string,string),(string,string))>();
            for (int i=0;i<items.Count;i++)
                for (int j=i+1;j<items.Count;j++)
                    set.Add(((items[i].Key,items[i].Value),(items[j].Key,items[j].Value)));
            return set;
        }

        public static List<Dictionary<string,string>> GeneratePairwise(
            Dictionary<string,List<string>> parameters,
            List<Dictionary<string,string>>? forbid = null,
            List<Dictionary<string,string>>? require = null)
        {
            forbid ??= new(); require ??= new();
            var keys = parameters.Keys.ToList();
            var toCover = AllPairsToCover(parameters);
            var covered = new HashSet<((string,string),(string,string))>();
            var rows = new List<Dictionary<string,string>>();

            Dictionary<string,string> GreedySeed() => keys.ToDictionary(k => k, k => parameters[k].First());

            Dictionary<string,string> Improve(Dictionary<string,string> seed)
            {
                var best = new Dictionary<string,string>(seed);
                int bestGain = -1; bool improved = true;
                while (improved)
                {
                    improved = false;
                    foreach (var k in keys)
                        foreach (var v in parameters[k])
                        {
                            var trial = new Dictionary<string,string>(best);
                            trial[k] = v;
                            if (Constraints.ViolatesForbid(trial, forbid)) continue;
                            var gain = PairsFrom(trial).Except(covered).Count();
                            if (gain > bestGain) { best = trial; bestGain = gain; improved = true; }
                        }
                }
                return best;
            }

            int attempts = 0, maxAttempts = Math.Max(100, 5 * toCover.Count);
            while (!covered.SetEquals(toCover) && attempts++ < maxAttempts)
            {
                var cand = Improve(GreedySeed());

                if (Constraints.ViolatesForbid(cand, forbid))
                {
                    bool changed = false;
                    foreach (var k in keys)
                    {
                        foreach (var v in parameters[k])
                        {
                            var t = new Dictionary<string,string>(cand); t[k] = v;
                            if (!Constraints.ViolatesForbid(t, forbid)) { cand = t; changed = true; break; }
                        }
                        if (changed) break;
                    }
                    if (Constraints.ViolatesForbid(cand, forbid)) continue;
                }

                if (require.Count > 0 && !Constraints.SatisfiesRequire(cand, require))
                    foreach (var r in require)
                    {
                        var t = new Dictionary<string,string>(cand);
                        foreach (var kv in r) t[kv.Key] = kv.Value;
                        if (!Constraints.ViolatesForbid(t, forbid)) { cand = t; break; }
                    }

                var newPairs = PairsFrom(cand).Except(covered).ToList();
                if (newPairs.Count == 0) continue;

                rows.Add(cand);
                foreach (var p in PairsFrom(cand)) covered.Add(p);
            }

            if (!covered.SetEquals(toCover)) // добивка картежем (на малых доменах)
            {
                IEnumerable<Dictionary<string,string>> Cartesian()
                {
                    IEnumerable<Dictionary<string,string>> seed = new [] { new Dictionary<string,string>() };
                    foreach (var k in keys)
                        seed = from s in seed from v in parameters[k]
                               select new Dictionary<string,string>(s) { [k] = v };
                    return seed;
                }

                foreach (var cand in Cartesian())
                {
                    if (Constraints.ViolatesForbid(cand, forbid)) continue;
                    if (PairsFrom(cand).Except(covered).Any())
                    {
                        rows.Add(cand);
                        foreach (var p in PairsFrom(cand)) covered.Add(p);
                    }
                    if (covered.SetEquals(toCover)) break;
                }
            }

            var seen = new HashSet<string>();
            var order = keys;
            var uniq = new List<Dictionary<string,string>>();
            foreach (var r in rows)
            {
                var key = string.Join("|", order.Select(k => k + "=" + r[k]));
                if (seen.Add(key)) uniq.Add(order.ToDictionary(k => k, k => r[k]));
            }
            return uniq;
        }
    }
}