using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace JiaranPet {
    // Reads local session files, using metadata and lifecycle events only.
    // No network requests, file writes, or persistent conversation storage.
    public sealed class CodexWatcher {
        sealed class Track { public long Offset;public byte[] Pending=new byte[0];public string State="idle";public bool Ignore,MetadataChecked;public DateTime Changed,Seen; }
        readonly Dictionary<string,Track> tracks=new Dictionary<string,Track>();
        readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=4*1024*1024 };
        readonly string sessionsRoot;
        bool initialized;
        public string LastState="idle", Error;
        public bool Available { get { return Directory.Exists(sessionsRoot); } }
        public CodexWatcher(string path=null) {
            string configured=Environment.GetEnvironmentVariable("CODEX_HOME");
            sessionsRoot=path??Path.Combine(string.IsNullOrWhiteSpace(configured)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex"):configured,"sessions");
        }
        public string Poll() {
            try {
                Error=null;if(!Available){Error="未找到本地 Codex sessions";return null;}
                var candidates=new List<string>();
                for(int day=0;day<3;day++){string dir=Path.Combine(sessionsRoot,DateTime.Now.AddDays(-day).ToString("yyyy/MM/dd"));if(Directory.Exists(dir))candidates.AddRange(Directory.GetFiles(dir,"*.jsonl"));}
                foreach(string path in candidates.OrderByDescending(File.GetLastWriteTimeUtc).Take(32)) { try { Read(path); }catch(IOException){}catch(UnauthorizedAccessException){} }
                DateTime now=DateTime.UtcNow;
                foreach(string key in tracks.Where(x=>now-x.Value.Seen>TimeSpan.FromHours(36)).Select(x=>x.Key).ToArray())tracks.Remove(key);
                string next=tracks.Values.Any(t=>!t.Ignore&&t.State=="working"&&now-t.Changed<TimeSpan.FromHours(6))?"working":"idle";
                var newest=tracks.Values.Where(t=>!t.Ignore).OrderByDescending(t=>t.Changed).FirstOrDefault();
                if(next=="idle"&&newest!=null&&now-newest.Changed<TimeSpan.FromSeconds(15))next=newest.State;
                if(!initialized){initialized=true;foreach(var track in tracks.Values)if(track.State=="complete"||track.State=="failed")track.State="idle";if(next=="complete"||next=="failed")next="idle";}
                if(next==LastState)return null;LastState=next;return next;
            }catch(Exception ex){Error=ex.GetType().Name;return null;}
        }
        void Read(string path) {
            Track track;if(!tracks.TryGetValue(path,out track)){track=new Track();tracks[path]=track;}track.Seen=DateTime.UtcNow;if(track.Ignore)return;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                if(stream.Length<track.Offset){track.Offset=0;track.Pending=new byte[0];track.State="idle";track.MetadataChecked=false;}
                if(!track.MetadataChecked){using(var reader=new StreamReader(stream,Encoding.UTF8,true,4096,true)){string line=reader.ReadLine();if(line!=null){try{var meta=json.DeserializeObject(line) as Dictionary<string,object>;track.MetadataChecked=true;object p,s;var payload=meta!=null&&meta.TryGetValue("payload",out p)?p as Dictionary<string,object>:null;var source=payload!=null&&payload.TryGetValue("source",out s)?s as Dictionary<string,object>:null;if(source!=null&&source.ContainsKey("subagent")){track.Ignore=true;return;}}catch(ArgumentException){}}}stream.Position=0;}
                bool trimmed=false;
                if(track.Offset==0&&stream.Length>4*1024*1024){track.Offset=stream.Length-4*1024*1024;trimmed=true;}
                stream.Position=track.Offset;int remaining=(int)Math.Min(4*1024*1024,stream.Length-stream.Position);
                byte[] chunk=new byte[track.Pending.Length+remaining];Buffer.BlockCopy(track.Pending,0,chunk,0,track.Pending.Length);int read=0;
                while(read<remaining){int count=stream.Read(chunk,track.Pending.Length+read,remaining-read);if(count<=0)break;read+=count;}
                track.Offset=stream.Position;int length=track.Pending.Length+read,begin=0;
                if(trimmed){while(begin<length&&chunk[begin]!=10)begin++;begin=Math.Min(length,begin+1);}
                int segment=begin;
                for(int i=begin;i<length;i++)if(chunk[i]==10){Consume(Encoding.UTF8.GetString(chunk,segment,i-segment),track);segment=i+1;}
                int tail=length-segment;if(tail>1024*1024){track.Pending=new byte[0];return;}track.Pending=new byte[tail];Buffer.BlockCopy(chunk,segment,track.Pending,0,tail);
            }
        }
        void Consume(string line,Track track) {
            if(!(line.Contains("\"task_started\"")||line.Contains("\"task_complete\"")||line.Contains("\"turn_aborted\"")||line.Contains("\"task_failed\"")))return;
            try {
                var entry=json.DeserializeObject(line) as Dictionary<string,object>;object type,payload,stamp;
                if(entry==null||!entry.TryGetValue("type",out type)||Convert.ToString(type)!="event_msg"||!entry.TryGetValue("payload",out payload))return;
                var body=payload as Dictionary<string,object>;if(body==null||!body.TryGetValue("type",out type))return;
                string name=Convert.ToString(type);if(name=="task_started")track.State="working";else if(name=="task_complete")track.State="complete";else if(name=="turn_aborted")track.State="idle";else if(name=="task_failed")track.State="failed";else return;
                DateTime timestamp;track.Changed=entry.TryGetValue("timestamp",out stamp)&&DateTime.TryParse(Convert.ToString(stamp),out timestamp)?timestamp.ToUniversalTime():DateTime.UtcNow;
            }catch(ArgumentException){}
        }
        public static void SelfTest(string output) {
            string root=Path.Combine(Path.GetDirectoryName(output),"watcher-fixtures"),day=Path.Combine(root,DateTime.Now.ToString("yyyy/MM/dd"));Directory.CreateDirectory(day);
            string main=Path.Combine(day,"main.jsonl"),agent=Path.Combine(day,"agent.jsonl");
            Func<string,string> ev=name=>"{\"timestamp\":\""+DateTime.UtcNow.ToString("o")+"\",\"type\":\"event_msg\",\"payload\":{\"type\":\""+name+"\"}}\n";
            File.WriteAllText(main,"{\"type\":\"session_meta\",\"payload\":{\"source\":\"vscode\"}}\n"+ev("task_started"));File.WriteAllText(agent,"{\"type\":\"session_meta\",\"payload\":{\"source\":{\"subagent\":{}}}}\n"+ev("task_started"));
            var watcher=new CodexWatcher(root);var results=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);results.Add(name);};
            check(watcher.Poll()=="working","existing running task detected");check(watcher.Poll()==null,"unchanged state is emitted once");
            File.AppendAllText(main,ev("task_complete"));check(watcher.Poll()=="complete","completion detected while subagent is ignored");
            string start=ev("task_started");File.AppendAllText(main,start.Substring(0,25));check(watcher.Poll()==null,"partial event line waits");File.AppendAllText(main,start.Substring(25));check(watcher.Poll()=="working","partial event completed once");
            File.AppendAllText(main,"invalid JSON\n"+ev("turn_aborted"));check(watcher.Poll()=="idle","malformed line cannot block cancellation");
            File.WriteAllText(main,"{}\n"+ev("task_started"));check(watcher.Poll()=="working","truncated session is read from beginning");
            File.WriteAllText(main,"{}\n"+ev("task_complete"));watcher=new CodexWatcher(root);check(watcher.Poll()==null&&watcher.LastState=="idle","startup does not replay an old completion");check(watcher.Poll()==null,"startup suppression persists across polls");
            string partialAgent=Path.Combine(day,"partial-agent.jsonl"),agentMeta="{\"type\":\"session_meta\",\"payload\":{\"source\":{\"subagent\":{}}}}\n";
            File.WriteAllText(partialAgent,agentMeta.Substring(0,24));check(watcher.Poll()==null,"partial source metadata waits");File.AppendAllText(partialAgent,agentMeta.Substring(24)+ev("task_started"));check(watcher.Poll()==null,"completed subagent metadata remains excluded");
            File.WriteAllText(output,"PASS\n"+string.Join("\n",results));
        }
    }
}
