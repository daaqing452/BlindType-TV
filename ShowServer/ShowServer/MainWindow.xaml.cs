using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShowServer
{
    enum PointVisible { Flash, Visible, Unvisible };
    enum KeyboardOnOff { KeyboardOn, KeyboardOff };
    enum Algorithm { AGL, AGK, RGK };
    enum SampleFile { Normal, Confuse };

    public partial class MainWindow : Window
    {
        PointVisible pointVisible = PointVisible.Flash;
        KeyboardOnOff keyboardOnOff = KeyboardOnOff.KeyboardOn;
        Algorithm algorithm = Algorithm.AGK;
        SampleFile sampleFile = SampleFile.Normal;
        string userName = "";
        public int deviceWidth, deviceHeight;

        List<string> sampleList;
        List<string> sampleNormalList = new List<string>();
        List<string> sampleConfuseList = new List<string>();
        int sampleListIndex = 0;
        bool emptySentense = true;

        const int DRAG_ROW = 5;
        const int DRAG_COLUMN = 5;
        const double DRAG_SMOOTH = 1.0;
        bool draging = false;
        int dragStartX, dragStartY;
        int dragSpanX, dragSpanY;
        int selectX, selectY, selectIndex;

        List<Point2D> pointList = new List<Point2D>();
        List<string> wordList = new List<string>();
        Recognition recongition = new Recognition();
        string[] candidates;

        public MainWindow()
        {
            InitializeComponent();
            Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/background.png", UriKind.Relative)));
            xTextEntryCanvas.Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/text-entry.png", UriKind.Relative)));
            AddKeyboardUi();
            LoadSample();
            SetupServer();
        }

        public void Click(int x, int y)
        {
            if (emptySentense)
            {
                OperationWrite("sentence " + sampleList[sampleListIndex]);
                emptySentense = false;
            }
            OperationWrite("click " + x + " " + y);
            pointList.Add(new Point2D(x, y));
            UpdateTextEntry();

            //  -------------------- animation --------------------
            Image image = new Image();
            image.Source = (ImageSource)new ImageSourceConverter().ConvertFromString("../../../Image/point.png");
            image.Name = "i" + pointList.Count;
            if (pointVisible == PointVisible.Unvisible) image.Opacity = 0.0;
            Canvas.SetLeft(image, x / 1.2 + 300);
            Canvas.SetTop(image, (y - 510) / 2.8);
            xPointCanvas.Children.Add(image);
            RegisterName(image.Name, image);
            DoubleAnimation doubleAnimationImage = new DoubleAnimation();
            doubleAnimationImage.From = 1.0;
            doubleAnimationImage.To = 0.0;
            doubleAnimationImage.Duration = new Duration(TimeSpan.FromSeconds(0.5));
            Storyboard storyboard = new Storyboard();
            Storyboard.SetTargetName(doubleAnimationImage, image.Name);
            Storyboard.SetTargetProperty(doubleAnimationImage, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(doubleAnimationImage);
            if (pointVisible == PointVisible.Flash) storyboard.Begin(this);
            //  -------------------- animation --------------------
        }
        public void LeftSlip()
        {
            OperationWrite("leftslip");
            if (pointList.Count == 0 && wordList.Count != 0) wordList.RemoveAt(wordList.Count - 1);
            if (pointList.Count > 0) ErasePoint();
            UpdateTextEntry();
        }
        public void RightSlip()
        {
            string[] wordArray = sampleList[sampleListIndex].Split(' ');
            if (pointList.Count == 0 && wordList.Count == wordArray.Length)
            {
                NextSentence();
            }
            if (pointList.Count > 0)
            {
                OperationWrite("rightslip");
                NextWord();
            }
        }
        public void DownSlip()
        {
            OperationWrite("downslip");
            ClearPoints();
            UpdateTextEntry();
        }
        public void DragBegin(int x, int y)
        {
            if (pointList.Count == 0) return;
            OperationWrite("dragbegin");
            dragStartX = x;
            dragStartY = y;
            dragSpanX = Math.Min(Math.Max((deviceWidth - x - 40) / DRAG_COLUMN, 10), 80);
            dragSpanY = Math.Min(Math.Max((deviceHeight - y - 80) / DRAG_ROW, 10), 80);
            Console.WriteLine("drag span: " + dragSpanX + " " + dragSpanY);
            draging = true;
            xKeyboardCanvas.Visibility = Visibility.Hidden;
        }
        public void Drag(int x, int y)
        {
            if (pointList.Count == 0) return;
            double addition = DRAG_SMOOTH - 0.5;
            double selectX2 = 1.0 * (x - dragStartX) / dragSpanX;
            double selectY2 = 1.0 * (y - dragStartY) / dragSpanY;
            selectX2 = Math.Min(Math.Max(selectX2, -addition), DRAG_COLUMN + addition);
            selectY2 = Math.Min(Math.Max(selectY2, -addition), DRAG_ROW + addition);
            if (Math.Abs(selectX2 - (selectX + 0.5)) > DRAG_SMOOTH)
            {
                selectX = (x - dragStartX) / dragSpanX;
                selectX = Math.Min(Math.Max(selectX, 0), DRAG_COLUMN - 1);
            }
            if (Math.Abs(selectY2 - (selectY + 0.5)) > DRAG_SMOOTH)
            {
                selectY = (y - dragStartY) / dragSpanY;
                selectY = Math.Min(Math.Max(selectY, 0), DRAG_ROW - 1);
            }
            selectIndex = selectY * DRAG_COLUMN + selectX;
            selectIndex = Math.Min(selectIndex, candidates.Length - 1);
            UpdateTextEntry();
        }
        public void DragEnd(int x, int y)
        {
            if (pointList.Count == 0) return;
            Drag(x, y);
            OperationWrite("dragend");
            draging = false;
            xDragCanvas.Children.Clear();
            xKeyboardCanvas.Visibility = (keyboardOnOff == KeyboardOnOff.KeyboardOn) ? Visibility.Visible : Visibility.Hidden;
            NextWord();
        }
        
        void UpdateTextEntry()
        {
            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(sampleListIndex.ToString() + ": "));
            foreach (string word in wordList) paragraph.Inlines.Add(new Run(word + " "));
            if (pointList.Count > 0)
            {
                if (!draging) candidates = recongition.Recognize(pointList);
                paragraph.Inlines.Add(new Underline(new Run(candidates[selectIndex].Substring(0, pointList.Count))));
            }
            FlowDocument flowDocument = new FlowDocument();
            flowDocument.Blocks.Add(paragraph);
            xInputRichTextBox.Document = flowDocument;
            xInputRichTextBox.CaretPosition = xInputRichTextBox.Document.Blocks.LastBlock.ContentEnd;

            xDragCanvas.Children.Clear();
            for (int i = 0; i < DRAG_ROW; ++i)
                for (int j = 0; j < DRAG_COLUMN; ++j)
                {
                    if (pointList.Count == 0) continue;
                    if (draging == false && i > 0) continue;
                    int id = i * DRAG_COLUMN + j;
                    Label label = new Label();
                    label.Width = 200;
                    label.Height = 50;
                    if (i * DRAG_COLUMN + j == selectIndex)
                    {
                        label.Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/select.png", UriKind.Relative)));
                        //label.Background = new SolidColorBrush(Color.FromRgb(2, 91, 195));
                    }
                    label.Foreground = new SolidColorBrush(Color.FromRgb(187, 187, 187));
                    label.FontSize = 23;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    label.VerticalContentAlignment = VerticalAlignment.Center;
                    label.Content = id < candidates.Length ? candidates[id] : " ";
                    Canvas.SetTop(label, 12 + i * 55 + (i == 0 ? 0 : 6));
                    Canvas.SetLeft(label, 90 + j * 210);
                    xDragCanvas.Children.Add(label);
                }
        }
        void ErasePoint()
        {
            if (pointList.Count > 0)
            {
                //  -------------------- animation --------------------
                UnregisterName("i" + pointList.Count);
                xPointCanvas.Children.RemoveAt(xPointCanvas.Children.Count - 1);
                //  -------------------- animation --------------------
                pointList.RemoveAt(pointList.Count - 1);
            }
        }
        void ClearPoints()
        {
            while (pointList.Count() > 0)
            {
                ErasePoint();
            }
            selectX = selectY = selectIndex = 0;
        }
        void NextWord()
        {
            if (pointList.Count == 0) return;
            string[] sampleArray = sampleList[sampleListIndex].Split(' ');
            string requireWord = (wordList.Count < sampleArray.Length) ? sampleArray[wordList.Count] : "";
            OperationWrite("select " + candidates[selectIndex] + " " + selectIndex + " " + candidates.Contains(requireWord));
            wordList.Add(candidates[selectIndex]);
            ClearPoints();
            UpdateTextEntry();
        }
        void NextSentence()
        {
            ClearPoints();
            wordList.Clear();
            sampleListIndex = (sampleListIndex + 1) % sampleList.Count();
            xNoticeTextBlock.Text = "" + sampleListIndex.ToString() + ": " + sampleList[sampleListIndex];
            emptySentense = true;
            UpdateTextEntry();
        }
        void AddKeyboardUi()
        {
            int keySize = 68;
            int keySize2 = keySize - 1;
            int fontSize = keySize / 2 + 1;
            String[] keyLayout = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            int[] wOffset = { 300, 300 + keySize / 4, 300 + keySize * 3 / 4 };
            int hOffset = 85;

            for (int r = 0; r < keyLayout.Count(); ++r)
            {
                for (int c = 0; c < keyLayout[r].Count(); ++c)
                {
                    Rectangle rectangle = new Rectangle();
                    rectangle.Width = keySize;
                    rectangle.Height = keySize;
                    rectangle.Stroke = new SolidColorBrush(Color.FromRgb(104, 104, 104));
                    Canvas.SetTop(rectangle, hOffset + r * keySize2);
                    Canvas.SetLeft(rectangle, wOffset[r] + c * keySize2);
                    xKeyboardCanvas.Children.Add(rectangle);

                    Label label = new Label();
                    label.FontSize = fontSize;
                    label.Foreground = new SolidColorBrush(Color.FromRgb(104, 104, 104));
                    label.Width = keySize;
                    label.Height = keySize;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    label.VerticalContentAlignment = VerticalAlignment.Center;
                    label.Content = keyLayout[r][c];
                    Canvas.SetTop(label, hOffset + r * keySize2);
                    Canvas.SetLeft(label, wOffset[r] + c * keySize2);
                    xKeyboardCanvas.Children.Add(label);
                }
            }
        }
        void LoadSample()
        {
            StreamReader reader;
            reader = new StreamReader(new FileStream("../../../PhraseSets/phrases-normal.txt", FileMode.Open));
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                line = line.ToLower();
                sampleNormalList.Add(line);
            }
            reader.Close();

            reader = new StreamReader(new FileStream("../../../PhraseSets/phrases-confuse.txt", FileMode.Open));
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                line = line.ToLower();
                sampleConfuseList.Add(line);
            }
            reader.Close();

            sampleList = sampleNormalList;
            sampleListIndex = sampleList.Count - 1;
            NextSentence();
        }
        void OperationWrite(string operation)
        {
            string fileName = "../../../Result/op-" + userName + "-" + sampleFile + "-" + algorithm + ".txt";
            StreamWriter writer = new StreamWriter(new FileStream(fileName, FileMode.Append));
            long nowTime = DateTime.Now.ToFileTimeUtc() / 10000 % 100000000;
            writer.WriteLine(nowTime + " " + operation);
            writer.Close();
        }
        void SetupServer()
        {
            string hostName = Dns.GetHostName();
            IPAddress[] addressList = Dns.GetHostAddresses(hostName);
            string localIP = "127.0.0.1";
            foreach (IPAddress ip in addressList)
            {
                if (ip.ToString().IndexOf("192.168.173") != -1) localIP = ip.ToString();
                Console.WriteLine(ip);
            }
            Server server = new Server(localIP);
            server.Listen(this);
            MessageBox.Show("Server setup " + localIP);
            xInputRichTextBox.Focus();
        }

        private void xSettingButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility visibility = (xPointVisibleButton.Visibility == Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            xTesterTextBox.Visibility = visibility;
            xTesterButton.Visibility = visibility;
            xPointVisibleButton.Visibility = visibility;
            xKeyboardButton.Visibility = visibility;
            xAlgorithmButton.Visibility = visibility;
            xSampleFileButton.Visibility = visibility;
        }
        private void xTesterButton_Click(object sender, RoutedEventArgs e)
        {
            userName = xTesterTextBox.Text;
            MessageBox.Show("Tester: " + userName);
            xInputRichTextBox.Focus();
        }
        private void xAlgorithmButton_Click(object sender, RoutedEventArgs e)
        {
            switch (algorithm)
            {
                case Algorithm.AGL:
                    algorithm = Algorithm.AGK;
                    break;
                case Algorithm.AGK:
                    algorithm = Algorithm.RGK;
                    break;
                case Algorithm.RGK:
                    algorithm = Algorithm.AGK;
                    break;
            }
            recongition.ChangeMode(algorithm);
            xAlgorithmButton.Content = algorithm;
        }
        private void xPointVisibleButton_Click(object sender, RoutedEventArgs e)
        {
            switch (pointVisible)
            {
                case PointVisible.Visible:
                    pointVisible = PointVisible.Flash;
                    break;
                case PointVisible.Flash:
                    pointVisible = PointVisible.Unvisible;
                    break;
                case PointVisible.Unvisible:
                    pointVisible = PointVisible.Visible;
                    break;
            }
            xPointVisibleButton.Content = pointVisible;
            foreach (UIElement uiElement in xPointCanvas.Children)
            {
                uiElement.BeginAnimation(Ellipse.OpacityProperty, null);
                uiElement.Opacity = (pointVisible == PointVisible.Visible) ? 1.0 : 0.0;
            }
        }
        private void xKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            switch (keyboardOnOff)
            {
                case KeyboardOnOff.KeyboardOn:
                    keyboardOnOff = KeyboardOnOff.KeyboardOff;
                    break;
                case KeyboardOnOff.KeyboardOff:
                    keyboardOnOff = KeyboardOnOff.KeyboardOn;
                    break;
            }
            xKeyboardButton.Content = keyboardOnOff;
            xKeyboardCanvas.Visibility = (keyboardOnOff == KeyboardOnOff.KeyboardOn) ? Visibility.Visible : Visibility.Hidden;
        }
        private void xSampleFileButton_Click(object sender, RoutedEventArgs e)
        {
            switch (sampleFile)
            {
                case SampleFile.Normal:
                    sampleFile = SampleFile.Confuse;
                    sampleList = sampleConfuseList;
                    break;
                case SampleFile.Confuse:
                    sampleFile = SampleFile.Normal;
                    sampleList = sampleNormalList;
                    break;
            }
            xSampleFileButton.Content = sampleFile;
            sampleListIndex = sampleList.Count - 1;
            //sampleListIndex = 12;
            NextSentence();
        }
    }

    public class Server : DispatcherObject
    {
        public MainWindow mainWindow;
        public TcpListener tcpListener;
        public Thread listenThread;
        public string ip;
        public Server(string ip2)
        {
            ip = ip2;
        }
        public void Listen(MainWindow mainWindow2)
        {
            mainWindow = mainWindow2;
            tcpListener = new TcpListener(IPAddress.Parse(ip), 10309);
            listenThread = new Thread(ListenClient);
            listenThread.IsBackground = true;
            listenThread.Start();
        }
        public void ListenClient()
        {
            tcpListener.Start();
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                MessageBox.Show("Join!");
                //mainWindow.xInputRichTextBox.Focus();
                Thread receiveThread = new Thread(Receive);
                receiveThread.IsBackground = true;
                receiveThread.Start(client);
            }
        }
        public void Receive(object clientObject)
        {
            TcpClient client = (TcpClient)clientObject;
            StreamReader sr = new StreamReader(client.GetStream());
            while (true)
            {
                string str = sr.ReadLine();
                if (str == null) break;
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new BeginInvokeDelegate(BeginInvokeMethod), str);
            }
        }
        public delegate void BeginInvokeDelegate(string str);
        public void BeginInvokeMethod(string str)
        {
            Console.WriteLine(str);
            String[] strs = str.Split(' ');
            switch (strs[0])
            {
                case "devicesize":
                    mainWindow.deviceWidth = int.Parse(strs[1]);
                    mainWindow.deviceHeight = int.Parse(strs[2]);
                    break;
                case "dragbegin":
                    mainWindow.DragBegin(int.Parse(strs[1]), int.Parse(strs[2]));
                    break;
                case "drag":
                    mainWindow.Drag(int.Parse(strs[1]), int.Parse(strs[2]));
                    break;
                case "dragend":
                    mainWindow.DragEnd(int.Parse(strs[1]), int.Parse(strs[2]));
                    break;
                case "click":
                    mainWindow.Click(int.Parse(strs[1]), int.Parse(strs[2]));
                    break;
                case "leftslip":
                    mainWindow.LeftSlip();
                    break;
                case "rightslip":
                    mainWindow.RightSlip();
                    break;
                case "downslip":
                    mainWindow.DownSlip();
                    break;
                default:
                    break;
            }
        }
    }
}