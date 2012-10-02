using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace decaf
{
    class Node
    {
        public byte redundancy;
        public byte required;
        public Dictionary<String, Double> cascading;
        public double[] failure;
        public double[] repair;
    }
}
