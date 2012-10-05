using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace decaf
{
    public class Node
    {
        public string Type { get; set; }
        public byte Redundancy { get; set; }
        public byte Required { get; set; }
        public Dictionary<String, Double> Cascading { get; set; }
        public double[] Failure { get; set; }
        public double[] Repair { get; set; }
    }
}
