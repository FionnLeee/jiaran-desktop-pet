using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace JiaranPet {
    public sealed class PetWindow:Window {
        readonly PetEngine pet=new PetEngine();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly string root=AppDomain.CurrentDomain.BaseDirectory;
        readonly string configDir;
        readonly bool testing;
        readonly JavaScriptSerializer json=new JavaScriptSerializer();
        readonly AssetPack assets;
        readonly AudioService audio;
        readonly CodexWatcher watcher=new CodexWatcher();
        readonly DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(33) };
        readonly DispatcherTimer clickTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime) };
        readonly Grid canvas=new Grid();
        readonly Image sprite=new Image { Stretch=Stretch.Uniform,RenderTransformOrigin=new Point(.5,1),Cursor=Cursors.Hand };
        readonly Border bubble=new Border { CornerRadius=new CornerRadius(17),Padding=new Thickness(13,10,13,10),Background=new SolidColorBrush(Color.FromArgb(247,255,251,240)),BorderBrush=new SolidColorBrush(Color.FromRgb(238,183,143)),BorderThickness=new Thickness(1),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(3,2,3,7) };
        readonly TextBlock bubbleText=new TextBlock { TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,FontSize=12.5,Foreground=new SolidColorBrush(Color.FromRgb(99,65,55)),FontFamily=new FontFamily("Microsoft YaHei UI"),MaxHeight=74 };
        readonly Border statusPill=new Border { HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,CornerRadius=new CornerRadius(11),Background=new SolidColorBrush(Color.FromArgb(226,255,250,230)),Padding=new Thickness(9,3,9,3),Margin=new Thickness(0,0,0,3) };
        readonly TextBlock statusText=new TextBlock { FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=10,Foreground=new SolidColorBrush(Color.FromRgb(117,75,56)) };
        readonly TranslateTransform motion=new TranslateTransform();
        readonly ScaleTransform scale=new ScaleTransform(1,1);
        Forms.NotifyIcon tray;
        IntPtr handle;
        HwndSource source;
        bool pressed,closing,watchBusy,followCodex=true,autoWalk,menuOpen,quiet,showStatus=true,visiblePet=true;
        int pressX,pressY,windowX,windowY,clickCount,playedSequence=-1;
        int? savedPhysicalX,savedPhysicalY;
        double nextPoll,nextWalk=25,walkUntil,nextSave=30,previousTime,petWidth=220;
        BitmapSource lastBitmap;
        string runtimeError;
        public PetWindow(bool qa=false) {
            testing=qa;assets=new AssetPack(Path.Combine(root,"assets"));audio=new AudioService(root);
            string parent=Directory.GetParent(root.TrimEnd(Path.DirectorySeparatorChar)).FullName;
            configDir=Environment.GetEnvironmentVariable("JIARAN_CONFIG_DIR");
            if(string.IsNullOrWhiteSpace(configDir))configDir=Path.GetFileName(parent)=="JiaranDesktopPet"?parent:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JiaranDesktopPet");
            Title="嘉然 · 番茄炒蛋桌宠";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;Topmost=!testing;ShowInTaskbar=false;ShowActivated=false;
            UseLayoutRounding=true;SnapsToDevicePixels=true;FontFamily=new FontFamily("Microsoft YaHei UI");
            Content=canvas;canvas.RowDefinitions.Add(new RowDefinition { Height=new GridLength(86) });canvas.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });canvas.RowDefinitions.Add(new RowDefinition { Height=new GridLength(25) });
            bubble.Child=bubbleText;canvas.Children.Add(bubble);Grid.SetRow(sprite,1);canvas.Children.Add(sprite);statusPill.Child=statusText;Grid.SetRow(statusPill,2);canvas.Children.Add(statusPill);
            var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(motion);sprite.RenderTransform=transforms;
            RenderOptions.SetBitmapScalingMode(sprite,BitmapScalingMode.HighQuality);
            LoadSettings();SetSize(petWidth,false);
            SourceInitialized+=delegate { handle=new WindowInteropHelper(this).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x80|0x08000000);source=HwndSource.FromHwnd(handle);source.AddHook(WindowMessage);if(savedPhysicalX.HasValue)Native.Move(handle,savedPhysicalX.Value,savedPhysicalY.Value); };
            Loaded+=delegate { Clamp();pet.Say("经典常服换好啦 ♡  拖动、摸摸头或右键试试",Now,7);Render();if(!testing){SetupTray();timer.Start();SystemEvents.DisplaySettingsChanged+=DisplayChanged;audio.Play("hello",Now);} };
            sprite.MouseLeftButtonDown+=Down;sprite.MouseLeftButtonUp+=Up;
            sprite.LostMouseCapture+=delegate { if(pressed)ReleaseDrag(false); };
            sprite.MouseRightButtonUp+=delegate(object s,MouseButtonEventArgs e){StopWalking();OpenMenu();e.Handled=true;};
            sprite.MouseEnter+=delegate { if(!pet.Sleeping&&!pressed&&pet.Until<=Now&&!quiet)pet.Say("嘉心糖，我在呢～",Now,2.5); };
            sprite.ToolTip="拖动移动 · 单击摸摸头 · 双击元气跳 · 右键互动菜单";
            clickTimer.Tick+=delegate { clickTimer.Stop();pet.Click(Now,false);Render();SaveSettings(); };
            timer.Tick+=Tick;
            Closing+=delegate { closing=true;timer.Stop();clickTimer.Stop();SystemEvents.DisplaySettingsChanged-=DisplayChanged;if(source!=null)source.RemoveHook(WindowMessage);SaveSettings();audio.Dispose();if(tray!=null)tray.Dispose(); };
            Deactivated+=delegate { if(pressed&&!Native.LeftButtonDown())ReleaseDrag(false); };
        }
        double Now { get { return clock.Elapsed.TotalSeconds; } }
        IntPtr WindowMessage(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) { if(msg==0x02E0)Dispatcher.BeginInvoke(new Action(Clamp));return IntPtr.Zero; }
        void DisplayChanged(object sender,EventArgs e) { if(!closing)Dispatcher.BeginInvoke(new Action(()=>{StopWalking();Clamp();SaveSettings();})); }
        void Down(object sender,MouseButtonEventArgs e) {
            StopWalking();clickTimer.Stop();Native.POINT cursor;Native.GetCursorPos(out cursor);Native.RECT rect;Native.GetWindowRect(handle,out rect);
            pressX=cursor.X;pressY=cursor.Y;windowX=rect.Left;windowY=rect.Top;clickCount=e.ClickCount;pressed=true;sprite.CaptureMouse();pet.BeginDrag(pressX,pressY,Now);e.Handled=true;
        }
        void Up(object sender,MouseButtonEventArgs e) { if(pressed){UpdateDrag();ReleaseDrag(true);e.Handled=true;} }
        void ReleaseDrag(bool allowClick) {
            if(!pressed)return;pressed=false;bool moved=pet.EndDrag(Now);sprite.ReleaseMouseCapture();Clamp();
            if(allowClick&&!moved){if(clickCount>=2){clickTimer.Stop();pet.Click(Now,true);}else clickTimer.Start();}
            SaveSettings();
        }
        void UpdateDrag() { Native.POINT cursor;if(pressed&&Native.GetCursorPos(out cursor)&&pet.MoveDrag(cursor.X,cursor.Y,Now))Native.Move(handle,windowX+cursor.X-pressX,windowY+cursor.Y-pressY); }
        void Tick(object sender,EventArgs e) {
            double now=Now,dt=Math.Min(.1,Math.Max(0,now-previousTime));previousTime=now;pet.Tick(now);
            if(pressed){if(!Native.LeftButtonDown())ReleaseDrag(false);else UpdateDrag();}
            if(!pressed&&!menuOpen&&autoWalk&&pet.CanWalk(now)&&visiblePet) {
                if(walkUntil>now){Native.RECT rect;Native.GetWindowRect(handle,out rect);var area=Forms.Screen.FromHandle(handle).WorkingArea;int width=rect.Right-rect.Left,x=rect.Left+(int)Math.Round(pet.Direction*55*dt);if(x<area.Left||x+width>area.Right){pet.Direction*=-1;x=Math.Max(area.Left,Math.Min(area.Right-width,x));}Native.Move(handle,x,rect.Top);}
                else if(pet.State=="walk"){pet.EndWalk(now);nextWalk=now+40;}
                else if(now>=nextWalk){pet.Walk(now,-pet.Direction);walkUntil=now+4;}
            }else if(!pressed&&pet.State=="walk")StopWalking();
            Render();
            if(pet.SoundSequence!=playedSequence){playedSequence=pet.SoundSequence;if(!testing&&pet.SoundEvent!=null&&visiblePet)audio.Play(pet.SoundEvent,now);}
            if(now>=nextSave){nextSave=now+30;SaveSettings();}
            if(!testing&&followCodex&&!watchBusy&&now>=nextPoll){nextPoll=now+3;watchBusy=true;Task.Run(()=>watcher.Poll()).ContinueWith(result=>Dispatcher.BeginInvoke(new Action(()=>{watchBusy=false;if(closing||!followCodex)return;if(result.Status==TaskStatus.RanToCompletion&&result.Result!=null){StopWalking();pet.SetTask(result.Result,Now);SaveSettings();}})));}
        }
        void StopWalking() { walkUntil=0;nextWalk=Now+40;pet.EndWalk(Now); }
        void Render() {
            try {
                int gaze=-1;if(!testing&&!pressed&&!menuOpen&&!pet.Paused&&!pet.Sleeping&&pet.State=="idle"&&pet.Until<=Now){Native.POINT cursor;if(Native.GetCursorPos(out cursor)){Point center=sprite.PointToScreen(new Point(sprite.ActualWidth/2,sprite.ActualHeight*.36));double dx=cursor.X-center.X,dy=cursor.Y-center.Y;if(dx*dx+dy*dy<260*260)gaze=PetEngine.GazeIndex(dx,dy);}}
                var bitmap=assets.Get(pet.State,pet.AnimationAge,pet.Direction,gaze);if(bitmap!=lastBitmap){sprite.Source=bitmap;lastBitmap=bitmap;}
                // Visual motion remains attached to the feet; no mirroring of hair clips or costume details.
                scale.ScaleX=1;scale.ScaleY=1;motion.X=0;motion.Y=0;
                if(pet.State=="complete")motion.Y=-Math.Abs(Math.Sin(pet.AnimationAge*4))*7;
                bubbleText.Text=pet.Message;bubble.Visibility=!quiet&&Now<pet.MessageUntil?Visibility.Visible:Visibility.Hidden;
                statusText.Text=pet.FocusActive?(pet.Resting?"休息 ":"专注 ")+FormatTime(pet.FocusRemaining)+(pet.FocusPaused?" · 暂停":""):pet.State=="sleep"?"晚安 ♡":pet.RemoteState=="working"?"陪 Codex 干活":"嘉心糖 "+pet.Candies+" ♡";
                statusPill.Visibility=showStatus?Visibility.Visible:Visibility.Hidden;
            }catch(Exception ex){runtimeError=ex.Message;timer.Stop();bubbleText.Text="素材加载失败，请保留完整 assets 文件夹。";bubble.Visibility=Visibility.Visible;Log(ex);}
        }
        static string FormatTime(double seconds) { int value=(int)Math.Ceiling(Math.Max(0,seconds));return (value/60).ToString("00")+":"+(value%60).ToString("00"); }
        void Act(string state,string text,double duration=4) { clickTimer.Stop();StopWalking();pet.Act(state,text,Now,duration);Render();SaveSettings(); }
        MenuItem Item(string label,Action action) { var item=new MenuItem { Header=label,Padding=new Thickness(7,5,7,5) };item.Click+=delegate{action();};return item; }
        MenuItem Check(string label,bool value,Action action) { var item=Item(label,action);item.IsCheckable=true;item.IsChecked=value;return item; }
        MenuItem Group(string label) { return new MenuItem { Header=label,Padding=new Thickness(7,5,7,5) }; }
        void OpenMenu() {
            var menu=new ContextMenu { FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=13,Background=new SolidColorBrush(Color.FromRgb(255,251,242)),Foreground=new SolidColorBrush(Color.FromRgb(91,61,50)),BorderBrush=new SolidColorBrush(Color.FromRgb(236,189,152)),BorderThickness=new Thickness(1),Padding=new Thickness(6) };
            menu.Items.Add(new MenuItem { Header="嘉然 · 番茄炒蛋常服",IsEnabled=false,FontWeight=FontWeights.Bold });menu.Items.Add(new MenuItem { Header="嘉心糖 "+pet.Candies+" 颗  ·  专注 "+pet.FocusSessions+" 次",IsEnabled=false,FontSize=11 });menu.Items.Add(new Separator());
            menu.Items.Add(Item("摸摸头",()=>{StopWalking();pet.Click(Now,false);}));menu.Items.Add(Item("挥手打招呼",()=>Act("wave","你好呀，今天也一起加油！")));
            menu.Items.Add(Item("元气跳跃",()=>Act("jump","看我跳一下！",2.8)));menu.Items.Add(Item("喂一颗嘉心糖",()=>{StopWalking();pet.GiveCandy(Now);SaveSettings();}));
            menu.Items.Add(Item(pet.Sleeping?"叫醒嘉然":"让嘉然睡一会儿",()=>{StopWalking();if(pet.Sleeping)pet.Wake(Now);else Act("sleep","我眯一会儿。点我就会醒来 ♡",5);}));
            var focus=Group("一起专注");foreach(int minutes in new[]{15,25,45}){int value=minutes;focus.Items.Add(Item(value+" 分钟专注",()=>{StopWalking();pet.StartFocus(Now,value);SaveSettings();}));}focus.Items.Add(Item("休息 5 分钟",()=>{StopWalking();pet.StartFocus(Now,5,true);}));
            if(pet.FocusActive){focus.Items.Add(Item(pet.FocusPaused?"继续计时":"暂停计时",()=>{pet.FocusPaused=!pet.FocusPaused;SaveSettings();}));focus.Items.Add(Item("结束本轮计时",()=>pet.StopFocus(Now)));}menu.Items.Add(focus);
            var moods=Group("陪伴心情");moods.Items.Add(Item("一起写代码",()=>Act("working","先完成一个小目标，我陪你。",12)));moods.Items.Add(Item("下一步做什么",()=>Act("waiting","先选一件最重要的小事，慢慢来。",8)));moods.Items.Add(Item("一起庆祝",()=>Act("complete","完成啦，辛苦了 ♡",7)));moods.Items.Add(Item("陪你 Debug",()=>Act("failed","先复现问题，再一点点排查。",7)));moods.Items.Add(Item("回到待机",()=>{StopWalking();pet.Idle(Now);}));menu.Items.Add(moods);menu.Items.Add(new Separator());
            var options=Group("陪伴设置");options.Items.Add(Check("跟随本地 Codex 任务",followCodex,()=>{followCodex=!followCodex;pet.SetTask(followCodex&&(watcher.LastState=="working"||watcher.LastState=="waiting")?watcher.LastState:"idle",Now,false);nextPoll=0;SaveSettings();}));options.Items.Add(Check("偶尔在桌面散步",autoWalk,()=>{autoWalk=!autoWalk;StopWalking();SaveSettings();}));options.Items.Add(Check("显示对白",!quiet,()=>{quiet=!quiet;SaveSettings();}));options.Items.Add(Check("显示状态小标签",showStatus,()=>{showStatus=!showStatus;SaveSettings();}));options.Items.Add(Check("播放本地语音（需自备音频）",audio.Enabled,()=>{audio.Enabled=!audio.Enabled;if(!audio.Enabled)audio.Stop();else if(!testing)audio.Play("hello",Now,true);SaveSettings();}));
            if(!watcher.Available||watcher.Error!=null)options.Items.Add(new MenuItem { Header="本地 Codex 状态暂不可用",IsEnabled=false,FontSize=11 });
            options.Items.Add(new MenuItem { Header=audio.Status,IsEnabled=false,FontSize=11 });var volume=Group("语音音量");foreach(int percent in new[]{25,50,75,100}){int value=percent;volume.Items.Add(Check(value+"%",Math.Abs(audio.Volume-value/100.0)<.13,()=>{audio.Volume=value/100.0;SaveSettings();}));}options.Items.Add(volume);
            options.Items.Add(Check("暂停动画",pet.Paused,()=>{pet.Paused=!pet.Paused;StopWalking();SaveSettings();}));options.Items.Add(Check("保持置顶",Topmost,()=>{Topmost=!Topmost;SaveSettings();}));menu.Items.Add(options);
            var size=Group("桌宠大小");foreach(int px in new[]{160,220,300}){int value=px;size.Items.Add(Check(px==160?"小巧 · 160":px==220?"标准 · 220":"大号 · 300",Math.Abs(petWidth-px)<1,()=>{SetSize(value,true);SaveSettings();}));}menu.Items.Add(size);
            var monitors=Group("移到显示器");int index=0;foreach(var screen in Forms.Screen.AllScreens){var display=screen;index++;monitors.Items.Add(Item("显示器 "+index+(display.Primary?" · 主屏":""),()=>MoveTo(display)));}menu.Items.Add(monitors);
            menu.Items.Add(Item("找回嘉然位置",()=>MoveTo(CursorScreen())));menu.Items.Add(Item("暂时隐藏（托盘可找回）",HidePet));menu.Items.Add(Item("查看使用说明",()=>{string path=Path.Combine(root,"使用说明.txt");if(File.Exists(path))Process.Start(path);}));
            if(runtimeError!=null)menu.Items.Add(new MenuItem { Header="素材错误："+runtimeError,IsEnabled=false });menu.Items.Add(new Separator());menu.Items.Add(Item("退出嘉然桌宠",Close));
            menu.Opened+=delegate { menuOpen=true; };menu.Closed+=delegate { menuOpen=false;nextWalk=Now+40; };
            menu.PlacementTarget=sprite;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint;menu.IsOpen=true;
        }
        void SetSize(double width,bool preserveBottom) {
            Native.RECT rect=new Native.RECT();bool has=preserveBottom&&handle!=IntPtr.Zero&&Native.GetWindowRect(handle,out rect);
            petWidth=Math.Max(120,Math.Min(360,width));Width=petWidth+12;Height=111+petWidth*assets.CellHeight/assets.CellWidth;
            if(IsLoaded){UpdateLayout();if(has){Native.RECT next;Native.GetWindowRect(handle,out next);Native.Move(handle,rect.Left,rect.Bottom-(next.Bottom-next.Top));}Clamp();}
        }
        Forms.Screen CursorScreen() { Native.POINT p;return Native.GetCursorPos(out p)?Forms.Screen.FromPoint(new System.Drawing.Point(p.X,p.Y)):Forms.Screen.PrimaryScreen; }
        void MoveTo(Forms.Screen screen) { StopWalking();ShowPet();Native.RECT rect;Native.GetWindowRect(handle,out rect);var area=screen.WorkingArea;Native.Move(handle,area.Right-(rect.Right-rect.Left)-24,area.Bottom-(rect.Bottom-rect.Top)-16);Clamp();SaveSettings(); }
        void Clamp() {
            if(handle==IntPtr.Zero)return;Native.RECT rect;if(!Native.GetWindowRect(handle,out rect))return;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;var selected=Forms.Screen.FromRectangle(new System.Drawing.Rectangle(rect.Left,rect.Top,width,height));var area=selected.WorkingArea;
            int x=Geometry.ClampCoordinate(rect.Left,width,area.Left,area.Right),y=Geometry.ClampCoordinate(rect.Top,height,area.Top,area.Bottom);Native.Move(handle,x,y);
        }
        void HidePet() { visiblePet=false;StopWalking();audio.Stop();Hide(); }
        void ShowPet() { visiblePet=true;Show();WindowState=WindowState.Normal;Clamp(); }
        void SetupTray() {
            tray=new Forms.NotifyIcon { Text="嘉然 · 番茄炒蛋常服",Icon=File.Exists(Path.Combine(root,"jiaran.ico"))?new System.Drawing.Icon(Path.Combine(root,"jiaran.ico")):System.Drawing.SystemIcons.Information };
            var menu=new Forms.ContextMenuStrip();menu.Font=new System.Drawing.Font("Microsoft YaHei UI",10);menu.Items.Add("显示嘉然 / 找回位置",null,delegate{Dispatcher.BeginInvoke(new Action(()=>MoveTo(CursorScreen())));});menu.Items.Add("互动菜单",null,delegate{Dispatcher.BeginInvoke(new Action(()=>{ShowPet();OpenMenu();}));});menu.Items.Add("隐藏嘉然",null,delegate{Dispatcher.BeginInvoke(new Action(HidePet));});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出",null,delegate{Dispatcher.BeginInvoke(new Action(Close));});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Dispatcher.BeginInvoke(new Action(()=>MoveTo(CursorScreen())));};tray.Visible=true;
        }
        void LoadSettings() {
            Left=SystemParameters.WorkArea.Right-272;Top=SystemParameters.WorkArea.Bottom-380;if(testing)return;
            try {
                string path=Path.Combine(configDir,"settings.json");if(!File.Exists(path))return;var c=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));object v;
                if(c.TryGetValue("physicalX",out v)){savedPhysicalX=Convert.ToInt32(v);savedPhysicalY=c.ContainsKey("physicalY")?Convert.ToInt32(c["physicalY"]):0;}else {if(c.TryGetValue("x",out v))Left=Convert.ToDouble(v);if(c.TryGetValue("y",out v))Top=Convert.ToDouble(v);}
                if(c.TryGetValue("size",out v))petWidth=Math.Max(120,Math.Min(360,Convert.ToDouble(v)));if(c.TryGetValue("candies",out v))pet.Candies=Math.Max(0,Math.Min(999999,Convert.ToInt32(v)));if(c.TryGetValue("focusSessions",out v))pet.FocusSessions=Math.Max(0,Convert.ToInt32(v));
                if(c.TryGetValue("followCodex",out v))followCodex=Convert.ToBoolean(v);if(c.TryGetValue("wander",out v))autoWalk=Convert.ToBoolean(v);if(c.TryGetValue("quiet",out v))quiet=Convert.ToBoolean(v);if(c.TryGetValue("topmost",out v))Topmost=Convert.ToBoolean(v);if(c.TryGetValue("showStatus",out v))showStatus=Convert.ToBoolean(v);if(c.TryGetValue("voiceEnabled",out v))audio.Enabled=Convert.ToBoolean(v);if(c.TryGetValue("voiceVolume",out v))audio.Volume=Math.Max(0,Math.Min(1,Convert.ToDouble(v)));
            }catch(Exception ex){Log(ex);}
        }
        void SaveSettings() {
            if(testing||handle==IntPtr.Zero)return;
            try { Directory.CreateDirectory(configDir);Native.RECT rect;Native.GetWindowRect(handle,out rect);string path=Path.Combine(configDir,"settings.json"),tmp=path+".tmp";File.WriteAllText(tmp,json.Serialize(new { schemaVersion=3,x=Left,y=Top,physicalX=rect.Left,physicalY=rect.Top,size=petWidth,candies=pet.Candies,focusSessions=pet.FocusSessions,followCodex=followCodex,wander=autoWalk,quiet=quiet,topmost=Topmost,showStatus=showStatus,voiceEnabled=audio.Enabled,voiceVolume=audio.Volume }),System.Text.Encoding.UTF8);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path); }catch(Exception ex){Log(ex);}
        }
        void Log(Exception ex) { if(testing)return;try { Directory.CreateDirectory(configDir);File.WriteAllText(Path.Combine(configDir,"last-error.txt"),DateTime.Now.ToString("o")+"\n"+ex); }catch(Exception){} }
        void SaveRender(string path) {
            canvas.Measure(new Size(Width,Height));canvas.Arrange(new Rect(0,0,Width,Height));canvas.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(Width),(int)Math.Ceiling(Height),96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))png.Save(stream);
        }
        public void ExportQA(string directory) {
            Directory.CreateDirectory(directory);assets.Validate();var results=new List<string>();
            foreach(string state in new[]{"idle","wave","jump","working","waiting","complete","failed","candy","sleep","walk"}) {
                pet.Act(state,state+" · 嘉然经典常服",Now,20);pet.Tick(Now+1.1);Render();if(runtimeError!=null)throw new Exception(runtimeError);
                SaveRender(Path.Combine(directory,state+".png"));results.Add(state+": rendered");
            }
            pet.Act("idle","鼠标视线方向 · 嘉然经典常服",Now,20);Render();
            for(int i=0;i<16;i++){sprite.Source=assets.Get("idle",0,-1,i);SaveRender(Path.Combine(directory,"look-"+i.ToString("00")+".png"));}
            results.Add("sixteen gaze directions rendered through WPF");
            pet.Act("jump","真实逐帧跳跃 · 嘉然经典常服",Now,20);Render();
            for(int i=0;i<5;i++){sprite.Source=assets.Get("jump",0,-1,-1,i);SaveRender(Path.Combine(directory,"jump-frame-"+i+".png"));}
            results.Add("five jump frames rendered without artificial jump translation");
            foreach(int width in new[]{160,220,300}){SetSize(width,false);lastBitmap=null;pet.Act("wave","你好呀，今天也一起加油！",Now,20);Render();SaveRender(Path.Combine(directory,"size-"+width+".png"));}
            results.Add("all three display sizes rendered from "+assets.CellWidth+"x"+assets.CellHeight+" source frames");
            SetSize(220,false);
            Native.RECT before,after;Native.GetWindowRect(handle,out before);Native.Move(handle,before.Left-40,before.Top-20);Native.GetWindowRect(handle,out after);if(after.Left!=before.Left-40||after.Top!=before.Top-20)throw new Exception("Window movement failed");results.Add("native physical-pixel movement verified");
            Native.Move(handle,-50000,-50000);Clamp();Native.GetWindowRect(handle,out after);var area=Forms.Screen.FromHandle(handle).WorkingArea;if(after.Left<area.Left||after.Top<area.Top||after.Right>area.Right||after.Bottom>area.Bottom)throw new Exception("Screen recovery failed");results.Add("offscreen position recovered inside monitor work area");results.Add("monitor count: "+Forms.Screen.AllScreens.Length);File.WriteAllText(Path.Combine(directory,"render-tests.txt"),"PASS\n"+string.Join("\n",results));
        }
    }
    public static class Geometry {
        public static int ClampCoordinate(int position,int length,int start,int end) { return Math.Max(start,Math.Min(end-Math.Min(length,end-start),position)); }
        public static void SelfTest(string output) { if(ClampCoordinate(-2500,220,-1920,0)!=-1920||ClampCoordinate(-100,220,-1920,0)!=-220||ClampCoordinate(10,2500,0,1920)!=0||ClampCoordinate(2000,220,1920,3840)!=2000)throw new Exception("Monitor geometry failed");File.WriteAllText(output,"PASS\nnegative-origin display\nright edge bounds\noversized window recovery\nsecondary display origin\n"); }
    }
    static class Native {
        [StructLayout(LayoutKind.Sequential)]public struct POINT { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)]public struct RECT { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll")]public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr handle,out RECT rect);
        [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")]public static extern int GetWindowLong(IntPtr handle,int index);
        [DllImport("user32.dll")]public static extern int SetWindowLong(IntPtr handle,int index,int value);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        public static bool LeftButtonDown() { return (GetAsyncKeyState(1)&0x8000)!=0; }
        public static void Move(IntPtr handle,int x,int y) { SetWindowPos(handle,IntPtr.Zero,x,y,0,0,0x15); }
    }
    static class Program {
        [STAThread]public static void Main(string[] args) {
            try {
                if(args.Length>1&&args[0]=="--self-test"){Directory.CreateDirectory(args[1]);PetEngine.SelfTest(Path.Combine(args[1],"engine-tests.txt"));CodexWatcher.SelfTest(Path.Combine(args[1],"watcher-tests.txt"));Geometry.SelfTest(Path.Combine(args[1],"geometry-tests.txt"));AssetPack.SelfTest(Path.Combine(args[1],"asset-fixtures"),Path.Combine(args[1],"asset-tests.txt"));return;}
                if(args.Length>1&&args[0]=="--watcher-probe"){var watcher=new CodexWatcher();string state=watcher.Poll();File.WriteAllText(args[1],"state="+(state??watcher.LastState)+"\navailable="+watcher.Available+"\nerror="+(watcher.Error??"none"));return;}
                if(args.Length>1&&args[0]=="--audio-qa"){AudioService.ExportQA(AppDomain.CurrentDomain.BaseDirectory,args[1]);return;}
                if(args.Length>1&&args[0]=="--asset-qa"){var assets=new AssetPack(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"));assets.Validate();File.WriteAllText(args[1],"PASS\nall action frames loaded\ncell="+assets.CellWidth+"x"+assets.CellHeight);return;}
                bool renderQA=args.Length>1&&args[0]=="--render-qa",created;
                using(var mutex=new Mutex(true,renderQA?"Local\\JiaranDesktopPetV3QA":"Local\\JiaranDesktopPet",out created)) {
                    if(!created)return;
                    var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };var window=new PetWindow(renderQA);
                    if(renderQA)window.ContentRendered+=delegate { window.ExportQA(args[1]);window.Close(); };
                    app.Run(window);
                }
            }catch(Exception ex) {
                if(args.Length>1){try{string output=Directory.Exists(args[1])?Path.Combine(args[1],"last-error.txt"):args[1]+".error.txt";File.WriteAllText(output,ex.ToString());}catch(Exception){}Environment.ExitCode=1;return;}
                string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JiaranDesktopPet");try{Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"last-error.txt"),ex.ToString());}catch(Exception){}MessageBox.Show("嘉然启动遇到问题："+ex.Message+"\n错误记录："+directory,"嘉然桌宠");
            }
        }
    }
}
