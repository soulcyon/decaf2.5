using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace decaf
{
    public static class Simulation
    {
        #region Generic parameters

        public static double[,] Environments;
        public static double[,] QMatrix;
        public static UInt16 Length;
        public static short MaxEnvironment;
        public static Dictionary<string, byte> MaxVector;
        public static Dictionary<State, ushort> StateMap;
        public static Dictionary<String, Node> Components;
        public static List<State> StateList;
        public static Stopwatch Timer;
        #endregion

        #region Statistics
        public static ulong TimeToStates = 0; /* Generating all combinations of states */
        public static ulong TimeToTreeGen = 0; /* Filling failure transitions with trees */
        public static ulong TimeToQMatrix = 0; /* Filling repair and env-change in QMatrix */
        public static ulong TimeToMTTF = 0; /* Calculating mean time to failure */
        public static ulong TimeToSSU = 0; /* Calculating steady-state unavailability */
        public static uint NumberOfAvoidedTrees = 0; /* Number of trees not generated explicitly */
        public static uint NumberOfTransitions = 0; /* Number of failure transitions filled in the QMatrix */
        public static uint NumberOfTrees = 0; /* Number of all trees - identical to the value in Srini's code */
        public static uint NumberOfUniqueTrees = 0; /* Number of trees uniquely generated */
        
        #endregion

        #region Internal Variables
        private static readonly Dictionary<string, List<List<string>>> BinaryEcache = new Dictionary<string, List<List<string>>>(); 
        #endregion

        #region Main methods

        public static void Setup(Json intialJson)
        {
            #region Integrity Check

            // Assert environments variable is defined
            if (intialJson.environments == null || intialJson.environments.LongLength == 0)
            {
                throw new Exception("Env-change matrix not found or empty in configuration");
            }

            // Assert env-change matrix is a square
            if (Math.Sqrt(intialJson.environments.LongLength) % 1 > 0)
            {
                throw new Exception("Env-change matrix should be a square matrix");
            }

            // Assert env-change matrix diagonal consists of zeros
            for (var i = 0; i < intialJson.environments.GetLength(0); i++)
            {
                if (Math.Abs(intialJson.environments[i, i]) > 0)
                {
                    throw new Exception("Env-change matrix diagonal should only be ZERO");
                }
            }

            // Assert components variable is defined
            if (intialJson.components == null || intialJson.components.Keys.Count == 0)
            {
                throw new Exception("Components object not found or empty in configuration");
            }

            // Per-component integrity checks
            foreach (var sNkvp in intialJson.components)
            {
                // Assert equal number of failure rates as environments
                if (sNkvp.Value.Failure.Length != intialJson.environments.GetLength(0))
                {
                    throw new Exception("Comp:" + sNkvp.Key + ".failure rates should be defined for each Environment.");
                }
                // Assert equal number of repair rates as environments
                if (sNkvp.Value.Repair.Length != intialJson.environments.GetLength(0))
                {
                    throw new Exception("Comp:" + sNkvp.Key + ".repair rates should be defined for each Environment.");
                }
                // Assert all cascading components are valid components
                foreach (var sDkvp in sNkvp.Value.Cascading)
                {
                    if (!intialJson.components.ContainsKey(sDkvp.Key))
                    {
                        throw new Exception("Comp:" + sNkvp.Key + ".cascading:" + sDkvp.Key +
                                            " is an invalid cascading component.");
                    }
                }
            }

            // TypeList, StateList and StateMap should not be defined at this point - scare off hackers
            if (!(StateList == null && StateMap == null))
            {
                throw new Exception("Hack attempt!");
            }

            #endregion

            // Initialize necessary variables
            Environments = intialJson.environments;
            Components = intialJson.components;
            MaxEnvironment = (short) Environments.GetLength(0);
            Timer = new Stopwatch();
        }

        public static void Run()
        {
            // Simplified running scheme
            GenerateStates();
            MessageBox.Show(Length + ":");
            BuildQMatrix();
            MessageBox.Show(NumberOfTrees.ToString());
        }

        public static void GenerateStates()
        {
            // Something probably went wrong with Json reading
            if (Components.Count < 1)
            {
                throw new InvalidOperationException("Please run Setup before GenerateStates");
            }

            // Stopwatch start
            Timer.Start();

            // Initialize necessary variables
            var initialState = new List<byte>();
            StateList = new List<State>();
            StateMap = new Dictionary<State, ushort>();

            // Generate states
            Haircomb(0, initialState);

            // We can initialize more variables
            MaxVector = StateList.Last().Vector;
            Length = (ushort) StateList.Count;
            QMatrix = new double[Length, Length];

            // Stopwatch stop and store statistic
            Timer.Stop();
            TimeToStates = (ulong)Timer.ElapsedMilliseconds;
        }

        public static void BuildQMatrix()
        {
            // Something probably went wrong with Json reading
            if (Length < 1 || StateMap.Count < 1)
            {
                throw new InvalidOperationException("Please run GenerateStates before BuildQMatrix");
            }

            // Stopwatch start
            Timer.Start();

            // Fill Env-change rates
            for (var t = 0; t < Length; t += MaxEnvironment)
            {
                for (var i = 0; i < MaxEnvironment; i++)
                {
                    for (var j = 0; j < MaxEnvironment; j++)
                    {
                        QMatrix[t + i, t + j] = Environments[i, j];
                    }
                }
            }

            // Fill Repair rates
            var validRepairs = Components.Select(k => new State(0) {{k.Key, 1}}).ToList();
            for( var i = Length - 1; i >= 0; i-- )
            {
                foreach (var t in validRepairs)
                {
                    t.Environment = StateList[i].Environment;
                    var fromState = StateList[i] + t;
                    if (StateMap.ContainsKey(fromState))
                    {
                        QMatrix[StateMap[fromState], i] = t * fromState;
                    }
                }
            }

            // Fill Failure rates
            GenerateTrees();

            // Stopwatch stop and store statistic
            Timer.Stop();
            TimeToQMatrix = (ulong)Timer.ElapsedMilliseconds;

            // Print QMatrix - Debug
            var result = "";
            for (var i = 0; i < Length; i++)
            {
                for (var j = 0; j < Length; j++)
                {
                    result += QMatrix[i, j] + "\t";
                }
                result += "\n";
            }
            MessageBox.Show(result);
        }

        private static void GenerateTrees()
        {
            // Stopwatch start
            Timer.Start();

            // Cache the powerSet of all gammas
            foreach (var cKvp in Components)
            {
                BinaryEcache[cKvp.Key] = PowerSet(cKvp.Value.Cascading);
            }

            // These foreach loops must be separated to take full advantage of cache
            foreach (var cKvp in Components)
            {
                // Create initial level reading, failureTransition, bfhMap
                var levels = new List<List<string>> { new List<string> { "1:" + cKvp.Key } };
                var initialFt = new State(0) {{cKvp.Key, 1}};
                var bfhMap = Components.ToDictionary(cc => cc.Key, cc => new List<string>());
                bfhMap[cKvp.Key].Add("|");

                // Run the tree expansion
                RecurseChildren(levels, initialFt, 1.0, bfhMap);
            }

            // Stopwatch stop and store statistic
            Timer.Stop();
            TimeToTreeGen = (ulong)Timer.ElapsedMilliseconds;
        }

        private static void RecurseChildren(List<List<string>> levels, State differenceState, double subtreeRate, Dictionary<string, List<string>> bfhMap)
        {
            var debugString = "";
            // Required variables for proper powerSet recursion
            var gammaPermutations = new List<int>();
            var terminalTypes = new List<string>();
            var productSet = new List<List<int>>();

            // Fill gammaPermutations list with possible failure sets
            foreach (var type in from terminalNode in levels.Last() let type = terminalNode.Split(':')[1] where terminalNode[0] == '1' select type)
            {
                gammaPermutations.Add(BinaryEcache[type].Count);
                terminalTypes.Add(type);
            }

            // Fill in productSet with all combinations of gammaPermutations
            Nitcomb(0, gammaPermutations, new List<int>(), ref productSet);

            debugString += "\nPS Size: " + productSet.Count + "\n";
            for( var i = 0; i < productSet.Count; i++  )
            {
                // Copy each of the arguments so they don't leak into future recursions
                var levelsCopy = levels.Select(k => k.ToList()).ToList();
                var stateCopy = differenceState.Clone();
                var bfhCopy = bfhMap.ToDictionary(kvp => kvp.Key, kvp => new List<string>(kvp.Value));
                var newRate = subtreeRate;
                var nextLevel = new List<string>();

                // Foreach binary '1' in the product, populate breadth-first history and increment differentState
                debugString += "  BE Size: " + productSet[i].Count + "\n";
                for (var b = 0; b < productSet[i].Count; b++)
                {
                    var gammaStatus = BinaryEcache[terminalTypes[b]][productSet[i][b]];
                    nextLevel = gammaStatus;
                    debugString += "    GS Size: " + gammaStatus.Count+ "\n";
                    foreach (var childType in from t in gammaStatus let childType = t.Split(':')[1] where t[0] == '1' select childType)
                    {
                        stateCopy.Vector[childType]++;
                        bfhCopy[childType].Add(terminalTypes[b]);
                    }
                }

                // If any of the differentState's vectors are above redundancy, continue to next product
                if (Components.Any(cKvp => differenceState.Vector[cKvp.Key] > cKvp.Value.Redundancy))
                {
                    continue;
                }

                // We're at peace with the product, thus fill QMatrix
                if (i <= 0)
                {
                    debugString += "      PING: " + NumberOfTrees + "\n";
                    ProcessRates();
                }
                else
                {
                    levelsCopy.Add(nextLevel);
                    RecurseChildren(levelsCopy, stateCopy, newRate, bfhCopy);
                }
            }
            MessageBox.Show(debugString);
        }

        private static void ProcessRates()
        {
            NumberOfTrees++;
        }

        private static List<List<string>> PowerSet(Dictionary<string, double> cascading)
        {
            var result = new List<List<string>>();
            if (cascading == null || cascading.Count == 0)
            {
                return result;
            }
            for (var i = 0; i < Math.Pow(2, cascading.Count); i++)
            {
                var binary = Convert.ToString(i, 2).PadLeft(cascading.Count, '0');
                var block = binary.Select((t1, t) => t1 + ":" + cascading.Keys.ToArray()[t]).ToList();
                result.Add(block);
            }
            return result;
        }

        private static void Nitcomb(int index, IReadOnlyList<int> limits, List<int> current, ref List<List<int>> result )
        {
            if( current.Count == limits.Count )
            {
                result.Add(current);
            }
            if( index >= limits.Count )
            {
                return;
            }
            for( var i = 0; i < limits[index]; i++ )
            {
                Nitcomb(index + 1, limits, new List<int>(current) { i }, ref result);
            }
        }

        private static void Haircomb(int index, IReadOnlyList<byte> current)
        {
            if (current == null)
            {
                throw new ArgumentNullException("current");
            }

            // If the Vector is the correct Length, then add to StateMap and StateList
            if (current.Count == Components.Count)
            {
                for (int i = 0, j = 0; i < MaxEnvironment; i++, j = 0)
                {
                    var temp = new State(i);
                    foreach (var t in Components)
                    {
                        temp.Add(t.Key, current[j]);
                        j++;
                    }
                    StateMap.Add(temp, (ushort) StateList.Count);
                    StateList.Add(temp);
                }
            }

            // If the Index exceeds the number of components, break out
            if (index >= Components.Count)
            {
                return;
            }

            // Loop through the required number of redundancies for the Index-th component
            for (var i = 0; i <= Components[Components.Keys.ToArray()[index]].Redundancy; i++)
            {
                // Create new Vector and push to next recursion
                Haircomb(index + 1, new List<byte>(current) {(byte) i});
            }
        }

        public static double CalculateMttf()
        {
            throw new NotImplementedException();
        }

        public static double CalculateSsu()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}