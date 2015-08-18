using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShowServer
{
    public partial class MainWindow : Window
    {
        public enum PointVisible { Visible, Flash, Unvisible };
        public enum KeyboardVisible { Keyboard, NoKeyboard };
        public enum LengthCheck { Check, NoCheck };
        public enum TypeChar { TypeLetter, TypeStar };

        public Server server;
        public String testerName;
        public PointVisible pointVisible = PointVisible.Visible;
        public KeyboardVisible keyboardVisible = KeyboardVisible.Keyboard;
        public LengthCheck lengthCheck = LengthCheck.Check;
        public TypeChar typeChar = TypeChar.TypeLetter;

        public int deviceWidth, deviceHeight;
        public double deviceResize = 1;
        public int deviceYBias = 0;
        public int deviceDragLen = 150;

        public List<PointAndTime> pointList = new List<PointAndTime>();
        public List<String> wordList = new List<String>();
        
        public List<String> noticeList = new List<String>();
        String noticeNow;
        int noticeListIndex = -1;

        public Point dragStart;
        public Label dragFocusLabel;


        public MainWindow()
        {
            InitializeComponent();
            AddKeyboardUi();
            NoticeLoad();
            NoticeChange();
        }


        public void AddPoint(int x, int y, DateTime t)
        {
            if (x != -100)
            {
                operationWrite("add " + x + " " + y + " " + t.ToFileTime());
            }
            else
            {
                operationWrite("space");
            }
            pointList.Add(new PointAndTime(x, y, t));
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

            this.RegisterName(ellipse.Name, ellipse);
            this.RegisterName(label.Name, label);
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

        public int BackSpace(bool fromClearPoints)
        {
            if (!fromClearPoints)
            {
                operationWrite("backspace");
            }
            if (pointList.Count == 0) return 0;

            //  -------------------- animation --------------------
            this.UnregisterName("e" + pointList.Count);
            this.UnregisterName("l" + pointList.Count);
            pointList.RemoveAt(pointList.Count - 1);
            xDrawCanvas.Children.RemoveRange(xDrawCanvas.Children.Count - 2, 2);
            //  -------------------- animation --------------------

            InputedRefresh();
            return pointList.Count;
        }

        public void ClearPoints()
        {
            while (BackSpace(true) > 0) ;
        }

        public void Confirm()
        {
            if (lengthCheck == LengthCheck.Check && pointList.Count() != noticeNow.Count())
            {
                MessageBox.Show("Expect length: " + noticeNow.Count());
                return;
            }

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
            int keySize = 70;
            int keySize2 = keySize - 1;
            int fontSize = keySize / 2 + 1;
            String[] keyLayout = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            int[] wOffset = { 9, 9 + keySize / 2, 9 + keySize };
            int hOffset = 320;

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
                    label.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
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
                String line = reader.ReadLine();
                if (line == null) break;
                line = line.ToLower();
                noticeList.Add(line);
            }
            reader.Close();
        }

        public void NoticeChange()
        {
            ClearPoints();
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
            if (typeChar == TypeChar.TypeStar)
            {
                for (char ch = 'a'; ch <= 'z'; ++ch)
                {
                    xInputedTextBlock.Text = xInputedTextBlock.Text.Replace(ch, '*');
                }
            }
        }

        public void operationWrite(string operation)
        {
            StreamWriter writer1 = new StreamWriter(new FileStream("operation-" + testerName + ".txt", FileMode.Append));
            writer1.WriteLine(operation);
            writer1.Close();
        }
        

        private void xSetupButton_Click(object sender, RoutedEventArgs e)
        {
            if (server != null) return;
            server = new Server(xTextBox.Text);
            server.Listen(this);
            MessageBox.Show("Server setup!");
        }

        private void xTesterButton_Click(object sender, RoutedEventArgs e)
        {
            testerName = xTextBox.Text;
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
                case KeyboardVisible.Keyboard:
                    keyboardVisible = KeyboardVisible.NoKeyboard;
                    break;
                case KeyboardVisible.NoKeyboard:
                    keyboardVisible = KeyboardVisible.Keyboard;
                    break;
            }
            xKeyboardButton.Content = keyboardVisible;
            foreach (UIElement uiElement in xKeyboardCanvas.Children)
            {
                uiElement.Visibility = (keyboardVisible == KeyboardVisible.Keyboard) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            }
        }

        private void xLengthCheckButton_Click(object sender, RoutedEventArgs e)
        {
            switch (lengthCheck)
            {
                case LengthCheck.Check:
                    lengthCheck = LengthCheck.NoCheck;
                    break;
                case LengthCheck.NoCheck:
                    lengthCheck = LengthCheck.Check;
                    break;
            }
            xLengthCheckButton.Content = lengthCheck;
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

        private void xTypeCharButton_Click(object sender, RoutedEventArgs e)
        {
            switch (typeChar)
            {
                case TypeChar.TypeLetter:
                    typeChar = TypeChar.TypeStar;
                    break;
                case TypeChar.TypeStar:
                    typeChar = TypeChar.TypeLetter;
                    break;
            }
            xTypeCharButton.Content = typeChar;
            InputedRefresh();
        }
    }

    public class Server : DispatcherObject
    {
        public MainWindow mainWindow;
        public TcpListener tcpListener;
        public Thread listenThread;
        public String ip;

        public Server(String ip)
        {
            this.ip = ip;
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

        public delegate void BeginInvokeDelegate(String str);
        public void BeginInvokeMethod(String str)
        {
            Console.WriteLine(str);
            String[] strs = str.Split(' ');
            switch (strs[0])
            {
                case "devicesize":
                    mainWindow.deviceWidth = Int32.Parse(strs[1]);
                    mainWindow.deviceHeight = Int32.Parse(strs[2]);
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
                    mainWindow.Confirm();
                    break;
                case "drag":
                    break;
                case "dragend":
                    break;
                case "addpoint":
                    mainWindow.AddPoint(Int32.Parse(strs[1]), Int32.Parse(strs[2]), DateTime.Now);
                    break;
                case "backspace":
                    mainWindow.BackSpace(false);
                    break;
                case "clearpoints":
                    mainWindow.ClearPoints();
                    break;
                case "space":
                    mainWindow.AddPoint(-100, -100, DateTime.Now);
                    break;
                default:
                    break;
            }
        }
    }

    public class PointAndTime
    {
        public int x, y;
        public DateTime t;
        public PointAndTime(int _x, int _y, DateTime _t)
        {
            x = _x;
            y = _y;
            t = _t;
        }
    }
}