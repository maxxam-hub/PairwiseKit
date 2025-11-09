using System.Collections.Generic;

namespace PairwiseKit.Models
{
    public class Spec
    {
        public Dictionary<string, List<string>> Parameters { get; set; } = new();
        public List<Dictionary<string, string>> Forbid { get; set; } = new();
        public List<Dictionary<string, string>> Require { get; set; } = new();
    }
}