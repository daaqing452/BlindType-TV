using System;
using System.Collections.Generic;
using System.IO;


namespace ShowServer
{
    class Recognition
    {
        delegate double Prediction(string guess, List<Point2D> pointList);

        const int ALPHABET_SIZE = 26;
        const int LANGUAGE_MODEL_SIZE = 50000;
        const int TOP_K = 25;
        
        public Dictionary<string, double> languageModel;
        public GaussianPair[] absoluteGaussianPair;
        public GaussianPair[,] relativeGaussianPair;

        public GaussianPair[] absoluteLetterGaussianPair;
        public GaussianPair[] absoluteKeyboardGaussianPair;
        public GaussianPair[,] relativeKeyboardGaussianPair;
        Prediction prediction;

        public Recognition()
        {
            LoadLanguageModel();
            LoadAbsoluteLetterModel();
            LoadAbsoluteKeyboardModel();
            LoadRelativeKeyboardModel();
            ChangeMode(Algorithm.AGK);
        }
        public void ChangeMode(Algorithm algorithm)
        {
            switch (algorithm)
            {
                case Algorithm.AGL:
                    absoluteGaussianPair = absoluteLetterGaussianPair;
                    prediction = Absolute;
                    break;
                case Algorithm.AGK:
                    absoluteGaussianPair = absoluteKeyboardGaussianPair;
                    prediction = Absolute;
                    break;
                case Algorithm.RGK:
                    absoluteGaussianPair = absoluteKeyboardGaussianPair;
                    relativeGaussianPair = relativeKeyboardGaussianPair;
                    prediction = Relative;
                    break;
            }
        }
        public string[] Recognize(List<Point2D> pointList)
        {
            Console.WriteLine("Recoginize");
            PriorityQueue q = new PriorityQueue();
            foreach (KeyValuePair<string, double> k in languageModel)
            {
                string s = k.Key;
                if (s.Length != pointList.Count) continue;
                double p = k.Value;
                p *= prediction(s, pointList);
                q.Push(new Guess(s, p));
                if (q.Count > TOP_K) q.Pop();
            }
            List<string> candidates = new List<string>();
            candidates.Add(SimilarSequence(pointList));
            while (q.Count > 0)
            {
                candidates.Add(q.Pop().s);
            }
            candidates.Reverse();
            Ordering(candidates);
            return candidates.ToArray();
        }

        void LoadLanguageModel()
        {
            languageModel = new Dictionary<string, double>();
            StreamReader reader = new StreamReader("../../../Model/ANC-all-count.txt");
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                string[] lineArray = line.Split(' ');
                languageModel[lineArray[0]] = double.Parse(lineArray[1]);
                if (languageModel.Count > LANGUAGE_MODEL_SIZE) break;
            }
            Console.WriteLine("Load language model : " + languageModel.Count);
            reader.Close();
        }
        void LoadAbsoluteLetterModel()
        {
            absoluteLetterGaussianPair = new GaussianPair[26];
            StreamReader reader = new StreamReader("../../../Model/Absolute-General-Letter.txt");
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                string[] lineArray = line.Split('\t');
                char letter = lineArray[0][0];
                Gaussian xD = new Gaussian(double.Parse(lineArray[1]), double.Parse(lineArray[2]));
                Gaussian yD = new Gaussian(double.Parse(lineArray[3]), double.Parse(lineArray[4]));
                absoluteLetterGaussianPair[letter - 'a'] = new GaussianPair(xD, yD);
            }
            Console.WriteLine("Load absolute letter GaussianPair : " + absoluteLetterGaussianPair.Length);
            reader.Close();
        }
        void LoadAbsoluteKeyboardModel()
        {
            absoluteKeyboardGaussianPair = new GaussianPair[ALPHABET_SIZE];
            StreamReader reader = new StreamReader("../../../Model/Absolute-General-Keyboard.txt");
            string line = reader.ReadLine();
            string[] lineArray = line.Split('\t');
            double xk = double.Parse(lineArray[0]);
            double xb = double.Parse(lineArray[1]);
            double xstddev = double.Parse(lineArray[2]);
            double yk = double.Parse(lineArray[3]);
            double yb = double.Parse(lineArray[4]);
            double ystddev = double.Parse(lineArray[5]);
            for (int i = 0; i < ALPHABET_SIZE; ++i)
            {
                Point2D p = StandardPosition((char)(i + 'a'));
                absoluteKeyboardGaussianPair[i] = new GaussianPair(new Gaussian(p.x * xk + xb, xstddev), new Gaussian(p.y * yk + yb, ystddev));
            }
            Console.WriteLine("Load absolute keyboard GaussianPair : " + absoluteKeyboardGaussianPair.Length);
            reader.Close();
        }
        void LoadRelativeKeyboardModel()
        {
            relativeKeyboardGaussianPair = new GaussianPair[ALPHABET_SIZE, ALPHABET_SIZE];
            StreamReader reader = new StreamReader("../../../Model/Relative-General-Keyboard.txt");
            string line = reader.ReadLine();
            string[] lineArray = line.Split('\t');
            double xk = double.Parse(lineArray[0]);
            double xb = double.Parse(lineArray[1]);
            double xstddev = double.Parse(lineArray[2]);
            double yk = double.Parse(lineArray[3]);
            double yb = double.Parse(lineArray[4]);
            double ystddev = double.Parse(lineArray[5]);
            for (int i = 0; i < ALPHABET_SIZE; ++i)
                for (int j = 0; j < ALPHABET_SIZE; ++j)
                {
                    Point2D p = StandardPosition((char)(i + 'a')) - StandardPosition((char)(j + 'a'));
                    relativeKeyboardGaussianPair[i, j] = new GaussianPair(new Gaussian(p.x * xk + xb, xstddev), new Gaussian(p.y * yk + yb, ystddev));
                }
            Console.WriteLine("Load relative keyboard GaussianPair : " + relativeKeyboardGaussianPair.Length);
            reader.Close();
        }

        double Absolute(string s, List<Point2D> pointList)
        {
            double p = 1;
            for (int i = 0; i < pointList.Count; ++i)
            {
                p *= absoluteGaussianPair[s[i] - 'a'].Probability(pointList[i]);
            }
            return p;
        }
        double Relative(string s, List<Point2D> pointList)
        {
            double p = 1;
            p *= absoluteGaussianPair[s[0] - 'a'].Probability(pointList[0]);
            for (int i = 1; i < pointList.Count; ++i)
            {
                p *= relativeGaussianPair[s[i] - 'a', s[i - 1] - 'a'].Probability(pointList[i] - pointList[i - 1]);
            }
            return p;
        }

        Point2D StandardPosition(char c)
        {
            string[] standardKeyboard = new string[3] { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
            double[] xBias = new double[3] { 0, 0.25, 0.75 };
            for (int i = 0; i < 3; ++i)
                for (int j = 0; j < standardKeyboard[i].Length; ++j)
                    if (standardKeyboard[i][j] == c)
                    {
                        return new Point2D(xBias[i] + j, i);
                    }
            return new Point2D(-1, -1);
        }
        string SimilarSequence(List<Point2D> pointList)
        {
            string similarSequence = "";
            for (int i = 0; i < pointList.Count; ++i)
            {
                double bestP = 0;
                char c = ' ';
                for (int j = 0; j < ALPHABET_SIZE; ++j)
                {
                    double p = absoluteGaussianPair[j].Probability(pointList[i]);
                    if (p > bestP)
                    {
                        bestP = p;
                        c = (char)('a' + j);
                    }
                }
                similarSequence += c;
            }
            return similarSequence;
        } 
        void Ordering(List<string> a)
        {
            int lastIndex = (a.Count > TOP_K) ? a.Count - 1 : a.Count;
            if (lastIndex > 5)
            {
                a.Sort(5, lastIndex - 5, Comparer<string>.Default);
            }
        }
    }
    
    public class Point2D
    {
        public double x, y;
        public Point2D(double x2, double y2)
        {
            x = x2;
            y = y2;
        }
        public static Point2D operator - (Point2D a, Point2D b)
        {
            return new Point2D(a.x - b.x, a.y - b.y);
        }
    }

    class Gaussian
    {
        public double mu;
        public double sigma;
        private double k0;
        private double k1;

        public Gaussian(double mu2, double sigma2)
        {
            mu = mu2;
            sigma = sigma2;
            k0 = 1.0 / Math.Sqrt(2 * Math.Acos(-1)) / sigma;
            k1 = -1.0 / 2 / Math.Pow(sigma, 2);
        }

        public double Probability(double x)
        {
            return k0 * Math.Exp(Math.Pow(x - mu, 2) * k1);
        }
    }

    class GaussianPair
    {
        public Gaussian xD;
        public Gaussian yD;

        public GaussianPair(Gaussian xD2, Gaussian yD2)
        {
            xD = xD2;
            yD = yD2;
        }

        public double Probability(Point2D p)
        {
            return xD.Probability(p.x) * yD.Probability(p.y);
        }
    }

    class Guess
    {
        public string s;
        public double p;

        public Guess(string s2, double p2)
        {
            s = s2;
            p = p2;
        }
    }

    class PriorityQueue
    {
        Guess[] heap;

        public int Count = 0;

        public PriorityQueue() : this(1) { }

        public PriorityQueue(int capacity)
        {
            heap = new Guess[capacity];
        }

        public void Push(Guess v)
        {
            if (Count >= heap.Length) Array.Resize(ref heap, Count * 2);
            heap[Count] = v;
            SiftUp(Count++);
        }
        
        public Guess Pop()
        {
            Guess v = Top();
            heap[0] = heap[--Count];
            if (Count > 0) SiftDown(0);
            return v;
        }
        
        public Guess Top()
        {
            if (Count > 0) return heap[0];
            throw new InvalidOperationException("Empty priority queue!");
        }
        
        void SiftUp(int n)
        {
            Guess v = heap[n];
            for (var n2 = n / 2; n > 0; n = n2, n2 /= 2)
            {
                if (heap[n2].p <= v.p) break;
                heap[n] = heap[n2];
            }
            heap[n] = v;
        }
        
        void SiftDown(int n)
        {
            Guess v = heap[n];
            for (var n2 = n * 2; n2 < Count; n = n2, n2 *= 2)
            {
                if (n2 + 1 < Count && heap[n2 + 1].p < heap[n2].p) n2++;
                if (v.p <= heap[n2].p) break;
                heap[n] = heap[n2];
            }
            heap[n] = v;
        }
    }
}
