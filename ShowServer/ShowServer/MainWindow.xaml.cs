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
        enum KeyboardVisible { KeyboardOn, KeyboardOff };

        Server server;
        string testerName;
        PointVisible pointVisible = PointVisible.Flash;
        KeyboardVisible  keyboardVisible = KeyboardVisible.KeyboardOn;

        ImageBrush TEXT_ENTRY_BACKGROUND = new ImageBrush(new BitmapImage(new Uri("../../../Image/text-entry-background.png", UriKind.Relative)));
        Brush DRAG_SELETED_BRUSH = new SolidColorBrush(Color.FromRgb(2, 91, 195));

        public List<String> noticeList = new List<String>();
        public int noticeListIndex = 0;

        const int DRAG_ROW = 5;
        const int DRAG_COLUMN = 5;
        const int DRAG_LEN_X = 50;
        const int DRAG_LEN_Y = 30;
        bool draging = false;
        Point dragStart;
        string dragSelected;
        int dragSeletedIntex;

        List<UltraPoint> pointList = new List<UltraPoint>();
        List<String> wordList = new List<String>();
        Recognition recongition = new Recognition();

        public MainWindow()
        {
            InitializeComponent();
            Background = new ImageBrush(new BitmapImage(new Uri("../../../Image/background.png", UriKind.Relative)));
            xKeyboardCanvas.Background = TEXT_ENTRY_BACKGROUND;
            xInputedTextBlock.Focus();
            AddKeyboardUi();
            LoadNotice();
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
            if (userEvent)
            {
                OperationWrite("backspace");
            }
            if (pointList.Count == 0)
            {
                if (wordList.Count != 0)
                {
                    wordList.RemoveAt(wordList.Count - 1);
                }
            }
            else
            {
                //  -------------------- animation --------------------
                UnregisterName("i" + pointList.Count);
                xPointCanvas.Children.RemoveAt(xPointCanvas.Children.Count - 1);
                //  -------------------- animation --------------------

                pointList.RemoveAt(pointList.Count - 1);
            }
            UpdateTextEntry();
            return pointList.Count;
        }
        public void RightSlip()
        {
            string[] wordArray = noticeList[noticeListIndex].Split(' ');
            if (wordList.Count == wordArray.Length)
            {
                ChangeNotice();
            }
            else
            {
                Confirm();
            }
        }
        public void DragBegin(int x, int y)
        {
            dragStart = new Point(x, y);
            xDragCanvas.Background = TEXT_ENTRY_BACKGROUND;
            draging = true;
            Drag(x, y);
        }       
        public void Drag(int x, int y)
        {
            UpdateTextEntry();
            int focusX = Convert.ToInt32((x - dragStart.X) / DRAG_LEN_X);
            int focusY = Convert.ToInt32((y - dragStart.Y) / DRAG_LEN_Y);
            focusX = Math.Min(Math.Max(focusX, 0), DRAG_COLUMN - 1);
            focusY = Math.Min(Math.Max(focusY, 0), DRAG_ROW - 1);
            int Count = 0;
            foreach (UIElement uiElement in xDragCanvas.Children)
            {
                Label label = uiElement as Label;
                if (Count == focusY * DRAG_COLUMN + focusX)
                {
                    dragSelected = label.Content.ToString();
                    label.Background = DRAG_SELETED_BRUSH;
                }
                else
                {
                    label.Background = null;
                }
                Count++;
            }
        }
        public void DragEnd(int x, int y)
        {
            Drag(x, y);
            xDragCanvas.Background = null;
            xDragCanvas.Children.Clear();
            draging = false;
            Confirm();
        }
        
        void UpdateTextEntry()
        {
            xInputedTextBlock.Text = noticeListIndex.ToString() + ": ";
            foreach (string word in wordList) xInputedTextBlock.Text += word + " ";
            string[] candidates = recongition.Recognize(pointList);
            if (pointList.Count > 0)
            {
                dragSelected = candidates[dragSeletedIntex = 0];
                xInputedTextBlock.Text += dragSelected.Substring(0, pointList.Count);
            }
            xInputedTextBlock.SelectionStart = xInputedTextBlock.Text.Length;

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
                    if (draging == false && i == 0 && j == 0) label.Background = DRAG_SELETED_BRUSH;
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
                    label.Content = keyLayout[r][c];
                    Canvas.SetTop(label, hOffset + r * keySize2);
                    Canvas.SetLeft(label, wOffset[r] + c * keySize2);
                    xKeyboardCanvas.Children.Add(label);
                }
            }
        }
        void LoadNotice()
        {
            StreamReader reader = new StreamReader(new FileStream("../../../PhraseSets/phrases2.txt", FileMode.Open));
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                line = line.ToLower();
                noticeList.Add(line);
            }
            reader.Close();
            noticeListIndex = noticeList.Count - 1;
            ChangeNotice();
        }
        void ChangeNotice()
        {
            ClearPoints();
            wordList.Clear();
            noticeListIndex = (noticeListIndex + 1) % noticeList.Count();
            xNoticeTextBlock.Text = "" + noticeListIndex.ToString() + ": " + noticeList[noticeListIndex];
            UpdateTextEntry();
        }
        void ClearPoints()
        {
            while (pointList.Count() > 0)
            {
                LeftSlip(false);
            }
        }
        void Confirm()
        {
            wordList.Add(dragSelected);
            ClearPoints();
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
            foreach (UIElement uiElement in xPointCanvas.Children)
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
            ChangeNotice();
        }
        private void xRestartButton_Click(object sender, RoutedEventArgs e)
        {
            noticeListIndex = noticeList.Count - 1;
            ChangeNotice();
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