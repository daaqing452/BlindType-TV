using System;
using System.Collections.Generic;
using System.IO;


namespace ShowServer
{
    class Recognition
    {
        const int ALPHABET_SIZE = 26;

        public Dictionary<string, double> languageModel;
        public Keycloud[] absoluteKeycloud;
        public Keycloud[,] relativeKeycloud;

        public Keycloud[] absoluteLetterKeycloud;

        public Recognition()
        {
            LoadLanguageModel();
            LoadAbsoluteLetterModel();
        }
        
        public void LoadLanguageModel()
        {
            languageModel = new Dictionary<string, double>();
            StreamReader reader = new StreamReader("Model/ANC-all-count.txt");
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                string[] lineArray = line.Split(' ');
                languageModel[lineArray[0]] = double.Parse(lineArray[1]);
            }
            Console.WriteLine("Load language model : " + languageModel.Count);
            reader.Close();
        }

        public void LoadAbsoluteLetterModel()
        {
            absoluteLetterKeycloud = new Keycloud[26];
            StreamReader reader = new StreamReader("Model/Absolute-General-Letter.txt");
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                string[] lineArray = line.Split('\t');
                char letter = lineArray[0][0];
                GuassianD xD = new GuassianD(double.Parse(lineArray[1]), double.Parse(lineArray[2]));
                GuassianD yD = new GuassianD(double.Parse(lineArray[3]), double.Parse(lineArray[4]));
                absoluteLetterKeycloud[letter - 'a'] = new Keycloud(xD, yD);
            }
            Console.WriteLine("Load absolute letter keycloud : " + absoluteLetterKeycloud.Length);
            reader.Close();
        }

        public void ChangeMode(string mode)
        {
            if (mode == "Absolute-Letter")
            {
                absoluteKeycloud = absoluteLetterKeycloud;
            }
        }

        public List<string> recognize(List<UltraPoint> pointList)
        {
            return null;
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

    class Keycloud
    {
        public GuassianD xD;
        public GuassianD yD;

        public Keycloud(GuassianD xD2, GuassianD yD2)
        {
            xD = xD2;
            yD = yD2;
        }

        public double Probability(double x, double y)
        {
            return xD.Probability(x) * yD.Probability(y);
        }
    }
}
