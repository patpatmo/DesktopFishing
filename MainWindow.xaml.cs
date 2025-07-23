using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace TransparentOverlay
{
    public partial class MainWindow : Window
    {
        // Windows API 常量
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int GWL_EXSTYLE = -20;
        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        // Windows API 函数
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyBoardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        // 钩子委托和句柄
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyBoardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static IntPtr _mouseHookID = IntPtr.Zero;
        private static IntPtr _keyboardHookID = IntPtr.Zero;
        private LowLevelMouseProc _MouseProc;
        private LowLevelKeyBoardProc _KeyboardProc;

        // 鱼线相关字段
        private Point _lineEndPosition;
        private readonly Random _random = new Random();
        private double _swingFactor = 0;
        private double _lineVelocityX;
        private double _lineVelocityY;
        private const double Gravity = 1;
        private const double Damping = 0.92;

        // 碰撞检测缓存
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static bool _lastCollisionResult;

        // 钓鱼相关字段
        private bool isReadytoFishing = false;
        private bool isFishBiting = false;
        private bool isFishGet = false;
        private readonly Random random = new Random();
        private Point _hookedPosition; // 鱼钩被咬住时的固定位置
        private double _hookSwingAngle = 0; // 鱼钩摆动角度
        private double _hookSwingSpeed = 0.1; // 鱼钩摆动速度
        private DateTime _fishingStartTime = DateTime.MinValue; // 开始钓鱼时间
        private double _nextBiteTime = 0; // 下次咬钩时间（秒）
        private Point _prevMousePos;
        private DateTime _prevMouseTime = DateTime.Now;
        private double _verticalSpeed;
        private DateTime _lastPullTime = DateTime.MinValue; // 防止多次触发

        // 钓鱼 鱼显示相关

        // 性能优化相关
        private DispatcherTimer _fishingTimer;
        private DispatcherTimer _collisionTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new object();

        // 缓存变量以减少重复计算
        private DateTime _lastMouseMove = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWindow();
            InitializeTimers();

            this.Loaded += async (s, e) =>
            {
                await Task.Delay(500);
                StartAllHooks();
            };

            // 初始化鱼线末端位置
            _lineEndPosition = new Point(Width / 2, Height / 2);

            // 启动动画和钓鱼逻辑
            CompositionTarget.Rendering += UpdateFishingLine;
            _cancellationTokenSource = new CancellationTokenSource();
            StartFishingLogic();
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimers()
        {
            // 碰撞检测定时器 - 降低检测频率
            _collisionTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200) // 每200ms检测一次
            };
            _collisionTimer.Tick += CheckCollisionTimer_Tick;
            _collisionTimer.Start();

            // 钓鱼逻辑定时器
            _fishingTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(100) // 每100ms执行一次
            };
            _fishingTimer.Tick += FishingTimer_Tick;
            _fishingTimer.Start();
        }

        /// <summary>
        /// 初始化窗口属性
        /// </summary>
        private void InitializeWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;

            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            DisableClickThrough();
        }

        /// <summary>
        /// 启动全局钩子
        /// </summary>
        public void StartAllHooks()
        {
            try
            {
                _KeyboardProc = KeyboardHookCallback;
                _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _KeyboardProc,
                    GetModuleHandle(null), 0);

                _MouseProc = MouseHookCallback;
                _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _MouseProc,
                    GetModuleHandle(null), 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动钩子失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 优化的鼠标钩子回调
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    var now = DateTime.Now;
                    if ((now - _lastMouseMove).TotalMilliseconds < 16)
                    {
                        return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
                    }
                    _lastMouseMove = now;

                    var hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    var mousePos = new Point(hookStruct.pt.x, hookStruct.pt.y);

                    // ==== 新增：判断鼠标向上甩动速度 ====
                    double deltaTime = (now - _prevMouseTime).TotalSeconds;
                    if (deltaTime > 0.001)
                    {
                        double deltaY = _prevMousePos.Y - mousePos.Y; // 向上是正数
                        _verticalSpeed = deltaY / deltaTime;

                        _prevMousePos = mousePos;
                        _prevMouseTime = now;

                        // Debug 输出（可删）
                        // Debug.WriteLine($"鼠标Y速度: {_verticalSpeed:F0}");

                        if (_verticalSpeed > 400 && isFishBiting)
                        {
                            // 加0.5秒冷却防止连续触发
                            if ((now - _lastPullTime).TotalSeconds > 0.5)
                            {
                                _lastPullTime = now;
                                Dispatcher.Invoke(PullHook);
                            }
                        }
                    }

                    // ==== 原UI逻辑保持不变 ====
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var mousePosInWindow = PointFromScreen(mousePos);

                            Canvas.SetLeft(FishingRodImage, mousePosInWindow.X - FishingRodImage.Width + 40);
                            Canvas.SetTop(FishingRodImage, mousePosInWindow.Y - FishingRodImage.Height + 12);

                            _swingFactor = Math.Min(_swingFactor + 0.1, 1);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"鼠标回调UI更新错误: {ex.Message}");
                        }
                    }), DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"鼠标钩子回调错误: {ex.Message}");
                }
            }

            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }


        /// <summary>
        /// 键盘钩子回调
        /// </summary>
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int msg = wParam.ToInt32();
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        int vkCode = Marshal.ReadInt32(lParam);
                        Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                        if (key == Key.F1)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (MainCanvas.Visibility == Visibility.Visible)
                                    {
                                        MainCanvas.Visibility = Visibility.Collapsed;
                                        EnableClickThrough();
                                    }
                                    else
                                    {
                                        MainCanvas.Visibility = Visibility.Visible;
                                        DisableClickThrough();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"F1键处理错误: {ex.Message}");
                                }
                            }), DispatcherPriority.Background);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"键盘钩子回调错误: {ex.Message}");
                }
            }

            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// 启用窗口穿透
        /// </summary>
        public void EnableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        /// <summary>
        /// 禁用窗口穿透
        /// </summary>
        public void DisableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (extendedStyle & ~WS_EX_TRANSPARENT) | WS_EX_LAYERED);
        }

        /// <summary>
        /// 优化的鱼线更新
        /// </summary>
        private void UpdateFishingLine(object sender, EventArgs e)
        {
            try
            {
                // 始终获取鱼竿的实际位置，确保鱼线起点正确
                Point rodPos = FishingRodImage.TransformToVisual(MainCanvas)
                                             .Transform(new Point(20, 28));

                // 如果鱼咬钩了，鱼钩位置固定并添加摆动效果
                if (isFishBiting)
                {
                    // 更新摆动角度
                    _hookSwingAngle += _hookSwingSpeed;

                    // 在固定位置基础上添加小幅度摆动
                    double swingX = Math.Sin(_hookSwingAngle) * 8; // 左右摆动8像素
                    double swingY = Math.Cos(_hookSwingAngle * 0.7) * 3; // 上下摆动3像素

                    _lineEndPosition.X = _hookedPosition.X + swingX;
                    _lineEndPosition.Y = _hookedPosition.Y + swingY;

                    // 摆动速度随机变化，模拟鱼的挣扎
                    if (_random.NextDouble() < 0.1) // 10% 概率改变摆动速度
                    {
                        _hookSwingSpeed = 0.05 + _random.NextDouble() * 0.15; // 0.05-0.2之间
                    }
                }
                else
                {
                    // 正常的物理模拟
                    double forceX = (rodPos.X - _lineEndPosition.X) * 0.002;
                    double forceY = (rodPos.Y - _lineEndPosition.Y) * 0.002 + Gravity;

                    _lineVelocityX = (_lineVelocityX + forceX) * Damping;
                    _lineVelocityY = (_lineVelocityY + forceY) * Damping;

                    _lineEndPosition.X += _lineVelocityX;
                    _lineEndPosition.Y += _lineVelocityY;

                    // 添加摆动效果
                    if (_swingFactor > 0)
                    {
                        _lineEndPosition.X += _swingFactor * (_random.NextDouble() - 0.5) * 3;
                        _swingFactor = Math.Max(_swingFactor - 0.01, 0);
                    }
                }

                // 更新UI
                UpdateLineVisual(rodPos);
                // 检测是否显示鱼
                IsFishCanShow();
                // 将鱼图像放置在鱼钩位置
                Canvas.SetLeft(FishImage, _lineEndPosition.X - FishImage.Width / 2);
                Canvas.SetTop(FishImage, _lineEndPosition.Y - FishImage.Height / 2);

                Canvas.SetLeft(TipsGrid, _lineEndPosition.X - FishImage.Width / 2-200);
                Canvas.SetTop(TipsGrid, _lineEndPosition.Y - FishImage.Height / 2-100);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"鱼线更新错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新鱼线视觉效果
        /// </summary>
        private void UpdateLineVisual(Point startPoint)
        {
            try
            {
                Canvas.SetLeft(LineEnd, _lineEndPosition.X - LineEnd.Width / 2 - 1);
                Canvas.SetTop(LineEnd, _lineEndPosition.Y - LineEnd.Height / 2 + 9);

                LineStart.StartPoint = startPoint;

                Point controlPoint = new Point(
                    (startPoint.X * 0.3 + _lineEndPosition.X * 0.7),
                    (startPoint.Y * 0.7 + _lineEndPosition.Y * 0.3));

                LineCurve.Point1 = controlPoint;
                LineCurve.Point2 = _lineEndPosition;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"鱼线视觉更新错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 碰撞检测定时器
        /// </summary>
        private void CheckCollisionTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    bool wasInWater = isReadytoFishing;
                    isReadytoFishing = CheckCollision(Water, LineEnd, 1000);

                    if (wasInWater != isReadytoFishing)
                    {
                        if (isReadytoFishing)
                        {
                            // 刚进入水中
                            _fishingStartTime = DateTime.Now;
                            _nextBiteTime = 5 + random.NextDouble() * 10;
                            isFishBiting = false;
                            Debug.WriteLine($"开始钓鱼，预计 {_nextBiteTime:F1} 秒后咬钩");
                        }
                        else
                        {
                            // 离开水面，重置状态并隐藏水花
                            isFishBiting = false;
                            _fishingStartTime = DateTime.MinValue;

                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    waterSplash.Visibility = Visibility.Collapsed;
                                    Debug.WriteLine("离开水面，钓鱼结束，水花已隐藏");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"隐藏水花时出错: {ex.Message}");
                                }
                            }), DispatcherPriority.Normal);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"碰撞检测错误: {ex.Message}");
            }
        }
        /// <summary>
        /// 钓鱼逻辑定时器
        /// </summary>
        private void FishingTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    if (isReadytoFishing && !isFishBiting && _fishingStartTime != DateTime.MinValue)
                    {
                        // 检查是否到了咬钩时间
                        double elapsedSeconds = (DateTime.Now - _fishingStartTime).TotalSeconds;
                        if (elapsedSeconds >= _nextBiteTime)
                        {
                            // 鱼咬钩了！
                            isFishBiting = true;
                            _hookedPosition = new Point(_lineEndPosition.X, _lineEndPosition.Y);
                            _hookSwingAngle = 0;
                            _hookSwingSpeed = 0.08 + random.NextDouble() * 0.12;

                            Debug.WriteLine($"鱼咬钩了！位置: ({_hookedPosition.X:F1}, {_hookedPosition.Y:F1})");

                            // 设置鱼挣扎的持续时间
                            Task.Delay(TimeSpan.FromSeconds(15 + random.NextDouble() * 20)).ContinueWith(_ =>
                            {
                                lock (_lockObject)  // 添加锁
                                {
                                    if (isFishBiting)
                                    {
                                        // 鱼跑了
                                        isFishBiting = false;
                                        _fishingStartTime = DateTime.Now;
                                        _nextBiteTime = 5 + random.NextDouble() * 15;

                                        Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            try
                                            {
                                                waterSplash.Visibility = Visibility.Collapsed;
                                                Debug.WriteLine("鱼跑了！水花已隐藏");
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine($"隐藏水花时出错: {ex.Message}");
                                            }
                                        }), DispatcherPriority.Normal);
                                    }
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"钓鱼逻辑错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动钓鱼逻辑
        /// </summary>
        private void StartFishingLogic()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);

                        lock (_lockObject)
                        {
                            if (isFishBiting)
                            {
                                OnFishCaught();
                                //ShowSplashAt();
                                // 鱼咬钩期间，可以添加其他逻辑
                                // 比如检测玩家是否成功钓到鱼等

                                // 这里可以添加键盘输入检测来收线等操作
                                // 例如：检测空格键是否按下来收线
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"钓鱼逻辑异常: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// 优化的碰撞检测
        /// </summary>
        public static bool CheckCollision(FrameworkElement element1, FrameworkElement element2, double bottomExpand = 0)
        {
            try
            {
                if (element1 == null || element2 == null)
                    return false;

                Point pos1 = element1.TranslatePoint(new Point(0, 0), null);
                Point pos2 = element2.TranslatePoint(new Point(0, 0), null);

                if (pos1.X == double.NaN || pos1.Y == double.NaN ||
                    pos2.X == double.NaN || pos2.Y == double.NaN)
                    return false;

                Rect rect1 = new Rect(pos1.X, pos1.Y, element1.ActualWidth, element1.ActualHeight + bottomExpand);
                Rect rect2 = new Rect(pos2.X, pos2.Y, element2.ActualWidth, element2.ActualHeight);

                return rect1.IntersectsWith(rect2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"碰撞检测异常: {ex.Message}");
                return false;
            }
        }
        //鱼上钩时的函数
        private void OnFishCaught()
        {
            lock (_lockObject)  // 添加锁确保线程安全
            {
                if (isFishBiting && isReadytoFishing)  // 双重检查
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 计算水花位置
                            double hookX = _hookedPosition.X;
                            double waterSurfaceY = MainCanvas.ActualHeight - Water.Height;

                            // 设置水花位置
                            Canvas.SetLeft(waterSplash, hookX - waterSplash.Width / 2);
                            Canvas.SetTop(waterSplash, waterSurfaceY - waterSplash.Height / 2 - 10);

                            // 显示水花
                            ShowWaterSplash();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"显示水花时出错: {ex.Message}");
                        }
                    }), DispatcherPriority.Normal);
                }
            }
        }
        //收杆动作函数
        private void PullHook()
        {
            lock (_lockObject)  // 添加锁确保线程安全
            {
                if (isFishBiting)
                {
                    Debug.WriteLine("拉杆起钩！");
                    _lineVelocityY = -90;
                    _lineVelocityX = (_random.NextDouble() - 0.5) * 10;
                    isFishBiting = false;
                    isFishGet = true;
                    // 确保在UI线程中执行
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            waterSplash.Visibility = Visibility.Collapsed;
                            Debug.WriteLine("水花已隐藏");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"隐藏水花时出错: {ex.Message}");
                        }
                    }), DispatcherPriority.Normal);
                }
            }
        }
        //钓鱼提示淡出动画
        private void StartFadeOut()
        {
            // 确保在UI线程上执行
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(StartFadeOut));
                return;
            }

            try
            {
                // 停止任何现有的动画
                TipsGrid.BeginAnimation(UIElement.OpacityProperty, null);

                // 立即设置为可见状态
                TipsGrid.Visibility = Visibility.Visible;
                TipsGrid.Opacity = 1.0;

                // 强制刷新布局
                TipsGrid.UpdateLayout();

                // 创建渐隐动画
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(3),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                fadeOutAnimation.Completed += (s, args) =>
                {
                    try
                    {
                        // 清理动画
                        TipsGrid.BeginAnimation(UIElement.OpacityProperty, null);
                        // 设置最终状态
                        TipsGrid.Opacity = 0;
                        TipsGrid.Visibility = Visibility.Collapsed;
                        Debug.WriteLine("渐隐动画完成");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"渐隐动画完成处理异常: {ex.Message}");
                    }
                };

                // 开始动画
                TipsGrid.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                Debug.WriteLine("渐隐动画开始");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartFadeOut异常: {ex.Message}");
            }
        }
        private void ShowWaterSplash()
        {
            try
            {
                // 移除条件判断，直接显示
                waterSplash.Visibility = Visibility.Visible;
                Debug.WriteLine("水花已显示");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"显示水花异常: {ex.Message}");
            }
        }
        private bool _isHideFishTimerCalled = false; // 类级别变量

        private void IsFishCanShow()
        {
            if (isFishGet)
            {
                FishImage.Visibility = Visibility.Visible;
                //double fishAngle = Math.Sin(_hookSwingAngle * 2) * 15;
                //FishRotateTransform.Angle = fishAngle;

                // 只在第一次进入时调用
                if (!_isHideFishTimerCalled)
                {
                    _isHideFishTimerCalled = true;
                    HideFishTimer();
                }
            }
            else
            {
                FishImage.Visibility = Visibility.Collapsed;
                //FishRotateTransform.Angle = 0;
                _isHideFishTimerCalled = false; // 重置状态，允许下次调用
            }
        }
        private async void HideFishTimer()
        {
            
            await Task.Delay(3000).ConfigureAwait(true); // 确保回到 UI 线程
            isFishGet = false;
            FishImage.Visibility = Visibility.Collapsed;
            StartFadeOut();
        }
        /// <summary>
        /// 清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _fishingTimer?.Stop();
                _collisionTimer?.Stop();

                CompositionTarget.Rendering -= UpdateFishingLine;

                if (_mouseHookID != IntPtr.Zero)
                    UnhookWindowsHookEx(_mouseHookID);
                if (_keyboardHookID != IntPtr.Zero)
                    UnhookWindowsHookEx(_keyboardHookID);

                _cancellationTokenSource?.Dispose();
                _fishingTimer = null;
                _collisionTimer = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"资源清理错误: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        // 数据结构
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private struct POINT
        {
            public int x;
            public int y;
        }
    }
}