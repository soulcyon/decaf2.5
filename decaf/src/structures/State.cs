using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace decaf
{
    public class State : IEnumerable<KeyValuePair<string, byte>>
    {
        public int Environment;
        public Dictionary<string, byte> Vector;
        public int Index
        {
            get { return Simulation.StateMap[this]; }
        }

        public State(int e)
        {
            if (e < 0 || e >= Simulation.MaxEnvironment)
            {
                throw new IndexOutOfRangeException("Expecting 0 > e > " + Simulation.MaxEnvironment + ", but found " + e);
            }
            Environment = e;
            Vector = new Dictionary<string, byte>();
            foreach( var type in Simulation.TypeList )
            {
                Vector.Add(type, 0);
            }
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
            foreach(var k in Simulation.TypeList)
            {
                temp.Vector.Add(k, (byte)(a.Vector[k] + b.Vector[k]));
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
            foreach (var k in Simulation.TypeList)
            {
                temp.Vector.Add(k, (byte)(a.Vector[k] - b.Vector[k]));
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
    }
}