using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShowServer
{
    public partial class MainWindow : Window
    {
        enum PointVisible    { Flash, Visible, Unvisible };
        enum KeyboardOnOff { KeyboardOn, KeyboardOff };

        Server server;
        string testerName;
        PointVisible pointVisible = PointVisible.Flash;
        KeyboardOnOff keyboardOnOff = KeyboardOnOff.KeyboardOn;

        public List<String> sampleList = new List<String>();
        public int sampleListIndex = 0;

        const int DRAG_ROW = 5;
        const int DRAG_COLUMN = 5;
        const int DRAG_SPAN_X = 50;
        const int DRAG_SPAN_Y = 60;
        const double DRAG_SMOOTH = 1.0;
        bool draging = false;
        int dragStartX, dragStartY;
        int selectX, selectY, selectIndex;

        List<UltraPoint> pointList = new List<UltraPoint>();
        List<String> wordList = new List<String>();
        Recognition recongition = new Recognition();
        string[] candidates;

        public MainWindow()
        {
            InitializeComponent();
            Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/background.png", UriKind.Relative)));
            xTextEntryCanvas.Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/text-entry.png", UriKind.Relative)));
            AddKeyboardUi();
            LoadSample();
        }

        public void Click(int x, int y, DateTime t)
        {
            OperationWrite("add " + x + " " + y + " " + t.ToFileTime());
            pointList.Add(new UltraPoint(x, y, t));
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
        public int LeftSlip(bool userEvent)
        {
            if (pointList.Count == 0 && wordList.Count != 0) wordList.RemoveAt(wordList.Count - 1);
            if (pointList.Count > 0)
            {
                //  -------------------- animation --------------------
                UnregisterName("i" + pointList.Count);
                xPointCanvas.Children.RemoveAt(xPointCanvas.Children.Count - 1);
                //  -------------------- animation --------------------
                pointList.RemoveAt(pointList.Count - 1);
            }
            if (userEvent)
            {
                OperationWrite("backspace");
                UpdateTextEntry();
            }
            return pointList.Count;
        }
        public void RightSlip()
        {
            string[] wordArray = sampleList[sampleListIndex].Split(' ');
            if (pointList.Count == 0 && wordList.Count == wordArray.Length) NextSentence();
            if (pointList.Count != 0) NextWord();
        }
        public void DragBegin(int x, int y)
        {
            if (pointList.Count > 0)
            {
                dragStartX = x;
                dragStartY = y;
                draging = true;
                xKeyboardCanvas.Visibility = Visibility.Hidden;
            }
            Drag(x, y);
        }       
        public void Drag(int x, int y)
        {
            double addition = DRAG_SMOOTH - 0.5;
            double selectX2 = 1.0 * (x - dragStartX) / DRAG_SPAN_X;
            double selectY2 = 1.0 * (y - dragStartY) / DRAG_SPAN_Y;
            selectX2 = Math.Min(Math.Max(selectX2, -addition), DRAG_COLUMN + addition);
            selectY2 = Math.Min(Math.Max(selectY2, -addition), DRAG_ROW + addition);

            if (Math.Abs(selectX2 - (selectX + 0.5)) > DRAG_SMOOTH)
            {
                selectX = (x - dragStartX) / DRAG_SPAN_X;
                selectX = Math.Min(Math.Max(selectX, 0), DRAG_COLUMN - 1);
            }
            if (Math.Abs(selectY2 - (selectY + 0.5)) > DRAG_SMOOTH)
            {
                selectY = (y - dragStartY) / DRAG_SPAN_Y;
                selectY = Math.Min(Math.Max(selectY, 0), DRAG_ROW - 1);
            }
            selectIndex = selectY * DRAG_COLUMN + selectX;
            selectIndex = Math.Min(selectIndex, candidates.Length - 1);
            UpdateTextEntry();
        }
        public void DragEnd(int x, int y)
        {
            Drag(x, y);
            draging = false;
            xDragCanvas.Children.Clear();
            xKeyboardCanvas.Visibility = (keyboardOnOff == KeyboardOnOff.KeyboardOn) ? Visibility.Visible : Visibility.Hidden;
            NextWord();
        }
        
        void UpdateTextEntry()
        {
            xInputTextBox.Text = sampleListIndex.ToString() + ": ";
            foreach (string word in wordList) xInputTextBox.Text += word + " ";
            if (pointList.Count > 0)
            {
                if (!draging) candidates = recongition.Recognize(pointList);
                xInputTextBox.Text += candidates[selectIndex].Substring(0, pointList.Count);
            }
            xInputTextBox.SelectionStart = xInputTextBox.Text.Length;
            
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
                        //label.Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/select.png", UriKind.Relative)));
                        label.Background = new SolidColorBrush(Color.FromRgb(2, 91, 195));
                    }
                    label.Foreground = new SolidColorBrush(Color.FromRgb(187, 187, 187));
                    label.FontSize = 20;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    label.VerticalContentAlignment = VerticalAlignment.Center;
                    label.Content = id < candidates.Length ? candidates[id] : " ";
                    Canvas.SetTop(label, 12 + i * 55 + (i == 0 ? 0 : 6));
                    Canvas.SetLeft(label, 90 + j * 210);
                    xDragCanvas.Children.Add(label);
                }
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
                    rectangle.Height = keySize;
                    rectangle.Width = keySize;
                    rectangle.Stroke = new SolidColorBrush(Color.FromRgb(104, 104, 104));
                    Canvas.SetTop(rectangle, hOffset + r * keySize2);
                    Canvas.SetLeft(rectangle, wOffset[r] + c * keySize2);
                    xKeyboardCanvas.Children.Add(rectangle);

                    Label label = new Label();
                    label.FontSize = fontSize;
                    label.Foreground = new SolidColorBrush(Color.FromRgb(104, 104, 104));
                    label.Height = keySize;
                    label.Width = keySize;
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
            StreamReader reader = new StreamReader(new FileStream("../../../PhraseSets/phrases2.txt", FileMode.Open));
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                line = line.ToLower();
                sampleList.Add(line);
            }
            reader.Close();
            sampleListIndex = sampleList.Count - 1;
            NextSentence();
        }
        void NextWord()
        {
            if (pointList.Count > 0)
            {
                wordList.Add(candidates[selectIndex]);
                ClearPoints();
            }
            UpdateTextEntry();
        }
        void NextSentence()
        {
            wordList.Clear();
            sampleListIndex = (sampleListIndex + 1) % sampleList.Count();
            xNoticeTextBlock.Text = "" + sampleListIndex.ToString() + ": " + sampleList[sampleListIndex];
            UpdateTextEntry();
        }
        void ClearPoints()
        {
            while (pointList.Count() > 0)
            {
                LeftSlip(false);
            }
            selectX = selectY = selectIndex = 0;
        }
        void OperationWrite(string operation)
        {
            StreamWriter writer = new StreamWriter(new FileStream("../../../Result/operation-" + testerName + ".txt", FileMode.Append));
            writer.WriteLine(operation);
            writer.Close();
        }
      
        private void xSettingButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility visibility = (xIPTextBox.Visibility == Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            xIPTextBox.Visibility = visibility;
            xTesterTextBox.Visibility = visibility;
            xSetupButton.Visibility = visibility;
            xTesterButton.Visibility = visibility;
            xPointVisibleButton.Visibility = visibility;
            xKeyboardButton.Visibility = visibility;
            xSampleChangeButton.Visibility = visibility;
            xRestartButton.Visibility = visibility;
        }
        private void xSetupButton_Click(object sender, RoutedEventArgs e)
        {
            if (server != null) return;
            server = new Server(xIPTextBox.Text);
            server.Listen(this);
            MessageBox.Show("Server setup!");
            xInputTextBox.Focus();
        }
        private void xTesterButton_Click(object sender, RoutedEventArgs e)
        {
            testerName = xTesterTextBox.Text;
            MessageBox.Show("Tester's name: " + testerName);
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
        private void xSampleChangeButton_Click(object sender, RoutedEventArgs e)
        {
            NextSentence();
        }
        private void xRestartButton_Click(object sender, RoutedEventArgs e)
        {
            sampleListIndex = sampleList.Count - 1;
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
                    mainWindow.Click(int.Parse(strs[1]), int.Parse(strs[2]), DateTime.Now);
                    break;
                case "leftslip":
                    mainWindow.LeftSlip(true);
                    break;
                case "rightslip":
                    mainWindow.RightSlip();
                    break;
                default:
                    break;
            }
        }
    }

    public class UltraPoint
    {
        public int x, y;
        public DateTime t;
        public Ellipse ellipse;
        public Label label;
        public UltraPoint(int x2, int y2, DateTime t2)
        {
            x = x2;
            y = y2;
            t = t2;
        }
    }
}