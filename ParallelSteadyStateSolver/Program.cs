using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ParallelSteadyStateSolver
{
    class Program
    {
        const int N = 100;
        static Random rand = new Random(42);

        static void Main(string[] args)
        {

            //double[,] mchain =
            //{
            //    {0.65, 0.15, 0.1},
            //    {0.25, 0.65, 0.4},
            //    {0.1,  0.2,  0.5},
            //};

            //MarkovChain m = new MarkovChain(mchain);
            //Console.WriteLine(m);

            //var solved = m.SteadyStateValues();
            //foreach (var s in solved) Console.WriteLine($"pi_{s.Pi} = {s.Value}");

            //var solved2 = m.SteadyStateValues2();
            //foreach (var s in solved2) Console.WriteLine($"pi_{s.Pi} = {s.Value}");

            //double[,] mchain = new double[N, N];
            //for (int i = 0; i < N; i++)
            //{
            //    for (int j = 0; j < N; j++)
            //    {
            //        mchain[i, j] = (i + j) / 200;
            //    }
            //}
            //for (int k = 0; k < 100; k++)
            //{
            //    MarkovChain m = new MarkovChain(mchain);
            //    var solved = m.SteadyStateValues();
            //    Console.WriteLine(m);
            //}
            //foreach (var s in solved) Console.WriteLine($"pi_{s.Pi} = {s.Value}");

            var m = new MarkovChain(AllocateMatrix(N));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var solved = m.SteadyStateValues();
            //foreach (var s in solved) Console.WriteLine($"pi_{s.Pi} = {s.Value}");

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

            Console.ReadLine();
        }

        static double[,] AllocateMatrix(int n, bool initialize = true)
        {
            var matrix = new double[n, n];
            if (initialize)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        matrix[i, j] = rand.NextDouble();
                    }
                }
            }
            return matrix;
        }
    }

    public class MarkovChain
    {
        private double[,] Matrix;
        private SteadyStateEquation[] SteadyStateEquations;
        private SteadyStateValue[] SolvedSteadyStateValues;
        private int Len;

        public MarkovChain(double[,] matrix)
        {
            Matrix = matrix;
            Len = matrix.GetLength(0);

            SteadyStateEquations = new SteadyStateEquation[Len];
            SolvedSteadyStateValues = new SteadyStateValue[Len];

            for (int i = 0; i < Len; i++)
            {
                double[] row = Enumerable.Range(0, Len)
                    .Select(x => matrix[i, x])
                    .ToArray();

                SteadyStateEquations[i] = new SteadyStateEquation(i, row);
            }
        }

        public SteadyStateValue[] SteadyStateValues() //solve
        {
            //simplifcation can be done to make things simpler from the start
            foreach (SteadyStateEquation steadyStateEquation in SteadyStateEquations) 
                steadyStateEquation.Simplify();

            for (int i = 1; i < SteadyStateEquations.Length; i++)
                for (int j = 1; j < SteadyStateEquations.Length; j++)
                    if (i != j)
                    {
                        //i is substituted into j
                        SteadyStateEquations[j].SubstituteEquation(SteadyStateEquations[i]);
                        SteadyStateEquations[j].Simplify();
                    }

            double pi_0 = GetPi_0();
            SolveAll(pi_0);

            return SolvedSteadyStateValues;
        }
        #region solving
        public double GetPi_0()
        {
            double sum = 1;

            for (int i = 1; i < SteadyStateEquations.Length; i++)
                sum += SteadyStateEquations[i].SteadyStateValues.First().Value;

            return 1 / sum;
        }

        public void SolveAll(double pi_0Value)
        {
            SolvedSteadyStateValues[0] = new SteadyStateValue(0, pi_0Value);

            for (int i = 1; i < Len; i++)
            {
                SteadyStateEquation equation = SteadyStateEquations[i];
                double valueInTermsOfPi_0 = equation.SteadyStateValues.First().Value;
                SteadyStateValue solved = new SteadyStateValue(equation.Equivalent, valueInTermsOfPi_0 * pi_0Value);
                SolvedSteadyStateValues[equation.Equivalent] = solved;
            }
        }
        #endregion solving

        public override string ToString()
        {
            StringBuilder markovChainString = new StringBuilder();

            for (int i = 0; i < Len; i++)
            {
                for (int j = 0; j < Len; j++)
                    markovChainString.Append($"{Matrix[i, j]}  ");

                markovChainString.AppendLine();
            }

            return markovChainString.ToString();
        }
    }

    public class SteadyStateEquation
    {
        public int Equivalent { get; set; }
        public List<SteadyStateValue> SteadyStateValues { get; set; }

        public SteadyStateEquation(int equivalent, double[] values)
        {
            Equivalent = equivalent;

            SteadyStateValues = new List<SteadyStateValue>();

            for (int i = 0; i < values.Length; i++)
                SteadyStateValues.Add(new SteadyStateValue(i, values[i]));
        }

        #region substitution
        public void SubstituteEquation(SteadyStateEquation subEquation)
        {
            for (int i = SteadyStateValues.Count - 1; i >= 0; i--)
                if (SteadyStateValues[i].Pi == subEquation.Equivalent)
                    SubstituteValue(i, subEquation);
        }

        private void SubstituteValue(int oldSteadyStateValueIndex, SteadyStateEquation SubEquation)
        {
            double multiplier = SteadyStateValues[oldSteadyStateValueIndex].Value;
            SteadyStateValues.RemoveAt(oldSteadyStateValueIndex);

            foreach (SteadyStateValue newSteadyStateValue in SubEquation.SteadyStateValues)
            {
                bool addedFlag = false;
                int newPi = newSteadyStateValue.Pi;
                double newVal = newSteadyStateValue.Value * multiplier;

                foreach(SteadyStateValue oldSteadyStateValue in SteadyStateValues)
                {
                    if (newPi == oldSteadyStateValue.Pi)
                    {
                        oldSteadyStateValue.Value += newVal;
                        addedFlag = true;
                        break;
                    }
                }

                if (!addedFlag)
                    SteadyStateValues.Add(new SteadyStateValue(newSteadyStateValue.Pi, newVal));

            }

                //foreach (SteadyStateValue newSteadyStateValue in SubEquation.SteadyStateValues)
                //    SteadyStateValues.Add(new SteadyStateValue(newSteadyStateValue.Pi, newSteadyStateValue.Value * multiplier));



            //SteadyStateValues.RemoveAt(oldSteadyStateValueIndex);
        }

        //public void Consolidate()
        //{
        //    List<int> removalIndices = new List<int>();

        //    for (int i = SteadyStateValues.Count - 1; i >= 0; i--)
        //        for (int j = SteadyStateValues.Count - 1; j >= 0; j--)
        //            if (i != j && SteadyStateValues[i].Pi == SteadyStateValues[j].Pi && !removalIndices.Contains(j))
        //            {
        //                double p = SteadyStateValues[i].Value;
        //                removalIndices.Add(i);
        //                SteadyStateValues[j].Value += p;
        //            }

        //    removalIndices.ForEach(i => SteadyStateValues.RemoveAt(i));
        //}

        public void Consolidate()
        {

            for (int i = SteadyStateValues.Count - 2; i >= 0; i--)
            {
                SteadyStateValue last = SteadyStateValues[i + 1];
                SteadyStateValue current = SteadyStateValues[i];

                if (last.Pi == current.Pi)
                {
                    current.Value += last.Value;
                    SteadyStateValues.RemoveAt(i + 1);
                }

            }
        }

        public void Simplify()
        {
            double compliment = 1;

            for (int i = SteadyStateValues.Count - 1; i >= 0; i--)
                if (SteadyStateValues[i].Pi == Equivalent)
                {
                    compliment = 1 - SteadyStateValues[i].Value;
                    SteadyStateValues.RemoveAt(i);
                    break;
                }

            for (int i = 0; i < SteadyStateValues.Count; i++)
                SteadyStateValues[i].Value /= compliment;
        }
        #endregion substitution

    }

    public class SteadyStateValue
    {
        public int Pi { get; set; } //as an index
        public double Value { get; set; }

        public SteadyStateValue(int pi, double value)
        {
            Pi = pi;
            Value = value;
        }
    }
}
