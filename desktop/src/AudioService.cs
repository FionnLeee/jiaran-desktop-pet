using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Threading;

namespace JiaranPet {
    public sealed class AudioService : IDisposable {
        readonly string root;
        readonly Dictionary<string,string[]> events=new Dictionary<string,string[]>();
        readonly Random random=new Random();
        readonly MediaPlayer player=new MediaPlayer();
        readonly Dictionary<string,int> lastIndices=new Dictionary<string,int>();
        double lastPlayed=-100;
        bool disposed;
        public bool Enabled=true;
        double volume=.5;
        public double Volume { get { return volume; } set { volume=double.IsNaN(value)?0:Math.Max(0,Math.Min(1,value));if(!disposed)player.Volume=volume; } }
        public string Status { get; private set; }
        public int ClipCount { get; private set; }
        public AudioService(string appRoot) {
            root=appRoot;Status="未配置本地语音（可自备已获授权的音频）";string path=Path.Combine(root,"voice_manifest.json");
            player.MediaFailed+=delegate { Status="音频播放失败，可检查 voice_manifest.json"; };
            if(!File.Exists(path))return;
            try {
                var manifest=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string,object>;object raw;
                if(manifest!=null&&manifest.TryGetValue("events",out raw)) {
                    var mapping=raw as Dictionary<string,object>;if(mapping==null)return;
                    var uniqueClips=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach(var entry in mapping) {
                        var clips=new List<string>();var array=entry.Value as object[];
                        if(array==null)array=new[]{entry.Value};
                        foreach(object item in array) {
                            string relative=item as string;var spec=item as Dictionary<string,object>;object filename;
                            if(relative==null&&spec!=null&&spec.TryGetValue("file",out filename))relative=filename as string;
                            if(string.IsNullOrWhiteSpace(relative))continue;
                            string clip=Path.GetFullPath(Path.Combine(root,relative)),allowed=Path.GetFullPath(Path.Combine(root,"audio")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                            if(clip.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)&&File.Exists(clip)){clips.Add(clip);uniqueClips.Add(clip);}
                        }
                        if(clips.Count>0)events[entry.Key]=clips.ToArray();
                    }
                    ClipCount=uniqueClips.Count;
                    if(ClipCount>0)Status="本地语音 · "+ClipCount+" 段片段";
                }
            }catch(Exception){Status="音频清单格式有误";}
        }
        public bool Play(string eventName,double now,bool force=false) {
            if(disposed||!Enabled||(!force&&now-lastPlayed<4))return false;
            string[] clips;if(!events.TryGetValue(eventName,out clips))return false;
            int index=random.Next(clips.Length),previous;if(clips.Length>1&&lastIndices.TryGetValue(eventName,out previous)&&index==previous)index=(index+1)%clips.Length;
            lastIndices[eventName]=index;lastPlayed=now;
            try { player.Stop();player.Close();player.Volume=Math.Max(0,Math.Min(1,Volume));player.Open(new Uri(clips[index]));player.Play();return true; }catch(Exception){Status="音频播放失败";return false;}
        }
        public void Stop() { if(!disposed)player.Stop(); }
        public void Dispose() { if(disposed)return;disposed=true;player.Close(); }
        public static void ExportQA(string appRoot,string output) {
            var results=new List<string>();
            foreach(string path in Directory.GetFiles(Path.Combine(appRoot,"audio"),"*.wav")) {
                var decoder=new MediaPlayer { Volume=0 };var frame=new DispatcherFrame();string error=null;double duration=0;
                var timeout=new DispatcherTimer { Interval=TimeSpan.FromSeconds(8) };
                timeout.Tick+=delegate { error="decoder timeout";frame.Continue=false; };
                decoder.MediaOpened+=delegate { if(decoder.NaturalDuration.HasTimeSpan)duration=decoder.NaturalDuration.TimeSpan.TotalSeconds;frame.Continue=false; };
                decoder.MediaFailed+=(sender,e)=>{error=e.ErrorException.Message;frame.Continue=false;};
                timeout.Start();decoder.Open(new Uri(path));decoder.Play();Dispatcher.PushFrame(frame);timeout.Stop();decoder.Close();
                if(error!=null||duration<=0)throw new Exception("Audio decode failed: "+Path.GetFileName(path)+" "+error);
                results.Add(Path.GetFileName(path)+": "+duration.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"s decoded at zero volume");
            }
            if(results.Count==0)throw new Exception("No voice clips found");
            File.WriteAllText(output,"PASS\n"+string.Join("\n",results));
        }
    }
}
