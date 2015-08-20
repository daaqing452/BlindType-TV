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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShowServer
{
    public partial class MainWindow : Window
    {
        public enum PointVisible    { Visible, Flash, Unvisible };
        public enum KeyboardVisible { KeyboardOn, KeyboardOff };

        public Server server;
        public string testerName;
        public PointVisible     pointVisible    = PointVisible.Visible;
        public KeyboardVisible  keyboardVisible = KeyboardVisible.KeyboardOn;
        
        public int deviceHeight;
        public int deviceWidth;
        public int deviceYBias;
        public double deviceResize = 1;
        public int deviceDragLen = 150;

        public List<UltraPoint> pointList = new List<UltraPoint>();
        public List<String> wordList = new List<String>();
        
        public List<String> noticeList = new List<String>();
        string noticeNow;
        int noticeListIndex = -1;

        public Point dragStart;
        public Label dragFocusLabel;

        Recognition recongition = new Recognition();

        public MainWindow()
        {
            InitializeComponent();
            AddKeyboardUi();
            NoticeLoad();
            NoticeChange();
        }


        public void Click(int x, int y, DateTime t)
        {
            operationWrite("add " + x + " " + y + " " + t.ToFileTime());
            pointList.Add(new UltraPoint(x, y, t));
            y += deviceYBias;

            //  -------------------- animation --------------------
            Ellipse ellipse = new Ellipse();
            ellipse.Name = "e" + pointList.Count;
            ellipse.Width = 6;
            ellipse.Height = 6;
            ellipse.Stroke = Brushes.Indigo;
            ellipse.Fill = Brushes.Blue;
            if (pointVisible == PointVisible.Unvisible) ellipse.Opacity = 0.0;
            Canvas.SetLeft(ellipse, x / deviceResize - 3);
            Canvas.SetTop(ellipse, y / deviceResize - 3);
            xDrawCanvas.Children.Add(ellipse);

            Label label = new Label();
            label.Name = "l" + pointList.Count;
            label.Content = pointList.Count.ToString();
            if (pointVisible == PointVisible.Unvisible) label.Opacity = 0.0;
            Canvas.SetLeft(label, x / deviceResize - 3);
            Canvas.SetTop(label, y / deviceResize - 5);
            xDrawCanvas.Children.Add(label);

            RegisterName(ellipse.Name, ellipse);
            RegisterName(label.Name, label);
            DoubleAnimation doubleAnimationEllipse = new DoubleAnimation();
            DoubleAnimation doubleAnimationLabel = new DoubleAnimation();
            doubleAnimationEllipse.From = doubleAnimationLabel.From = 1.0;
            doubleAnimationEllipse.To = doubleAnimationLabel.To = 0.0;
            doubleAnimationEllipse.Duration = doubleAnimationLabel.Duration = new Duration(TimeSpan.FromSeconds(0.5));
            Storyboard storyboard = new Storyboard();
            Storyboard.SetTargetName(doubleAnimationEllipse, ellipse.Name);
            Storyboard.SetTargetProperty(doubleAnimationEllipse, new PropertyPath(Ellipse.OpacityProperty));
            Storyboard.SetTargetName(doubleAnimationLabel, label.Name);
            Storyboard.SetTargetProperty(doubleAnimationLabel, new PropertyPath(Label.OpacityProperty));
            storyboard.Children.Add(doubleAnimationEllipse);
            storyboard.Children.Add(doubleAnimationLabel);
            if (pointVisible == PointVisible.Flash) storyboard.Begin(this);
            //  -------------------- animation --------------------

            InputedRefresh();
        }

        public int LeftSlip(bool userEvent)
        {
            if (userEvent)
            {
                operationWrite("backspace");
            }
            if (pointList.Count == 0) return 0;

            //  -------------------- animation --------------------
            UnregisterName("e" + pointList.Count);
            UnregisterName("l" + pointList.Count);
            pointList.RemoveAt(pointList.Count - 1);
            xDrawCanvas.Children.RemoveRange(xDrawCanvas.Children.Count - 2, 2);
            //  -------------------- animation --------------------

            InputedRefresh();
            return pointList.Count;
        }

        public void DragBegin(int x, int y)
        {
            int appearIndex = (new Random()).Next() % 9 + 3;
            dragStart = new Point(x, y);
            for (int i = 0; i < 5; ++i)
                for (int j = 0; j < 3; ++j)
                {
                    Label label = new Label();
                    label.Width = 125;
                    label.Height = 55;
                    label.FontSize = 30;
                    label.Content = "";
                    /**/
                    int[] nia = { 11, 9, 12, 5, 1, 6, 3, 0, 4, 7, 2, 8, 13, 10, 14 };
                    int ni = nia[i * 3 + j];
                    label.Content = i + j;
                    /**/
                    Canvas.SetTop(label, 300 + i * 60);
                    Canvas.SetLeft(label, 7 + j * 130);
                    xChooseCanvas.Children.Add(label);
                }
            xChooseCanvas.Background = Brushes.Ivory;
            Drag(x, y);
        }
        
        public void Drag(int x, int y)
        {
            int focusX = Convert.ToInt32((x - dragStart.X) / deviceDragLen);
            int focusY = Convert.ToInt32((y - dragStart.Y) / deviceDragLen);
            focusX = Math.Min(Math.Max(focusX, -1), 1) + 1;
            focusY = Math.Min(Math.Max(focusY, -2), 2) + 2;
            int Count = 0;
            foreach (UIElement uiElement in xChooseCanvas.Children)
            {
                Label label = uiElement as Label;
                if (Count == focusY * 3 + focusX)
                {
                    dragFocusLabel = label;
                    label.Background = Brushes.Coral;
                }
                else
                {
                    label.Background = Brushes.PaleTurquoise;
                }
                Count++;
            }
        }

        public void DragEnd(int x, int y)
        {
            Drag(x, y);
            xChooseCanvas.Background = null;
            xChooseCanvas.Children.Clear();
            //InputedText += dragFocusLabel.Content;
            //ClearPoints();
        }

        public void Confirm()
        {
            StreamWriter writer0 = new StreamWriter(new FileStream("record-" + testerName + ".txt", FileMode.Append));
            writer0.WriteLine(noticeNow);
            for (int i = 0; i < pointList.Count; ++i)
            {
                if (pointList[i].x < 0)
                {
                    writer0.WriteLine("*");
                    continue;
                }
                writer0.WriteLine(pointList[i].x + " " + pointList[i].y + " " + pointList[i].t.ToFileTime());
            }
            writer0.WriteLine();
            writer0.Close();
            
            NoticeChange();
        }


        public void AddKeyboardUi()
        {
            int keySize = 100;
            int keySize2 = keySize - 1;
            int fontSize = keySize / 2 + 1;
            String[] keyLayout = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            int[] wOffset = { 150, 150 + keySize / 4, 150 + keySize * 3 / 4 };
            int hOffset = 33;

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
                    label.Content = keyLayout[r][c];
                    Canvas.SetTop(label, hOffset + r * keySize2);
                    Canvas.SetLeft(label, wOffset[r] + c * keySize2);
                    xKeyboardCanvas.Children.Add(label);
                }
            }
        }

        public void NoticeLoad()
        {
            StreamReader reader = new StreamReader(new FileStream("PhraseSets/phrases2.txt", FileMode.Open));
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                line = line.ToLower();
                noticeList.Add(line);
            }
            reader.Close();
        }

        public void NoticeChange()
        {
            while (pointList.Count() > 0)
            {
                LeftSlip(false);
            }
            wordList.Clear();
            noticeListIndex = (noticeListIndex + 1) % noticeList.Count();
            noticeNow = noticeList[noticeListIndex];
            xNoticeTextBlock.Text = noticeListIndex.ToString() + ": " + noticeNow;
            InputedRefresh();
        }

        public void InputedRefresh()
        {
            xInputedTextBlock.Text = noticeListIndex.ToString() + ": ";
            for (int i = 0; i < Math.Min(pointList.Count(), noticeNow.Count()); ++i)
            {
                if (pointList[i].x < 0)
                {
                    xInputedTextBlock.Text += " ";
                }
                else
                {
                    if (noticeNow[i] == ' ')
                    {
                        xInputedTextBlock.Text += "*";
                    }
                    else
                    {
                        xInputedTextBlock.Text += noticeNow[i];
                    }
                }
            }
            for (int i = noticeNow.Count(); i < pointList.Count(); ++i)
            {
                xInputedTextBlock.Text += "*";
            }
            xInputedTextBlock.Text += "|";
        }

        public void operationWrite(string operation)
        {
            StreamWriter writer1 = new StreamWriter(new FileStream("operation-" + testerName + ".txt", FileMode.Append));
            writer1.WriteLine(operation);
            writer1.Close();
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
            xNoticeChangeButton.Visibility = visibility;
            xRestartButton.Visibility = visibility;
        }

        private void xSetupButton_Click(object sender, RoutedEventArgs e)
        {
            if (server != null) return;
            server = new Server(xIPTextBox.Text);
            server.Listen(this);
            MessageBox.Show("Server setup!");
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
            foreach (UIElement uiElement in xDrawCanvas.Children)
            {
                uiElement.BeginAnimation(Ellipse.OpacityProperty, null);
                uiElement.Opacity = (pointVisible == PointVisible.Visible) ? 1.0 : 0.0;
            }
        }

        private void xKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            switch (keyboardVisible)
            {
                case KeyboardVisible.KeyboardOn:
                    keyboardVisible = KeyboardVisible.KeyboardOff;
                    break;
                case KeyboardVisible.KeyboardOff:
                    keyboardVisible = KeyboardVisible.KeyboardOn;
                    break;
            }
            xKeyboardButton.Content = keyboardVisible;
            foreach (UIElement uiElement in xKeyboardCanvas.Children)
            {
                uiElement.Visibility = (keyboardVisible == KeyboardVisible.KeyboardOn) ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void xNoticeChangeButton_Click(object sender, RoutedEventArgs e)
        {
            NoticeChange();
        }

        private void xRestartButton_Click(object sender, RoutedEventArgs e)
        {
            noticeListIndex = -1;
            NoticeChange();
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
                case "devicesize":
                    mainWindow.deviceWidth = int.Parse(strs[1]);
                    mainWindow.deviceHeight = int.Parse(strs[2]);
                    switch (mainWindow.deviceWidth)
                    {
                        case 1080:
                            mainWindow.deviceResize = 2.7;
                            mainWindow.deviceYBias = 0;
                            mainWindow.deviceDragLen = 150;
                            break;
                        case 720:
                            mainWindow.deviceResize = 1.8;
                            mainWindow.deviceYBias = 0;
                            mainWindow.deviceDragLen = 150;
                            break;
                        case 480:
                            mainWindow.deviceResize = 1.22;
                            mainWindow.deviceYBias = 50;
                            mainWindow.deviceDragLen = 100;
                            break;
                    }
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
                    mainWindow.Click(int.Parse(strs[1]), int.Parse(strs[2]), DateTime.Now);
                    break;
                case "leftslip":
                    mainWindow.LeftSlip(true);
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