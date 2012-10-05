using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace decaf
{
    public class State : IEnumerable<KeyValuePair<string, byte>>
    {
        public int Environment { get; set; }
        public Dictionary<string, byte> Vector { get; private set; }
        public int Index { get { return Simulation.StateMap[this]; } }

        public State(int e)
        {
            if (e < 0 || e >= Simulation.MaxEnvironment)
            {
                throw new IndexOutOfRangeException("Expecting 0 > e > " + Simulation.MaxEnvironment + ", but found " + e);
            }
            Environment = e;
            Vector = new Dictionary<string, byte>();
            foreach( var cKvp in Simulation.Components )
            {
                Vector.Add(cKvp.Key, 0);
            }
        }

        public void Add(string type, byte e)
        {
            if( Vector.ContainsKey(type) )
            {
                Vector[type] = e;
                return;
            }
            Vector.Add(type, e);
        }

        // Calculate repair rate
        public static double operator *(State a, State b)
        {
            var currentComponent = "";
            var sum = b.Vector.Aggregate(0, (current, k) => current + k.Value);
            foreach (var k in a.Vector.Where(k => k.Value == 1))
            {
                currentComponent = k.Key;
            }
            return b.Vector[currentComponent]*Simulation.Components[currentComponent].Repair[b.Environment]/sum;
        }

        // Add difference state
        public static State operator +(State a, State b)
        {
            if( a.Environment != b.Environment )
            {
                return null;
            }
            var temp = new State(a.Environment);
            foreach(var cKvp in Simulation.Components)
            {
                temp.Add(cKvp.Key, (byte)(a.Vector[cKvp.Key] + b.Vector[cKvp.Key]));
            }
            return temp;
        }

        // Subtract difference state
        public static State operator -(State a, State b)
        {
            if (a.Environment != b.Environment)
            {
                return null;
            }
            var temp = new State(a.Environment);
            foreach (var cKvp in Simulation.Components)
            {
                temp.Vector.Add(cKvp.Key, (byte)(a.Vector[cKvp.Key] - b.Vector[cKvp.Key]));
            }
            return temp;
        }

        public override String ToString()
        {
            return "(" + Vector.Aggregate("", (current, k) => current + (", " + k.Value)).Substring(2) + ", " + Environment + ")";
        }

        public override bool Equals(object obj)
        {
            var b = obj as State;

            return (b != null && b.Vector != null && b.Environment == Environment) && Vector.All(k => b.Vector[k.Key] == k.Value);
        }

        public override int GetHashCode()
        {
            var result = 0;
            result *= Environment.GetHashCode();
            result *= Vector.Sum(k => k.GetHashCode());
            return result.GetHashCode();
        }

        #region IEnumerable<KeyValuePair<string,byte>> Members

        public IEnumerator<KeyValuePair<string, byte>> GetEnumerator()
        {
            return Vector.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public State Clone()
        {
            var temp = new State(Environment);
            foreach (var k in Vector)
            {
                temp.Add(k.Key, k.Value);
            }
            return temp;
        }
    }
}