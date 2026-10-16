using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JiaranPet {
    public sealed class AssetPack {
        readonly string root;
        readonly Dictionary<string,BitmapSource> cache=new Dictionary<string,BitmapSource>();
        readonly Dictionary<string,object> actions=new Dictionary<string,object>();
        public int CellWidth=192, CellHeight=208;
        public AssetPack(string directory) {
            root=directory;string manifest=Path.Combine(root,"manifest.json");
            if(File.Exists(manifest)) {
                var data=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(manifest)) as Dictionary<string,object>;
                if(data!=null){CellWidth=Int(data,"frameWidth",Int(data,"cellWidth",192));CellHeight=Int(data,"frameHeight",Int(data,"cellHeight",208));object map;if(data.TryGetValue("actions",out map)){var a=map as Dictionary<string,object>;if(a!=null)foreach(var entry in a)actions.Add(entry.Key,entry.Value);}}
            }
            if(CellWidth<16||CellHeight<16||CellWidth>4096||CellHeight>4096)throw new Exception("manifest.json 帧尺寸无效");
        }
        static int Int(Dictionary<string,object> d,string k,int def) { object v;return d.TryGetValue(k,out v)?Convert.ToInt32(v):def; }
        static double Number(Dictionary<string,object> d,string k,double def) { object v;return d.TryGetValue(k,out v)?Convert.ToDouble(v):def; }
        static string Text(Dictionary<string,object> d,string k,string def) { object v;return d.TryGetValue(k,out v)?Convert.ToString(v):def; }
        BitmapSource Load(string name) {
            string path=Path.GetFullPath(Path.Combine(root,name));string allowed=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!path.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new Exception("素材路径不能超出 assets 文件夹");
            BitmapSource b;if(cache.TryGetValue(path,out b))return b;
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();cache[path]=bitmap;return bitmap;
        }
        public BitmapSource Get(string state,double age,int direction,int gaze=-1,int frameOverride=-1) {
            string key=state=="walk"?(direction>0?"walk-right":"walk-left"):state;
            if(gaze>=0)key="look";
            string file="atlas.png";int row=0,count=6,col=0,start=0;double fps=3;bool loop=true;
            object raw;var spec=actions.TryGetValue(key,out raw)?raw as Dictionary<string,object>:null;
            if(spec!=null) { file=Text(spec,"file",file);row=Int(spec,"row",0);count=Int(spec,"count",1);start=Int(spec,"column",0);fps=Number(spec,"fps",4);object v;if(spec.TryGetValue("loop",out v))loop=Convert.ToBoolean(v); }
            else {
                switch(key) {
                    case "walk-right":row=1;count=8;fps=8;break;case "walk-left":row=2;count=8;fps=8;break;
                    case "wave":file=File.Exists(Path.Combine(root,"wave-frames.png"))?"wave-frames.png":"atlas.png";row=file=="atlas.png"?3:0;count=4;fps=5;break;
                    case "jump":row=4;count=5;fps=6;loop=false;break;case "failed":row=5;count=8;fps=5;break;
                    case "waiting":row=6;count=6;fps=4;break;case "working":row=7;count=6;fps=4;break;case "complete":row=8;count=6;fps=5;break;
                    case "look":row=9;count=16;break;case "candy":file="candy-frames.png";count=4;fps=1;loop=false;break;
                    case "sleep":file="sleep-frames.png";count=4;fps=1.6;break;
                }
            }
            if(count<1||count>64)throw new Exception("动作帧数无效："+key);
            col=frameOverride>=0?Math.Min(count-1,frameOverride):gaze>=0?Math.Min(count-1,gaze):(loop?(int)(age*Math.Max(.1,fps))%count:Math.Min(count-1,(int)(age*Math.Max(.1,fps))));
            var source=Load(file);int columns=source.PixelWidth/CellWidth;
            if(columns<1)throw new Exception("素材尺寸小于单帧："+file);
            int index=start+col,x=(index%columns)*CellWidth,y=(row+index/columns)*CellHeight;
            string crop=file+"/"+x+"/"+y;BitmapSource result;if(cache.TryGetValue(crop,out result))return result;
            if(x+CellWidth>source.PixelWidth||y+CellHeight>source.PixelHeight)throw new Exception("动作素材越界："+key+" #"+col);
            result=new CroppedBitmap(source,new Int32Rect(x,y,CellWidth,CellHeight));result.Freeze();cache[crop]=result;return result;
        }
        public void Validate() { foreach(string state in new[]{"idle","wave","jump","failed","waiting","working","complete","candy","sleep","walk"})for(int i=0;i<64;i++)Get(state,0,-1,-1,i);for(int i=0;i<64;i++){Get("walk",0,1,-1,i);Get("idle",0,-1,0,i);} }
        public static void SelfTest(string directory,string output) {
            Directory.CreateDirectory(directory);var mapping=new Dictionary<string,object>();
            foreach(string state in new[]{"idle","wave","jump","failed","waiting","working","complete","candy","sleep","walk-left","walk-right","look"})mapping[state]=new { file="valid-strip.png",count=4,fps=4 };
            Action<string,int> strip=(name,width)=>{byte[] pixels=new byte[width*16*4];for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;var image=BitmapSource.Create(width,16,96,96,PixelFormats.Bgra32,null,pixels,width*4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(directory,name)))png.Save(stream);};
            strip("valid-strip.png",64);strip("short-strip.png",16);
            var serializer=new JavaScriptSerializer();string manifest=Path.Combine(directory,"manifest.json");
            File.WriteAllText(manifest,serializer.Serialize(new { cellWidth=16,cellHeight=16,actions=mapping }));new AssetPack(directory).Validate();
            mapping["walk-right"]=new { file="short-strip.png",count=4,fps=4 };File.WriteAllText(manifest,serializer.Serialize(new { cellWidth=16,cellHeight=16,actions=mapping }));
            bool rejected=false;try{new AssetPack(directory).Validate();}catch(Exception ex){rejected=ex.Message.Contains("walk-right");}if(!rejected)throw new Exception("Incomplete walking strip passed preflight");
            File.WriteAllText(output,"PASS\ncustom cell dimensions decoded\nall valid action strips load\nmissing later walking frame rejected before installation\n");
        }
    }
}
