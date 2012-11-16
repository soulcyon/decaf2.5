using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Double.Solvers;
using MathNet.Numerics.LinearAlgebra.Double.Solvers.Iterative;
using MathNet.Numerics.LinearAlgebra.Double.Solvers.Preconditioners;

namespace decaf
{
    public static class Simulation
    {
        #region Generic parameters

        public static double[,] Cascading;
        public static double[,] Environments;
        public static DenseMatrix QMatrix;
        public static ushort NodeLength;
        public static ushort EnvLength;
        public static ushort Length;
        public static List<ushort> UpStates;
        public static Node[] Components;
        public static Stopwatch Timer;

        #endregion

        #region Statistics
        public static String TimeToEnvChange
        {
            get { return _timeToEnvChange/(double) Stopwatch.Frequency + " s"; }
        }
        public static String TimeToRepair
        {
            get { return _timeToRepair / (double)Stopwatch.Frequency + " s"; }
        }
        public static String TimeToFail
        {
            get { return _timeToFail / (double)Stopwatch.Frequency + " s"; }
        }
        public static String TimeToQMatrix
        {
            get { return _timeToQMatrix / (double)Stopwatch.Frequency + " s"; }
        }
        public static String TimeToMTTF
        {
            get { return _timeToMTTF / (double)Stopwatch.Frequency + " s"; }
        }
        public static String TimeToSSU
        {
            get { return _timeToSSU / (double)Stopwatch.Frequency + " s"; }
        }
        private static ulong _timeToEnvChange; /* Filling in env-change transitions */
        private static ulong _timeToRepair; /* Filling in repair transitions */
        private static ulong _timeToFail; /* Filling in failure transitions - including tree generation */
        private static ulong _timeToQMatrix; /* Filling in complete QMatrix */
        private static ulong _timeToMTTF; /* Calculating mean time to failure */
        private static ulong _timeToSSU; /* Calculating steady-state unavailability */
        public static uint NumberOfTrees; /* Number of all trees - identical to the value in Srini's code */
        public static uint NumberOfUniqueTrees; /* Number of trees uniquely generated */

        #endregion

        #region Internal Variables

        private static List<string>[] _binaryEcache;
        private static bool _summableMatrix;

        #endregion

        #region Main methods

        public static void Setup(Json initialJson)
        {
            #region Integrity Check

            // Assert environments variable is defined
            if (initialJson.environments == null || initialJson.environments.LongLength == 0)
            {
                throw new Exception("Env-change matrix not found or empty in configuration");
            }

            // Assert env-change matrix is a square
            if (Math.Sqrt(initialJson.environments.LongLength)%1 > 0)
            {
                throw new Exception("Env-change matrix should be a square matrix");
            }

            // Assert env-change matrix diagonal consists of zeros
            for (var i = 0; i < initialJson.environments.GetLength(0); i++)
            {
                if (Math.Abs(initialJson.environments[i, i]) > 0)
                {
                    throw new Exception("Env-change matrix diagonal should only be ZERO");
                }
            }

            // Assert components variable is defined
            if (initialJson.components == null || initialJson.components.Length == 0)
            {
                throw new Exception("Components object not found or empty in configuration");
            }

            // Per-component integrity checks
            foreach (var failureNode in initialJson.components)
            {
                // Assert equal number of failure rates as environments
                if (failureNode.Failure.Length != initialJson.environments.GetLength(0))
                {
                    throw new Exception("Comp:" + failureNode.Type +
                                        ".failure rates should be defined for each Environment.");
                }
                // Assert equal number of repair rates as environments
                if (failureNode.Repair.Length != initialJson.environments.GetLength(0))
                {
                    throw new Exception("Comp:" + failureNode.Type +
                                        ".repair rates should be defined for each Environment.");
                }
            }

            #endregion

            // Initialize necessary variables
            Environments = initialJson.environments;
            Components = initialJson.components;
            Cascading = initialJson.cascading;
            EnvLength = (ushort) Environments.GetLength(0);
            NodeLength = (ushort) Components.Length;
            Length = 1;

            // Populate Cascading List for each component
            for (ushort i = 0; i < NodeLength; i++)
            {
                Components[i].Cascading = new List<ushort>();
                for (ushort j = 0; j < NodeLength; j++)
                {
                    if (Cascading[i, j] > 0)
                        Components[i].Cascading.Add(j);
                }
            }
            _binaryEcache = new List<string>[NodeLength];
            Timer = new Stopwatch();
        }

        public static void Run()
        {
            var tempTimer = new Stopwatch();

            // Calculate Optimization Parameters
            _summableMatrix = new SparseMatrix(Cascading).NonZerosCount/NodeLength*NodeLength - NodeLength > 0.5;

            // Calculate Length
            for (var i = 0; i < NodeLength; i++)
            {
                Length *= (ushort) (Components[i].Redundancy + 1);
            }
            Length *= EnvLength;

            // Calculate UpStates
            UpStates = new List<ushort>();
            for (ushort i = 0; i < Length; i++)
            {
                var valid = true;
                var tempState = IntToState(i);
                for (var j = 0; j < NodeLength; j++)
                {
                    if (Components[j].Redundancy - tempState[j] >= Components[j].Required) continue;
                    valid = false;
                    break;
                }
                if( valid ){
                    UpStates.Add(i);
                }
            }
            tempTimer.Start();
            // Initialize QMatrix and build
            QMatrix = new DenseMatrix(Length, Length);
            BuildQMatrix();
            _timeToQMatrix = (ulong) tempTimer.ElapsedTicks;
            tempTimer.Stop();
        }

        public static void BuildQMatrix()
        {
            Timer.Start();

            // Fill Env-change rates
            for (var t = 0; t < Length; t += EnvLength)
            {
                for (var i = 0; i < EnvLength; i++)
                {
                    for (var j = 0; j < EnvLength; j++)
                    {
                        QMatrix[t + i, t + j] = Environments[i, j];
                    }
                }
            }

            _timeToEnvChange = (ulong)Timer.ElapsedTicks;
            Timer.Restart();

            // Fill Repair rates
            for (ushort i = 0; i < Length; i++)
            {
                var currState = IntToState(i);
                for (ushort j = 0; j < NodeLength; j++)
                {
                    var temp = (ushort[]) currState.Clone();
                    temp[j] += 1;
                    var toIndex = StateToInt(temp);
                    if (toIndex == ushort.MaxValue) continue;

                    ushort sum = 0;
                    for (ushort k = 0; k < NodeLength; k++)
                    {
                        sum += temp[k];
                    }
                    QMatrix[toIndex, i] = temp[j]*Components[j].Repair[temp[NodeLength]]/sum;
                }
            }

            _timeToRepair = (ulong)Timer.ElapsedTicks;
            Timer.Restart();

            // Fill Failure rates
            GenerateTrees();

            _timeToFail = (ulong)Timer.ElapsedTicks;
            Timer.Stop();

            if (_summableMatrix) return;
            for (var i = 0; i < Length; i++)
            {
                double sum = 0;
                for (var j = 0; j < Length; j++)
                {
                    sum += QMatrix[i, j];
                }
                QMatrix[i, i] = -sum;
            }
        }

        private static void GenerateTrees()
        {
            // Cache the powerSet of all gammas
            for (var index = 0; index < NodeLength; index++)
            {
                if (!Components[index].Cascading.Any()) continue;
                _binaryEcache[index] = PowerSet(Components[index].Cascading);
            }

            // These for loops must be separated to take full advantage of cache
            for (var i = 0; i < NodeLength; i++)
            {
                // Create initial level reading, failureTransition, bfhMap
                var levels = new List<string> {"1:" + i};
                var initialFt = new ushort[NodeLength + 1];
                initialFt[i] += 1;
                var bfhMap = new List<ushort>[NodeLength];
                for (var t = 0; t < NodeLength; t++)
                {
                    bfhMap[t] = new List<ushort>();
                }
                bfhMap[i].Add(ushort.MaxValue);

                // Run the tree expansion
                RecurseChildren(levels, initialFt, 1.0, bfhMap);
            }
        }

        private static void RecurseChildren(List<string> levels, ushort[] differenceState, double subtreeRate,
                                            List<ushort>[] bfhMap)
        {
            // Required variables for proper powerSet recursion
            var gammaPermutations = new List<ushort>();
            var terminalTypes = new List<ushort>();
            var productSet = new List<List<ushort>>();

            // Fill gammaPermutations list with possible failure sets
            foreach(var terminalNode in levels.Last().Split(','))
            {
                if (terminalNode[0] != '1') continue;
                var type = ushort.Parse(terminalNode.Split(':')[1]);

                if (_binaryEcache[type] == null) continue;
                gammaPermutations.Add((ushort) _binaryEcache[type].Count);
                terminalTypes.Add(type);
            }

            // Fill in productSet with all combinations of gammaPermutations
            Nitcomb(0, gammaPermutations, new List<ushort>(), ref productSet);
            if (productSet.Count == 0)
            {
                productSet = new List<List<ushort>> {new List<ushort>()};
            }

            for (var i = 0; i < productSet.Count; i++)
            {
                // Copy each of the arguments so they don't leak into future recursions
                var levelsCopy = levels.ToArray().ToList();
                var stateCopy = (ushort[]) differenceState.Clone();
                var bfhCopy = new List<ushort>[NodeLength];
                for (var t = 0; t < NodeLength; t++)
                {
                    bfhCopy[t] = bfhMap[t].ToArray().ToList();
                }

                var newRate = subtreeRate;
                var nextLevel = "";

                // Foreach binary '1' in the product, populate breadth-first history and increment differentState
                for (var b = 0; b < productSet[i].Count; b++)
                {
                    var parentType = terminalTypes[b];
                    var block = "," + _binaryEcache[terminalTypes[b]][productSet[i][b]];
                    nextLevel += block;
                    var gammaStatus = block.Split(',');
                    foreach (var childInfo in gammaStatus)
                    {
                        if (childInfo == "") continue;

                        var childType = ushort.Parse(childInfo.Substring(childInfo.IndexOf(':') + 1));

                        if (childInfo[0] == '1')
                        {
                            stateCopy[childType] += 1;
                            bfhCopy[childType].Add(ushort.MaxValue);
                            newRate *= Cascading[parentType, childType];
                        }
                        else
                        {
                            bfhCopy[childType].Add(parentType);
                        }
                    }
                }

                // If any of the differentState's vectors are above redundancy, continue to next entry in productSet
                if (!ValidTransition(stateCopy) )
                {
                    continue;
                }
                if (i == 0)
                {
                    // Process the rate of the current subtree'
                    ProcessRates(levels, differenceState, subtreeRate, bfhCopy);
                }
                else
                {
                    levelsCopy.Add(nextLevel.Substring(1));
                    RecurseChildren(levelsCopy, stateCopy, newRate, bfhCopy);
                }
            }
        }
        private static bool ValidTransition(ushort[] transition)
        {
            for (var j = 0; j < NodeLength; j++)
            {
                if (transition[j] > Components[j].Redundancy)
                {
                    return false;
                }
            }
            return true;
        }
        
        /*private static string StateToString(IList<ushort> differenceState)
        {
            if (differenceState == null) throw new ArgumentNullException("differenceState");

            var result = "";
            for (var i = 0; i < NodeLength; i++)
            {
                result += ", " + Components[i].Type + "(" + differenceState[i] + ")";
            }
            return "(" + result.Substring(2) + ", " + differenceState[NodeLength] + ")";
        }*/

        /// <summary>
        /// ProcessRates is the last step in the TreeGeneration process.  This is where the QMatrix is finally filled.
        /// Taking the tree rate, root rate and complement rate, as per equation X.X, the final rate is updated into
        /// the respective QMatrix entry.  What is important to note is that the environment comes into play only at
        /// this stage - we decide to ignore storing any environment when growing sub trees or iterating roots.
        /// </summary>
        /// <param name="levels"></param>
        /// <param name="transition"></param>
        /// <param name="subTreeRate"></param>
        /// <param name="bfhCopy"></param>
        private static void ProcessRates(IReadOnlyList<string> levels, IList<ushort> transition, double subTreeRate,
                                         IList<List<ushort>> bfhCopy)
        {
            NumberOfTrees++;
            var rootIndex = ushort.Parse(levels[0].Split(':')[1]);

            // Rather than iterate the whole Length, we will jump EnvLength
            // If we were to skip certain transitions, this will save computation time
            for (ushort i = 0; i < Length; i += EnvLength)
            {
                var toState = new ushort[NodeLength + 1];
                var fromState = IntToState(i);

                // fromState + differenceState = toState
                for (ushort j = 0; j < NodeLength; j++)
                {
                    toState[j] = (ushort)(fromState[j] + transition[j]);
                }

                // Get the index of the toState, and properly handle invalid indices
                var toIndex = StateToInt(toState);
                if( toIndex == ushort.MaxValue )
                {
                    continue;
                }

                // Iterate over EnvLength
                for (ushort j = 0; j < EnvLength; j++)
                {
                    // Finally set the Environment into toState
                    toState[NodeLength] = j;

                    // Offset toIndex appropriately
                    toIndex += j;

                    // Definition of rooRate as per Equation X.X
                    var rootRate = (Components[rootIndex].Redundancy - fromState[rootIndex])*
                                   Components[rootIndex].Failure[j];

                    // BFH traversal
                    var complementRate = 1.0;
                    for (var k = 0; k < NodeLength; k++)
                    {
                        var compsAvailable = Components[k].Redundancy - fromState[k];
                        foreach (var s in bfhCopy[k])
                        {
                            if (s == ushort.MaxValue)
                            {
                                --compsAvailable;
                            }
                            else if (compsAvailable > 0)
                            {
                                complementRate *= 1 - Cascading[s, k];
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    // Update the QMatrix according to Equation X.X
                    QMatrix[i + j, toIndex] += rootRate*subTreeRate*complementRate;
                    
                    // Optimization Technique X.X - we can Sum up the diagonals at this point based on _summableMatrix
                    if (_summableMatrix)
                    {
                        QMatrix[i + j, i + j] += QMatrix[i + j, toIndex];
                    }
                }
            }
        }

        /// <summary>
        /// Power set enumeration using the binary counting method.  This is primarily used for exhaustively toggling a component's gamma.
        /// </summary>
        /// <param name="gammaList">List of gamma IDs</param>
        /// <returns>List of strings formatted (0|1):gammaID, for all gamma</returns>
        private static List<string> PowerSet(IReadOnlyList<ushort> gammaList)
        {
            var result = new List<string>();
            if (gammaList == null || gammaList.Count == 0)
            {
                return result;
            }
            for (var i = 0; i < Math.Pow(2, gammaList.Count); i++)
            {
                var binary = Convert.ToString(i, 2).PadLeft(gammaList.Count, '0');
                var block = "";
                for (var b = 0; b < binary.Length; b++)
                {
                    block += "," + binary[b] + ":" + gammaList[b];
                }
                result.Add(block.Substring(1));
            }
            return result;
        }

        /// <summary>
        /// Combination enumeration starting with a list <code>startList</code>, counting up to <code>endList</code>.
        /// <br />
        /// <list type="table">
        /// <listheader><term>For Example:</term></listheader>
        /// <item><term>Start</term><description>1, 1, 1</description></item>
        /// <item><term>End</term><description>2, 2, 2</description></item>
        /// </list>
        /// <br />
        /// <list type="table">
        /// <listheader><term>Will give a result list:</term></listheader>
        /// <item><term>1, 1, 2</term></item>
        /// <item><term>1, 2, 1</term></item>
        /// <item><term>2, 1, 1</term></item>
        /// <item><term>1, 2, 2</term></item>
        /// <item><term>2, 1, 2</term></item>
        /// <item><term>2, 2, 1</term></item>
        /// <item><term>2, 2, 2</term></item>
        /// </list>
        /// </summary>
        /// <param name="index">Initial call must pass 0</param>
        /// <param name="endList">Ending list</param>
        /// <param name="startList">Starting list</param>
        /// <param name="result">All intermediary lists will be pushed to the resulting List</param>
        private static void Nitcomb(ushort index, IReadOnlyList<ushort> endList, List<ushort> startList,
                                    ref List<List<ushort>> result)
        {
            if (startList.Count == endList.Count)
            {
                result.Add(startList);
            }
            if (index >= endList.Count)
            {
                return;
            }
            for (ushort i = 0; i < endList[index]; i++)
            {
                Nitcomb((ushort) (index + 1), endList, new List<ushort>(startList) {i}, ref result);
            }
        }

        /// <summary>
        /// IntToState calculates the transition of any valid Q-Matrix index.
        /// 
        /// Any invalid index will return null.
        /// </summary>
        /// <param name="i">Index</param>
        /// <returns></returns>
        public static ushort[] IntToState(ushort i)
        {
            // Sanity check
            if (i > Length) return null;

            // Resulting transition array
            var result = new ushort[NodeLength + 1];
            
            // First let's take care of the environment
            result[result.Length - 1] = (ushort) (i%EnvLength);
            i /= EnvLength;

            // Equation x.x
            for (var j = NodeLength - 1; j >= 0; j--)
            {
                result[j] = (ushort) (i%(Components[j].Redundancy + 1));
                i = (ushort) ((i - (i%(Components[j].Redundancy + 1)))/(Components[j].Redundancy + 1));
            }
            return result;
        }

        /// <summary>
        /// StateToInt calculates the Q-Matrix index of any valid transition.
        /// 
        /// Any invalid transition will return ushort.MaxValue
        /// </summary>
        /// <param name="state">Transition represented as a ushort[] of failed components.</param>
        /// <returns>Index of the state in the Q-Matrix</returns>
        public static ushort StateToInt(ushort[] state)
        {
            // Counter variables
            var result = 0;
            var temp = 1;

            // Equation x.x
            for (var i = NodeLength - 1; i >= 0; i--)
            {
                // If this is an invalid transition, return the magic number
                if (state[i] > Components[i].Redundancy)
                {
                    return ushort.MaxValue;
                }
                result += state[i] * temp;
                temp *= Components[i].Redundancy + 1;
            }

            // Finally we can take care of the environment
            return (ushort) (result*EnvLength + state[NodeLength]);
        }

        public static double CalculateMttf()
        {
            Timer.Restart();

            // Store necessary variables of formula
            var partialLength = (ushort)UpStates.Count();
            var pmatrix = new DenseMatrix(partialLength, partialLength);
            var hvector = new DenseVector(partialLength);

            // Convert QMatrix to PMatrix
            for( var i = 0; i < partialLength; i++ )
            {
                var diagonal = QMatrix[UpStates[i], UpStates[i]];
                hvector[i] = diagonal;
                for( var j = 0; j < partialLength; j++ )
                {
                    pmatrix[i, j] = -QMatrix[UpStates[i], UpStates[j]]/diagonal;
                }
            }

            // Invert PMatrix and multiply by H-vector to get MTTF
            var result = new BiCgStab().Solve(pmatrix, hvector);

            _timeToMTTF = (ulong)Timer.ElapsedTicks;

            return result[0];
        }

        public static double CalculateSsu()
        {
            // Stopwatch start
            Timer.Restart();
            var evector = new DenseVector(Length);
            for (var i = 0; i < Length; i++ )
            {
                QMatrix[i, 0] = 1;
                evector[i] = 0;
            }
            evector[0] = 1.0;
            var qtilde = new SparseMatrix(QMatrix.Transpose().ToArray());

            var v = new BiCgStab().Solve(qtilde, evector);
            var result = v.Sum() - UpStates.Sum(i => v[i]);

            // Stopwatch stop and store statistic
            _timeToSSU = (ulong)Timer.ElapsedTicks;
            Timer.Stop();

            return result;
        }

        #endregion
    }
}