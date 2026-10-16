using System;
using System.Collections.Generic;
using System.IO;

namespace JiaranPet {
    public sealed class PetEngine {
        public string State { get; private set; }
        public string RemoteState { get; private set; }
        public string Message { get; private set; }
        public string SoundEvent { get; private set; }
        public int SoundSequence { get; private set; }
        public int Candies, FocusSessions, Direction = -1;
        public bool Paused, FocusPaused;
        public bool Dragging { get; private set; }
        public bool Sleeping { get; private set; }
        public bool FocusActive { get; private set; }
        public bool Resting { get; private set; }
        public double MessageUntil { get; private set; }
        public double Until { get; private set; }
        public double FocusRemaining { get; private set; }
        public double AnimationAge { get { return Math.Max(0, visualClock-stateStarted); } }
        public double LastInteraction { get; private set; }
        double previous, visualClock, stateStarted, dragX, dragY, lastDragX, clickAt=-100;
        bool hasClock, manualSleep;
        int clicks;
        string pendingState,pendingMessage,pendingSound;
        double pendingUntil;
        readonly Random random=new Random();
        public PetEngine() { State=RemoteState="idle"; Message="按住拖动 · 单击摸摸头 · 双击元气跳 · 右键互动"; }
        void Advance(double now) {
            double dt=hasClock?Math.Max(0,now-previous):0; previous=now; hasClock=true;
            if(!Paused) visualClock+=dt;
            if(FocusActive&&!FocusPaused) {
                FocusRemaining=Math.Max(0,FocusRemaining-dt);
                if(FocusRemaining<=0) {
                    bool wasRest=Resting; FocusActive=false; FocusPaused=false; Resting=false;
                    if(!wasRest) FocusSessions++;
                    Notify(wasRest?"wave":"complete",now,wasRest?"休息好啦，继续按自己的节奏来。":"这一轮专注完成啦！伸伸懒腰，喝一口水。",wasRest?"wake":"complete");
                }
            }
        }
        void Emit(string name) { SoundEvent=name; SoundSequence++; }
        void SetState(string state) { if(State==state)return; State=state;stateStarted=visualClock; }
        void SetAction(string state,double now,double duration) { SetState(state);stateStarted=visualClock;Until=duration>0?now+duration:0; }
        string BaseState { get { return FocusActive?(Resting?"idle":"working"):RemoteState; } }
        public void Say(string message,double now,double duration=4) { Message=message;MessageUntil=now+Math.Max(0,duration); }
        public void Act(string state,string message,double now,double duration=4) {
            Advance(now);LastInteraction=now; Sleeping=state=="sleep"; manualSleep=Sleeping;
            SetAction(state,now,Math.Max(0,duration));Say(message,now,Math.Min(duration,7));Emit(state);
        }
        public void Wake(double now) { Advance(now);Sleeping=manualSleep=false;SetAction("wave",now,3);LastInteraction=now;Say("醒啦！我在这里陪你。",now);Emit("wake"); }
        public void BeginDrag(double x,double y,double now) { Advance(now);dragX=lastDragX=x;dragY=y;Dragging=false;LastInteraction=now; }
        public bool MoveDrag(double x,double y,double now) {
            Advance(now);
            if(!Dragging&&Math.Abs(x-dragX)<6&&Math.Abs(y-dragY)<6)return false;
            if(!Dragging){Dragging=true;Sleeping=manualSleep=false;SetAction("walk",now,0);MessageUntil=0;}
            if(x-lastDragX>=3){Direction=1;lastDragX=x;}else if(x-lastDragX<=-3){Direction=-1;lastDragX=x;}
            LastInteraction=now;return true;
        }
        public bool EndDrag(double now) { Advance(now);bool moved=Dragging;Dragging=false;if(moved){SetState(BaseState);Until=0;LastInteraction=now;}return moved; }
        public void Click(double now,bool doubleClick) {
            Advance(now);LastInteraction=now;
            if(Sleeping){Wake(now);return;}
            clicks=now-clickAt<1.25?clicks+(doubleClick?2:1):(doubleClick?2:1);clickAt=now;
            if(clicks>=4){Act("failed","轻一点啦，嘉然要晕啦～",now,3);clicks=0;return;}
            if(doubleClick){Act("jump","接住这份元气！",now,2.8);return;}
            string[] lines={"摸摸头收到啦 ♡","我在呢，今天也一起加油！","嘉心糖，记得照顾好自己呀。","嘿嘿，继续陪你。"};
            Act("wave",lines[random.Next(lines.Length)],now,3.5);
        }
        public void GiveCandy(double now) { Candies=Math.Min(999999,Candies+1);Act("candy","谢谢你的嘉心糖 ♡  已收集 "+Candies+" 颗",now,5); }
        public void StartFocus(double now,int minutes,bool rest=false) {
            Advance(now);FocusActive=true;FocusPaused=false;Resting=rest;FocusRemaining=Math.Max(1,minutes)*60;
            Sleeping=manualSleep=false;LastInteraction=now;Until=0;SetState(rest?"idle":"working");
            Say(rest?"休息 "+minutes+" 分钟。眺望远处，放松一下～":"专注 "+minutes+" 分钟。先做一件小事，我陪你。",now,5);Emit(rest?"rest":"working");
        }
        public void StopFocus(double now) { Advance(now);FocusActive=FocusPaused=Resting=false;FocusRemaining=0;if(!Dragging&&!Sleeping&&Until<=now){Until=0;SetState(BaseState);}Say("专注计时已结束，按照自己的节奏来。",now); }
        public void SetTask(string status,double now,bool announce=true) {
            Advance(now);if(status!="working"&&status!="waiting"&&status!="complete"&&status!="failed"&&status!="idle")return;
            if(status=="complete"||status=="failed") {
                RemoteState="idle";
                if(!announce)return;
                if(Sleeping&&!manualSleep)Sleeping=false;
                Notify(status,now);return;
            }
            bool changed=RemoteState!=status;RemoteState=status;
            if(Sleeping&&!manualSleep&&status=="working")Sleeping=false;
            if(!Dragging&&!Sleeping&&!FocusActive&&Until<=now){SetState(BaseState);Until=0;if(changed&&announce&&status=="working"){Say("Codex 开始干活啦，我陪你一起想。",now);Emit("working");}}
        }
        void Notify(string status,double now,string message=null,string sound=null) {
            if(Dragging||FocusActive||Sleeping||Until>now){pendingState=status;pendingMessage=message;pendingSound=sound;pendingUntil=now+20;return;}
            TaskAction(status,now,message,sound);
        }
        void TaskAction(string status,double now,string message=null,string sound=null) { LastInteraction=now;SetAction(status,now,status=="complete"?7:6);Say(message??(status=="complete"?"Codex 这一轮完成啦！辛苦了 ♡":"遇到问题了，先复现，再一点点排查。"),now,6);Emit(sound??status); }
        public void Idle(double now) { Advance(now);Sleeping=manualSleep=false;Until=0;SetState(BaseState);LastInteraction=now;Say("我在这里陪你。",now,3); }
        public bool CanWalk(double now) { return !Dragging&&!Sleeping&&!Paused&&!FocusActive&&RemoteState=="idle"&&Until<=now; }
        public void Walk(double now,int direction) { Advance(now);Direction=direction;SetState("walk"); }
        public void EndWalk(double now) { Advance(now);if(State=="walk"&&!Dragging)SetState(BaseState); }
        public void Tick(double now) {
            Advance(now);if(Dragging)return;
            if(Until>0&&Until<=now){Until=0;if(!Sleeping)SetState(BaseState);}
            if(pendingState!=null&&now>pendingUntil)pendingState=null;
            if(pendingState!=null&&!Sleeping&&!FocusActive&&Until<=now){string status=pendingState,message=pendingMessage,sound=pendingSound;pendingState=pendingMessage=pendingSound=null;TaskAction(status,now,message,sound);}
            if(!Sleeping&&!FocusActive&&RemoteState=="idle"&&Until==0&&State!="walk"&&now-LastInteraction>240){Sleeping=true;manualSleep=false;SetState("sleep");Say("有点困，点我就会醒来。",now,4);}
        }
        public static int GazeIndex(double dx,double dy) { if(dx*dx+dy*dy<18*18)return -1;double degrees=(Math.Atan2(dx,-dy)*180/Math.PI+360)%360;return ((int)Math.Round(degrees/22.5))%16; }
        public static void SelfTest(string file) {
            var results=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);results.Add(name);};
            var p=new PetEngine();p.BeginDrag(100,100,0);check(!p.MoveDrag(102,103,.1),"pointer jitter remains a click");p.MoveDrag(115,100,.2);check(p.Dragging&&p.Direction==1,"drag threshold and direction");
            p.SetTask("working",.3);p.MoveDrag(90,100,.4);check(p.Direction==-1&&p.State=="walk","task changes cannot interrupt drag");p.EndDrag(.5);check(p.State=="working","release restores current task");
            p.GiveCandy(1);p.SetTask("complete",2);check(p.State=="candy","completion defers until interaction ends");p.Tick(6.1);check(p.State=="complete","deferred completion is presented");p.Tick(14);check(p.State=="idle","celebration expires into idle");
            p.Act("sleep","",20,4);p.SetTask("working",21);p.Tick(30);check(p.Sleeping&&p.State=="sleep","manual sleep is respected");p.Click(31,false);check(!p.Sleeping&&p.State=="wave","single click wakes pet");p.Tick(35);check(p.State=="working","wake restores running task");
            double age=p.AnimationAge;p.Paused=true;p.Tick(40);check(p.AnimationAge==age,"pause freezes visual time");p.Paused=false;p.Tick(41);check(p.AnimationAge>age,"resume advances visual time");
            p.StartFocus(50,1);p.FocusPaused=true;p.Tick(100);check(p.FocusRemaining==60,"focus pause preserves remaining time");p.FocusPaused=false;p.Tick(160);check(!p.FocusActive&&p.FocusSessions==1&&p.State=="complete","focus expires once");p.Tick(170);check(p.FocusSessions==1,"timer does not repeat reward");
            p=new PetEngine();p.Tick(0);p.Tick(241);check(p.Sleeping,"idle sleep");p.SetTask("working",242);check(!p.Sleeping&&p.State=="working","task wakes automatic sleep");
            p=new PetEngine();p.StartFocus(0,1);p.Act("sleep","",30,4);p.Tick(60);check(!p.FocusActive&&p.Sleeping&&p.State=="sleep","focus expiration respects manual sleep");p.Click(61,false);p.Tick(65);check(p.State=="complete"&&!p.Sleeping,"focus completion appears after waking");p.Tick(73);check(p.State=="idle","focus celebration cannot get stuck after sleep");
            p=new PetEngine();p.StartFocus(0,1);p.BeginDrag(0,0,58);p.MoveDrag(20,0,59);p.Tick(60);check(p.Dragging&&p.State=="walk","focus expiration cannot interrupt dragging");p.EndDrag(61);p.Tick(61);check(p.State=="complete","focus completion appears after drag release");
            p=new PetEngine();p.StartFocus(0,1);p.Act("sleep","",20,4);p.StopFocus(21);p.Tick(30);check(p.Sleeping&&p.State=="sleep","ending focus preserves manual sleep");
            p=new PetEngine();p.Tick(0);p.Tick(241);p.SetTask("complete",242);p.Tick(250);check(!p.Sleeping&&p.State=="idle","task completion wakes automatic sleep without getting stuck");
            check(GazeIndex(0,-100)==0&&GazeIndex(100,0)==4&&GazeIndex(0,100)==8&&GazeIndex(-100,0)==12,"gaze uses screen coordinate directions");
            File.WriteAllText(file,"PASS\n"+string.Join("\n",results));
        }
    }
}
