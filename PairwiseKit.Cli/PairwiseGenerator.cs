using System;
using System.Collections.Generic;
using System.Linq;

namespace PairwiseKit
{
    public static class PairwiseGenerator
    {
        // --- построение множества всех пар для покрытия (t=2) ---
        private static HashSet<((string, string), (string, string))> AllPairsToCover(
            Dictionary<string, List<string>> parameters)
        {
            var keys = parameters.Keys.ToList();
            var pairs = new HashSet<((string, string), (string, string))>();
            for (int i = 0; i < keys.Count; i++)
            {
                for (int j = i + 1; j < keys.Count; j++)
                {
                    var ki = keys[i]; var kj = keys[j];
                    foreach (var vi in parameters[ki])
                    foreach (var vj in parameters[kj])
                    {
                        var a = ((ki, vi), (kj, vj));
                        // нормализуем порядок по имени параметра
                        var ordered = (string.Compare(a.Item1.Item1, a.Item2.Item1, StringComparison.Ordinal) <= 0)
                            ? a : (a.Item2, a.Item1);
                        pairs.Add(ordered);
                    }
                }
            }
            return pairs;
        }

        // пары из одной строки-комбинации
        private static HashSet<((string, string), (string, string))> PairsFrom(
            Dictionary<string, string> assign)
        {
            var items = assign.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            var pairs = new HashSet<((string, string), (string, string))>();
            for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
            {
                pairs.Add(((items[i].Key, items[i].Value), (items[j].Key, items[j].Value)));
            }
            return pairs;
        }

        // пара (ровно по двум параметрам) недостижима, если она целиком покрывается каким-то forbid,
        // и этот forbid касается только этих двух ключей (или их подмножества) с точным совпадением значений
        private static bool PairIsForbidden(
            ((string, string), (string, string)) pair,
            List<Dictionary<string, string>> forbids)
        {
            if (forbids == null || forbids.Count == 0) return false;

            var d = new Dictionary<string, string>
            {
                [pair.Item1.Item1] = pair.Item1.Item2,
                [pair.Item2.Item1] = pair.Item2.Item2
            };

            foreach (var f in forbids)
            {
                // forbid должен быть применим к этим двум параметрам (или их подмножеству)
                // и полностью совпадать по указанным в forbid ключам/значениям
                bool keysWithinPair = f.Keys.All(k => d.ContainsKey(k));
                if (!keysWithinPair) continue;

                bool valuesMatch = f.All(kv => d.TryGetValue(kv.Key, out var v) && v == kv.Value);
                if (valuesMatch) return true;
            }
            return false;
        }

        public static List<Dictionary<string, string>> GeneratePairwise(
            Dictionary<string, List<string>> parameters,
            List<Dictionary<string, string>>? forbid = null,
            List<Dictionary<string, string>>? require = null)
        {
            forbid ??= new();
            require ??= new();

            var keys = parameters.Keys.ToList();

            // 1) цель покрытия по всем парам
            var toCover = AllPairsToCover(parameters);

            // 1a) убираем недостижимые пары (прямо запрещённые forbid по тем же двум параметрам)
            toCover.RemoveWhere(p => PairIsForbidden(p, forbid));

            var covered = new HashSet<((string, string), (string, string))>();
            var rows = new List<Dictionary<string, string>>();

            Dictionary<string, string> GreedySeed()
                => keys.ToDictionary(k => k, k => parameters[k].First());

            // жадное улучшение строки: выбираем значения, дающие максимальный прирост покрытия
            Dictionary<string, string> Improve(Dictionary<string, string> seed)
            {
                var best = new Dictionary<string, string>(seed);
                // приоритизируем самую «дорогую» ось Browser×OS, затем Browser×Auth, затем OS×Auth
                int Score(Dictionary<string, string> row)
                {
                    var prs = PairsFrom(row);
                    int newPairs = prs.Count(p => !covered.Contains(p));

                    // «весовая» метрика для осей (если нужны имена осей, можно усилить подсчёт)
                    int neo = 0, nba = 0, noa = 0;
                    foreach (var p in prs.Where(p => !covered.Contains(p)))
                    {
                        var a = p.Item1.Item1; var b = p.Item2.Item1;
                        var names = new HashSet<string> { a, b };
                        if (names.SetEquals(new[] { "Browser", "OS" })) neo++;
                        else if (names.SetEquals(new[] { "Browser", "Auth" })) nba++;
                        else if (names.SetEquals(new[] { "OS", "Auth" })) noa++;
                    }
                    // Сильный приоритет закрытия Browser×OS
                    return neo * 10000 + nba * 100 + noa * 10 + newPairs;
                }

                bool improved = true;
                int bestScore = Score(best);
                while (improved)
                {
                    improved = false;
                    foreach (var k in keys)
                    {
                        foreach (var v in parameters[k])
                        {
                            var trial = new Dictionary<string, string>(best) { [k] = v };
                            if (Constraints.ViolatesForbid(trial, forbid)) continue;
                            int score = Score(trial);
                            if (score > bestScore)
                            {
                                best = trial; bestScore = score; improved = true;
                            }
                        }
                    }
                }
                return best;
            }

            // Основной жадный цикл
            int attempts = 0;
            int maxAttempts = Math.Max(100, 5 * toCover.Count);
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
                            var trial = new Dictionary<string, string>(cand) { [k] = v };
                            if (!Constraints.ViolatesForbid(trial, forbid))
                            {
                                cand = trial; changed = true; break;
                            }
                        }
                        if (changed) break;
                    }
                    if (Constraints.ViolatesForbid(cand, forbid)) continue;
                }

                // попытка удовлетворить require (если возможно без нарушения forbid)
                if (require.Count > 0 && !Constraints.SatisfiesRequire(cand, require))
                {
                    foreach (var r in require)
                    {
                        var trial = new Dictionary<string, string>(cand);
                        foreach (var kv in r) trial[kv.Key] = kv.Value;
                        if (!Constraints.ViolatesForbid(trial, forbid)) { cand = trial; break; }
                    }
                }

                var newPairs = PairsFrom(cand).Except(covered).ToList();
                if (newPairs.Count == 0) continue;

                rows.Add(cand);
                foreach (var p in PairsFrom(cand)) covered.Add(p);
            }

            // Добивка картежем (только если нужно; домены должны быть умеренными)
            if (!covered.SetEquals(toCover))
            {
                IEnumerable<Dictionary<string, string>> Cartesian()
                {
                    IEnumerable<Dictionary<string, string>> seed = new[] { new Dictionary<string, string>() };
                    foreach (var k in keys)
                    {
                        seed = from s in seed
                               from v in parameters[k]
                               select new Dictionary<string, string>(s) { [k] = v };
                    }
                    return seed;
                }

                foreach (var cand in Cartesian())
                {
                    if (Constraints.ViolatesForbid(cand, forbid)) continue;
                    var need = PairsFrom(cand).Except(covered).Any();
                    if (need)
                    {
                        rows.Add(cand);
                        foreach (var p in PairsFrom(cand)) covered.Add(p);
                    }
                    if (covered.SetEquals(toCover)) break;
                }
            }

            // 2) Post-pruning: выкидываем строки, без которых покрытие остаётся полным
            bool pruned = true;
            while (pruned)
            {
                pruned = false;
                for (int i = rows.Count - 1; i >= 0; i--)
                {
                    var saved = rows[i];
                    rows.RemoveAt(i);

                    var coveredNow = new HashSet<((string, string), (string, string))>();
                    foreach (var r in rows)
                        foreach (var p in PairsFrom(r)) coveredNow.Add(p);

                    if (!coveredNow.IsSupersetOf(toCover))
                    {
                        // строка была нужна — вернём
                        rows.Insert(i, saved);
                    }
                    else
                    {
                        pruned = true; // получилось убрать — попробуем ещё
                    }
                }
            }

            // стабилизация порядка ключей и уникализация
            var seen = new HashSet<string>();
            var orderedKeys = keys;
            var uniq = new List<Dictionary<string, string>>();
            foreach (var r in rows)
            {
                var key = string.Join("|", orderedKeys.Select(k => k + "=" + r[k]));
                if (seen.Add(key))
                    uniq.Add(orderedKeys.ToDictionary(k => k, k => r[k]));
            }
            return uniq;
        }
    }
}