using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;


namespace ShowServer
{
    class Recognition
    {
        delegate double Prediction(string guess, List<UltraPoint> pointList);

        const int ALPHABET_SIZE = 26;
        const int LANGUAGE_MODEL_SIZE = 50000;
        const int TOP_K = 25;
        
        public Dictionary<string, double> languageModel;
        public GDPair[] absoluteGDPair;
        public GDPair[,] relativeGDPair;

        public GDPair[] absoluteLetterGDPair;
        public GDPair[] absoluteKeyboardGDPair;
        Prediction prediction;

        public Recognition()
        {
            LoadLanguageModel();
            LoadAbsoluteLetterModel();
            ChangeMode("Absolute-Letter");
        }
        public void ChangeMode(string mode)
        {
            switch (mode)
            {
                case "Absolute-Letter":
                    absoluteGDPair = absoluteLetterGDPair;
                    prediction = Absolute;
                    break;
                case "Absolute-Keyboard":
                    break;
                case "Relative-Keyboard":
                    break;
            }
        }
        public string[] Recognize(List<UltraPoint> pointList)
        {
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
            string[] candidates = new string[q.Count];
            for (int i = q.Count - 1; i >= 0; --i)
            {
                candidates[i] = q.Pop().s;
            }
            return candidates;
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
            absoluteLetterGDPair = new GDPair[26];
            StreamReader reader = new StreamReader("../../../Model/Absolute-General-Letter.txt");
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                string[] lineArray = line.Split('\t');
                char letter = lineArray[0][0];
                GuassianD xD = new GuassianD(double.Parse(lineArray[1]), double.Parse(lineArray[2]));
                GuassianD yD = new GuassianD(double.Parse(lineArray[3]), double.Parse(lineArray[4]));
                absoluteLetterGDPair[letter - 'a'] = new GDPair(xD, yD);
            }
            Console.WriteLine("Load absolute letter keyGDPair : " + absoluteLetterGDPair.Length);
            reader.Close();
        }
        void LoadAbsoluteKeyboardModel()
        {
            absoluteKeyboardGDPair = new GDPair[ALPHABET_SIZE];
        }

        double Absolute(string s, List<UltraPoint> pointList)
        {
            double p = 1;
            for (int i = 0; i < pointList.Count; ++i)
            {
                p *= absoluteGDPair[s[i] - 'a'].Probability(pointList[i].x, pointList[i].y);
            }
            return p;
        }
    }
    
    class GuassianD
    {
        public double mu;
        public double sigma;
        private double k0;
        private double k1;

        public GuassianD(double mu2, double sigma2)
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

    class GDPair
    {
        public GuassianD xD;
        public GuassianD yD;

        public GDPair(GuassianD xD2, GuassianD yD2)
        {
            xD = xD2;
            yD = yD2;
        }

        public double Probability(double x, double y)
        {
            return xD.Probability(x) * yD.Probability(y);
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
