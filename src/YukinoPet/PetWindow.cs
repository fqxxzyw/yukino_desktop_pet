using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace YukinoPet
{
    internal sealed class PetWindow : Window
    {
        private const int CellWidth = 192;
        private const int CellHeight = 208;
        private const string StartupName = "YukinoPet";

        private readonly PetApplication application;
        private readonly Random random = new Random();
        private readonly CharacterState character = new CharacterState();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Image sprite = new Image();
        private readonly TranslateTransform breathing = new TranslateTransform();
        private readonly Dictionary<string, AnimationClip> clips;
        private readonly Dictionary<string, BitmapSource> frameCache = new Dictionary<string, BitmapSource>();
        private readonly AnimationController animation;
        private readonly LookController look = new LookController();
        private readonly QuoteManager quotes;
        private readonly SpeechBubbleWindow bubble = new SpeechBubbleWindow();
        private readonly BehaviorDirector behavior;
        private readonly AudioManager audio;
        private readonly BitmapSource baseAtlas;
        private readonly BitmapSource walkingRight;
        private readonly BitmapSource walkingLeft;
        private readonly Forms.NotifyIcon trayIcon;

        private double previousSeconds;
        private double walkTargetX;
        private double walkTargetY;
        private double walkSpeed;
        private double velocityX;
        private double velocityY;
        private Rect walkArea;
        private bool hasWalkArea;
        private bool walking;
        private bool sitting;
        private double sittingRemaining;
        private bool facingRight = true;
        private bool closing;
        private bool mouseDown;
        private bool dragging;
        private bool headPatCandidate;
        private bool headPatRecognized;
        private Point mouseDownScreen;
        private Point dragWindowOrigin;
        private Point lastPatPoint;
        private int patReversals;
        private int patDirection;
        private DateTime mouseDownAt;
        private readonly Queue<DateTime> clickTimes = new Queue<DateTime>();
        private DateTime focusEnds = DateTime.MinValue;
        private bool pendingPomodoro;
        private DateTime pomodoroReadyAt = DateTime.MinValue;
        private DateTime lastPomodoroCompleted = DateTime.MinValue;
        private string lastFrameKey = String.Empty;
        private PetBehaviorState behaviorState = PetBehaviorState.Idle;

        public AppSettings Settings { get; private set; }
        public bool IsWalking { get { return walking; } }
        public bool IsSitting { get { return sitting; } }
        public bool IsBubbleVisible { get { return bubble.IsSpeaking; } }
        public bool IsUserBusy { get { return mouseDown || dragging || headPatRecognized; } }
        public bool IsFocusActive { get { return focusEnds > DateTime.Now || pendingPomodoro; } }
        public bool HasBlockingAnimation { get { return animation.IsOneShotPlaying && animation.CurrentPriority >= 70; } }
        public PetBehaviorState BehaviorState { get { return behaviorState; } }
        public bool IsInteractionLocked { get { return IsInteractionState(behaviorState); } }

        public PetWindow(PetApplication application)
        {
            this.application = application;
            string localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YukinoPet");
            Settings = new AppSettings(Path.Combine(localData, "settings.ini"));
            Settings.Load();

            baseAtlas = LoadResource("YukinoPet.Sprites.png");
            walkingRight = LoadResource("YukinoPet.WalkingRight.png");
            walkingLeft = LoadResource("YukinoPet.WalkingLeft.png");
            clips = CreateClips();
            animation = new AnimationController(clips, random);
            string quotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quotes.json");
            quotes = new QuoteManager(quotesPath, random);
            behavior = new BehaviorDirector(random, character);
            audio = new AudioManager(random);

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = Settings.Topmost;
            Opacity = Settings.Opacity;
            Width = CellWidth * Settings.Scale;
            Height = CellHeight * Settings.Scale;
            UseLayoutRounding = true;

            sprite.Stretch = Stretch.Fill;
            sprite.RenderTransform = breathing;
            sprite.RenderTransformOrigin = new Point(0.5, 0.8);
            Content = sprite;

            Loaded += OnLoaded;
            Closed += OnClosed;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonUp += OnMouseRightButtonUp;
            CompositionTarget.Rendering += OnRendering;

            trayIcon = CreateTrayIcon();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Rect area = GetCurrentWorkArea();
            Left = Double.IsNaN(Settings.Left) ? area.Right - Width - 32 : Settings.Left;
            Top = Double.IsNaN(Settings.Top) ? area.Bottom - Height - 8 : Settings.Top;
            ClampToVisibleArea();
            previousSeconds = clock.Elapsed.TotalSeconds;
            RenderCurrentFrame(true);
            RequestQuote(QuoteTrigger.Greeting, true);
        }

        private Dictionary<string, AnimationClip> CreateClips()
        {
            return new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase)
            {
                { "idle", new AnimationClip("idle", 0, 0, 7, new[] { 700, 500, 500, 120, 500, 180, 300 }, true, 5, true, 3000, 8000) },
                { "run-right", new AnimationClip("run-right", 1, 0, 8, new[] { 92, 78, 78, 105, 92, 78, 78, 105 }, true, 45, true, 0, 0) },
                { "run-left", new AnimationClip("run-left", 2, 0, 8, new[] { 92, 78, 78, 105, 92, 78, 78, 105 }, true, 45, true, 0, 0) },
                { "wave", new AnimationClip("wave", 3, 0, 4, new[] { 130, 105, 165, 230 }, false, 60, false, 0, 0) },
                { "jump", new AnimationClip("jump", 4, 0, 5, new[] { 110, 90, 145, 95, 180 }, false, 85, false, 0, 0) },
                { "lifted", new AnimationClip("lifted", 4, 2, 1, new[] { 220 }, true, 100, false, 0, 0) },
                { "landing", new AnimationClip("landing", 4, 3, 2, new[] { 95, 210 }, false, 95, false, 0, 0) },
                { "annoyed", new AnimationClip("annoyed", 5, 0, 6, new[] { 140, 120, 180, 150, 190, 270 }, false, 75, false, 0, 0) },
                { "waiting", new AnimationClip("waiting", 6, 0, 6, new[] { 240, 210, 260, 230, 300, 420 }, false, 35, true, 0, 0) },
                { "run", new AnimationClip("run", 7, 0, 8, new[] { 88, 76, 76, 102, 88, 76, 76, 102 }, true, 45, true, 0, 0) },
                { "review", new AnimationClip("review", 8, 0, 6, new[] { 260, 310, 350, 280, 320, 520 }, false, 30, true, 0, 0) },
                { "head-tilt", new AnimationClip("head-tilt", 8, 0, 6, new[] { 150, 180, 260, 700, 180, 220 }, false, 65, false, 0, 0) },
                { "sitting", new AnimationClip("sitting", 6, 0, 6, new[] { 280, 240, 330, 270, 360, 480 }, true, 55, true, 0, 0) },
                { "walk-right", new AnimationClip("walk-right", 11, 0, 8, new[] { 165, 115, 130, 185, 165, 115, 130, 185 }, true, 40, true, 0, 0) },
                { "walk-left", new AnimationClip("walk-left", 12, 0, 8, new[] { 165, 115, 130, 185, 165, 115, 130, 185 }, true, 40, true, 0, 0) }
            };
        }

        private void OnRendering(object sender, EventArgs e)
        {
            double nowSeconds = clock.Elapsed.TotalSeconds;
            double elapsed = Math.Max(0, Math.Min(0.12, nowSeconds - previousSeconds));
            previousSeconds = nowSeconds;

            UpdateSitting(elapsed);
            behavior.Update(this, elapsed, DateTime.Now);
            bool lookChanged = look.Update(elapsed);
            bool frameChanged = animation.Update(elapsed, Settings.AnimationPaused);
            UpdateFocusTimer();
            ProcessPendingPomodoro();
            bubble.Update(elapsed, GetPetBounds(), GetCurrentWorkArea());

            if (!Settings.AnimationPaused && !dragging && !walking && animation.CurrentName == "idle")
            {
                double phase = nowSeconds * Math.PI * 2.0 / 3.2;
                breathing.Y = Math.Sin(phase) * 1.15 * Settings.Scale;
            }
            else breathing.Y = 0;

            if (frameChanged || lookChanged) RenderCurrentFrame(false);
        }

        private void UpdateFocusTimer()
        {
            if (focusEnds == DateTime.MinValue || DateTime.Now < focusEnds) return;
            focusEnds = DateTime.MinValue;
            character.CurrentMood = Mood.Relaxed;
            if ((DateTime.Now - lastPomodoroCompleted).TotalSeconds < 30) return;
            lastPomodoroCompleted = DateTime.Now;
            pendingPomodoro = true;
            pomodoroReadyAt = DateTime.Now.AddMilliseconds(random.Next(600, 2300));
        }

        private void ProcessPendingPomodoro()
        {
            if (!pendingPomodoro || DateTime.Now < pomodoroReadyAt) return;
            if (behaviorState == PetBehaviorState.Dragging || behaviorState == PetBehaviorState.Greeting || behaviorState == PetBehaviorState.Interaction || behaviorState == PetBehaviorState.HeadTilt || behaviorState == PetBehaviorState.Sitting || behaviorState == PetBehaviorState.Talking) return;
            pendingPomodoro = false;
            StopMovement();
            SetBehaviorState(PetBehaviorState.Talking);
            PlayAnimation("wave", true, FinishInteraction);
            RequestQuote(QuoteTrigger.Break, true);
            TryPlayVoice(VoiceTrigger.Pomodoro);
        }

        private void RenderCurrentFrame(bool force)
        {
            int row = animation.CurrentRow;
            int column = animation.CurrentColumn;
            if (look.Active && animation.CurrentName == "idle" && !mouseDown)
            {
                row = look.Row;
                column = look.Column;
            }
            string key = row + ":" + column;
            if (!force && key == lastFrameKey) return;
            BitmapSource frame;
            if (!frameCache.TryGetValue(key, out frame))
            {
                try
                {
                    BitmapSource sheet = row == 11 ? walkingRight : row == 12 ? walkingLeft : baseAtlas;
                    int sourceRow = row >= 11 ? 0 : row;
                    frame = new CroppedBitmap(sheet, new Int32Rect(column * CellWidth, sourceRow * CellHeight, CellWidth, CellHeight));
                    frame.Freeze();
                    frameCache[key] = frame;
                }
                catch { return; }
            }
            sprite.Source = frame;
            lastFrameKey = key;
        }

        public bool PlayAnimation(string name, bool force, Action onComplete)
        {
            bool result = animation.Play(name, force, onComplete);
            if (result) RenderCurrentFrame(true);
            return result;
        }

        public void BeginWalking(double targetX, double targetY, double speed)
        {
            if (dragging || IsInteractionState(behaviorState)) return;
            Rect area = GetCurrentWorkArea();
            walkArea = area;
            hasWalkArea = true;
            ClampPositionToArea(area);
            walkTargetX = Math.Max(area.Left, Math.Min(targetX, area.Right - Width));
            walkTargetY = Math.Max(area.Top, Math.Min(targetY, area.Bottom - Height));
            walkSpeed = Math.Max(20, Math.Min(45, speed));
            double dx = walkTargetX - Left;
            double dy = walkTargetY - Top;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 3)
            {
                hasWalkArea = false;
                return;
            }
            velocityX = dx / length * walkSpeed;
            velocityY = dy / length * walkSpeed;
            if (Math.Abs(dx) > 2) facingRight = dx > 0;
            walking = true;
            sitting = false;
            SetBehaviorState(PetBehaviorState.Walk);
            character.CurrentAction = "walking";
            PlayAnimation(facingRight ? "walk-right" : "walk-left", true, null);
        }

        public void BeginWalking(double targetX, double speed) { BeginWalking(targetX, Top, speed); }

        public void UpdateWalking(double elapsedSeconds)
        {
            if (!walking || dragging || behaviorState != PetBehaviorState.Walk) return;
            double dx = walkTargetX - Left;
            double dy = walkTargetY - Top;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double step = walkSpeed * elapsedSeconds;
            if (distance <= step + 0.5)
            {
                Left = walkTargetX;
                Top = walkTargetY;
                Rect area = hasWalkArea ? walkArea : GetCurrentWorkArea();
                bool reachedBottom = Math.Abs((Top + Height) - area.Bottom) <= 2.5;
                if (reachedBottom) StartSitting(4 + random.NextDouble() * 6);
                else StopMovement();
                return;
            }
            Rect areaForStep = hasWalkArea ? walkArea : GetCurrentWorkArea();
            double nextLeft = Left + velocityX * elapsedSeconds;
            double nextTop = Top + velocityY * elapsedSeconds;
            Left = Math.Max(areaForStep.Left, Math.Min(nextLeft, areaForStep.Right - Width));
            Top = Math.Max(areaForStep.Top, Math.Min(nextTop, areaForStep.Bottom - Height));
        }

        /// <summary>停止二维移动并清除旧目标和速度，防止交互后继续滑行。</summary>
        public void StopMovement()
        {
            walking = false;
            velocityX = 0;
            velocityY = 0;
            walkTargetX = Double.NaN;
            walkTargetY = Double.NaN;
            hasWalkArea = false;
            if (sitting) sitting = false;
            character.CurrentAction = "idle";
            if (behaviorState == PetBehaviorState.Walk || behaviorState == PetBehaviorState.Sitting)
            {
                SetBehaviorState(PetBehaviorState.Idle);
                PlayAnimation("idle", true, null);
            }
        }

        public void StopWalking() { StopMovement(); }

        public void StartSitting(double seconds)
        {
            walking = false;
            velocityX = velocityY = 0;
            walkTargetX = walkTargetY = Double.NaN;
            hasWalkArea = false;
            sitting = true;
            sittingRemaining = Math.Max(3, seconds);
            character.CurrentAction = "sitting";
            SetBehaviorState(PetBehaviorState.Sitting);
            PlayAnimation("sitting", true, null);
            if (random.NextDouble() < 0.22) RequestQuote(QuoteTrigger.Observation, false);
        }

        private void UpdateSitting(double elapsedSeconds)
        {
            if (!sitting || behaviorState != PetBehaviorState.Sitting) return;
            sittingRemaining -= elapsedSeconds;
            if (sittingRemaining <= 0)
            {
                sitting = false;
                SetBehaviorState(PetBehaviorState.Idle);
                character.CurrentAction = "idle";
                PlayAnimation("idle", true, null);
            }
        }

        public void BeginHeadTilt()
        {
            StopMovement();
            SetBehaviorState(PetBehaviorState.HeadTilt);
            PlayAnimation("head-tilt", true, FinishInteraction);
        }

        public bool TryPlayVoice(VoiceTrigger trigger)
        {
            return audio.TryPlayRandom(trigger, Settings.SoundEnabled && !Settings.QuietMode, Settings.SoundVolume);
        }

        public Point GetCursorPositionDip()
        {
            System.Drawing.Point pixel = Forms.Cursor.Position;
            Point point = new Point(pixel.X, pixel.Y);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null && source.CompositionTarget != null)
                point = source.CompositionTarget.TransformFromDevice.Transform(point);
            return point;
        }

        public void AimAt(Point cursor)
        {
            Point center = new Point(Left + Width / 2, Top + Height * 0.38);
            look.Aim(cursor.X - center.X, cursor.Y - center.Y);
        }

        public void ReleaseLook() { look.Release(); }

        public Rect GetCurrentWorkArea()
        {
            if (Forms.Screen.AllScreens.Length == 1)
                return SystemParameters.WorkArea;
            System.Drawing.Point center = new System.Drawing.Point((int)((Left + Width / 2) * DeviceScaleX()), (int)((Top + Height / 2) * DeviceScaleY()));
            if (Double.IsNaN(Left) || Double.IsNaN(Top)) center = Forms.Cursor.Position;
            System.Drawing.Rectangle wa = Forms.Screen.FromPoint(center).WorkingArea;
            return new Rect(wa.Left / DeviceScaleX(), wa.Top / DeviceScaleY(), wa.Width / DeviceScaleX(), wa.Height / DeviceScaleY());
        }

        public bool RequestQuote(QuoteTrigger trigger, bool highPriority)
        {
            if (!Settings.ShowBubbles || Settings.QuietMode && !highPriority) return false;
            if ((trigger == QuoteTrigger.Mouse || trigger == QuoteTrigger.Click || trigger == QuoteTrigger.HeadPat || trigger == QuoteTrigger.Annoyed) && !Settings.InteractionQuotes) return false;
            QuoteEntry entry;
            if (!quotes.TryPick(trigger, character, DateTime.Now, highPriority, character.CurrentAction, out entry)) return false;
            return bubble.ShowQuote(entry.Text, highPriority ? Math.Max(80, entry.Priority) : entry.Priority, GetPetBounds(), GetCurrentWorkArea(), Settings.Scale);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Settings.InteractionPaused) return;
            behavior.Interrupt(this);
            mouseDown = true;
            dragging = false;
            headPatRecognized = false;
            mouseDownAt = DateTime.Now;
            mouseDownScreen = GetCursorPositionDip();
            dragWindowOrigin = new Point(Left, Top);
            Point local = e.GetPosition(this);
            headPatCandidate = local.Y <= Height * 0.42;
            lastPatPoint = mouseDownScreen;
            patReversals = 0;
            patDirection = 0;
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = GetCursorPositionDip();
            double dx = current.X - mouseDownScreen.X;
            double dy = current.Y - mouseDownScreen.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (headPatCandidate && !dragging)
            {
                double segment = current.X - lastPatPoint.X;
                int direction = Math.Abs(segment) >= 2 ? Math.Sign(segment) : 0;
                if (direction != 0 && patDirection != 0 && direction != patDirection && Math.Abs(current.Y - mouseDownScreen.Y) < 22) patReversals++;
                if (direction != 0) patDirection = direction;
                lastPatPoint = current;
                if (patReversals >= 3 && (DateTime.Now - mouseDownAt).TotalMilliseconds >= 550)
                {
                    headPatRecognized = true;
                    SetBehaviorState(PetBehaviorState.Interaction);
                    PlayAnimation("waiting", true, null);
                }
                if (!headPatRecognized && (Math.Abs(dy) > 28 || distance > 46)) headPatCandidate = false;
            }

            if (!headPatCandidate && !headPatRecognized && (distance > 7 || (DateTime.Now - mouseDownAt).TotalMilliseconds > 180))
            {
                dragging = true;
                SetBehaviorState(PetBehaviorState.Dragging);
                bubble.Dismiss();
                PlayAnimation("lifted", true, null);
            }
            if (dragging)
            {
                Left = dragWindowOrigin.X + dx;
                Top = dragWindowOrigin.Y + dy;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!mouseDown) return;
            ReleaseMouseCapture();
            mouseDown = false;
            if (dragging)
            {
                dragging = false;
                ClampToVisibleArea();
                character.RegisterInteraction(0, 0, 1.5);
                SetBehaviorState(PetBehaviorState.Interaction);
                PlayAnimation("landing", true, FinishInteraction);
                if (random.NextDouble() < 0.28) RequestQuote(QuoteTrigger.Click, false);
            }
            else if (headPatRecognized)
            {
                headPatRecognized = false;
                character.RegisterInteraction(0.7, 0.5, -1.3);
                RequestQuote(QuoteTrigger.HeadPat, true);
                FinishInteraction();
            }
            else RegisterClick();
            headPatCandidate = false;
            Settings.Left = Left;
            Settings.Top = Top;
            Settings.Save();
            e.Handled = true;
        }

        private void RegisterClick()
        {
            DateTime now = DateTime.Now;
            while (clickTimes.Count > 0 && (now - clickTimes.Peek()).TotalSeconds > 4.5) clickTimes.Dequeue();
            clickTimes.Enqueue(now);
            int count = clickTimes.Count;
            StopMovement();
            SetBehaviorState(PetBehaviorState.Interaction);
            if (count >= 7)
            {
                character.RegisterInteraction(-0.2, -0.1, 10);
                PlayAnimation("annoyed", true, FinishInteraction);
                RequestQuote(QuoteTrigger.Annoyed, true);
                clickTimes.Clear();
            }
            else if (count >= 4)
            {
                character.RegisterInteraction(0, 0, 3);
                RequestQuote(QuoteTrigger.Click, true);
                SetBehaviorState(PetBehaviorState.HeadTilt);
                PlayAnimation("head-tilt", true, FinishInteraction);
            }
            else
            {
                character.RegisterInteraction(0.15, 0.1, 0.35);
                SetBehaviorState(PetBehaviorState.Greeting);
                PlayAnimation("wave", true, FinishInteraction);
                if (random.NextDouble() < 0.35) RequestQuote(QuoteTrigger.Click, false);
            }
            TryPlayVoice(VoiceTrigger.Click);
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ContextMenu menu = BuildContextMenu();
            menu.IsOpen = true;
            e.Handled = true;
        }

        private ContextMenu BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.FontFamily = new FontFamily("Microsoft YaHei UI");
            AddMenu(menu, "和她打招呼", false, delegate { StopMovement(); SetBehaviorState(PetBehaviorState.Greeting); PlayAnimation("wave", true, FinishInteraction); RequestQuote(QuoteTrigger.Greeting, true); TryPlayVoice(VoiceTrigger.Click); });
            AddMenu(menu, "随机试听一条语音", false, PreviewVoice);
            AddMenu(menu, "歪头看看", false, BeginHeadTilt);
            AddMenu(menu, "原地跳一下", false, delegate { StopMovement(); SetBehaviorState(PetBehaviorState.Interaction); PlayAnimation("jump", true, FinishInteraction); });
            AddMenu(menu, "安静地走一走", false, delegate
            {
                Rect area = GetCurrentWorkArea();
                double targetX = area.Left + 24 + random.NextDouble() * Math.Max(1, area.Width - Width - 48);
                double targetY = area.Top + 12 + random.NextDouble() * Math.Max(1, area.Height - Height - 24);
                BeginWalking(targetX, targetY, 32);
            });
            menu.Items.Add(new Separator());
            AddMenu(menu, Settings.AnimationPaused ? "继续动画" : "暂停动画", Settings.AnimationPaused, delegate { Settings.AnimationPaused = !Settings.AnimationPaused; SaveSettings(); });
            AddMenu(menu, Settings.InteractionPaused ? "继续互动" : "暂停互动", Settings.InteractionPaused, delegate { Settings.InteractionPaused = !Settings.InteractionPaused; SaveSettings(); });
            AddMenu(menu, "安静模式", Settings.QuietMode, delegate { Settings.QuietMode = !Settings.QuietMode; if (Settings.QuietMode) bubble.Dismiss(); SaveSettings(); });

            MenuItem quoteMenu = new MenuItem { Header = "语录设置" };
            AddMenu(quoteMenu, "随机语录", Settings.RandomQuotes, delegate { Settings.RandomQuotes = !Settings.RandomQuotes; SaveSettings(); });
            AddMenu(quoteMenu, "互动语录", Settings.InteractionQuotes, delegate { Settings.InteractionQuotes = !Settings.InteractionQuotes; SaveSettings(); });
            AddMenu(quoteMenu, "深夜提醒", Settings.NightReminders, delegate { Settings.NightReminders = !Settings.NightReminders; SaveSettings(); });
            AddMenu(quoteMenu, "显示气泡", Settings.ShowBubbles, delegate { Settings.ShowBubbles = !Settings.ShowBubbles; if (!Settings.ShowBubbles) bubble.Dismiss(); SaveSettings(); });
            menu.Items.Add(quoteMenu);

            MenuItem voiceMenu = new MenuItem { Header = "语音设置" };
            AddMenu(voiceMenu, "播放语音", Settings.SoundEnabled, delegate { Settings.SoundEnabled = !Settings.SoundEnabled; if (!Settings.SoundEnabled) audio.Stop(); SaveSettings(); });
            AddMenu(voiceMenu, "音量 25%", Near(Settings.SoundVolume, 0.25), delegate { Settings.SoundVolume = 0.25; SaveSettings(); });
            AddMenu(voiceMenu, "音量 50%", Near(Settings.SoundVolume, 0.50), delegate { Settings.SoundVolume = 0.50; SaveSettings(); });
            AddMenu(voiceMenu, "音量 75%", Near(Settings.SoundVolume, 0.75), delegate { Settings.SoundVolume = 0.75; SaveSettings(); });
            AddMenu(voiceMenu, "随机试听一条语音", false, PreviewVoice);
            menu.Items.Add(voiceMenu);

            MenuItem focusMenu = new MenuItem { Header = IsFocusActive ? "专注中（点击取消）" : "专注计时" };
            if (IsFocusActive) focusMenu.Click += delegate { focusEnds = DateTime.MinValue; pendingPomodoro = false; character.CurrentMood = Mood.Calm; };
            else
            {
                AddMenu(focusMenu, "25 分钟", false, delegate { StartFocus(25); });
                AddMenu(focusMenu, "50 分钟", false, delegate { StartFocus(50); });
            }
            menu.Items.Add(focusMenu);

            MenuItem sizeMenu = new MenuItem { Header = "角色大小" };
            AddMenu(sizeMenu, "80%", Near(Settings.Scale, 0.8), delegate { ApplyScale(0.8); });
            AddMenu(sizeMenu, "100%", Near(Settings.Scale, 1.0), delegate { ApplyScale(1.0); });
            AddMenu(sizeMenu, "125%", Near(Settings.Scale, 1.25), delegate { ApplyScale(1.25); });
            AddMenu(sizeMenu, "150%", Near(Settings.Scale, 1.5), delegate { ApplyScale(1.5); });
            menu.Items.Add(sizeMenu);

            MenuItem opacityMenu = new MenuItem { Header = "透明度" };
            AddMenu(opacityMenu, "60%", Near(Settings.Opacity, 0.6), delegate { ApplyOpacity(0.6); });
            AddMenu(opacityMenu, "80%", Near(Settings.Opacity, 0.8), delegate { ApplyOpacity(0.8); });
            AddMenu(opacityMenu, "100%", Near(Settings.Opacity, 1.0), delegate { ApplyOpacity(1.0); });
            menu.Items.Add(opacityMenu);

            AddMenu(menu, "总在最前", Settings.Topmost, delegate { Settings.Topmost = !Settings.Topmost; Topmost = Settings.Topmost; bubble.Topmost = Settings.Topmost; SaveSettings(); });
            AddMenu(menu, "开机启动", IsStartupEnabled(), delegate { SetStartup(!IsStartupEnabled()); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "隐藏到托盘", false, delegate { Hide(); bubble.Hide(); });
            AddMenu(menu, "退出", false, Quit);
            return menu;
        }

        private static void AddMenu(ItemsControl parent, string header, bool isChecked, Action action)
        {
            MenuItem item = new MenuItem { Header = header, IsCheckable = isChecked, IsChecked = isChecked };
            item.Click += delegate { action(); };
            parent.Items.Add(item);
        }

        private void StartFocus(int minutes)
        {
            focusEnds = DateTime.Now.AddMinutes(minutes);
            pendingPomodoro = false;
            character.CurrentMood = Mood.Focused;
            character.CurrentAction = "focused";
            bubble.Dismiss();
        }

        private void PreviewVoice()
        {
            Settings.SoundEnabled = true;
            SaveSettings();
            audio.TryPlayRandom(VoiceTrigger.Click, true, Settings.SoundVolume);
        }

        private void FinishInteraction()
        {
            if (behaviorState == PetBehaviorState.Dragging) return;
            SetBehaviorState(PetBehaviorState.Idle);
            character.CurrentAction = "idle";
            PlayAnimation("idle", true, null);
        }

        /// <summary>统一切换行为状态；离开 WALK/SITTING 时同步清空位置更新数据。</summary>
        private void SetBehaviorState(PetBehaviorState next)
        {
            if (behaviorState == PetBehaviorState.Walk && next != PetBehaviorState.Walk)
            {
                walking = false;
                velocityX = velocityY = 0;
                walkTargetX = walkTargetY = Double.NaN;
                hasWalkArea = false;
            }
            if (behaviorState == PetBehaviorState.Sitting && next != PetBehaviorState.Sitting)
                sitting = false;
            behaviorState = next;
        }

        private static bool IsInteractionState(PetBehaviorState state)
        {
            return state == PetBehaviorState.Dragging || state == PetBehaviorState.Greeting || state == PetBehaviorState.Interaction || state == PetBehaviorState.HeadTilt || state == PetBehaviorState.Talking || state == PetBehaviorState.Sitting;
        }

        private void ApplyScale(double scale)
        {
            Point center = new Point(Left + Width / 2, Top + Height / 2);
            Settings.Scale = scale;
            Width = CellWidth * scale;
            Height = CellHeight * scale;
            Left = center.X - Width / 2;
            Top = center.Y - Height / 2;
            ClampToVisibleArea();
            SaveSettings();
        }

        private void ApplyOpacity(double value)
        {
            Settings.Opacity = value;
            Opacity = value;
            SaveSettings();
        }

        private void ClampToVisibleArea()
        {
            Rect area = GetCurrentWorkArea();
            ClampPositionToArea(area);
        }

        private void ClampPositionToArea(Rect area)
        {
            Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
        }

        private Rect GetPetBounds() { return new Rect(Left, Top, Width, Height); }

        private double DeviceScaleX()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            return source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M11 : 1.0;
        }

        private double DeviceScaleY()
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            return source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M22 : 1.0;
        }

        private Forms.NotifyIcon CreateTrayIcon()
        {
            Forms.NotifyIcon icon = new Forms.NotifyIcon();
            icon.Text = "Yukino Pet";
            try { icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { icon.Icon = System.Drawing.SystemIcons.Application; }
            icon.Visible = true;
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示桌宠", null, delegate { Dispatcher.BeginInvoke(new Action(delegate { Show(); Activate(); })); });
            menu.Items.Add("退出", null, delegate { Dispatcher.BeginInvoke(new Action(Quit)); });
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(delegate { if (IsVisible) Hide(); else { Show(); Activate(); } })); };
            return icon;
        }

        private void SaveSettings()
        {
            Settings.Left = Left;
            Settings.Top = Top;
            Settings.Save();
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    return key != null && key.GetValue(StartupName) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enabled) key.SetValue(StartupName, "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                    else key.DeleteValue(StartupName, false);
                }
            }
            catch { }
        }

        private void Quit()
        {
            if (closing) return;
            closing = true;
            RequestQuote(QuoteTrigger.Farewell, true);
            SaveSettings();
            Close();
            application.Shutdown();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            bubble.Close();
            audio.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        private static bool Near(double a, double b) { return Math.Abs(a - b) < 0.01; }

        private static BitmapSource LoadResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Missing resource: " + name);
                PngBitmapDecoder decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource source = decoder.Frames[0];
                source.Freeze();
                return source;
            }
        }
    }
}
