using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace decaf
{
    internal class State : Simulation, IEnumerable<KeyValuePair<string, byte>>
    {
        public int Environment;
        public Dictionary<string, byte> Vector;

        public State(int e)
        {
            if (e < 0 || e >= MaxEnvironment)
            {
                throw new IndexOutOfRangeException();
            }
            Environment = e;
        }

        public int Index
        {
            get
            {
                int result = TypeList.Sum(t => Vector[t]*MaxVector[t]);
                return result*(MaxEnvironment + Environment);
            }
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