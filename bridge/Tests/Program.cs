using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text;
using Roon.Broker.Api;
using Base;

// Public mode uses synthetic contracts; private integration mode takes local DLLs.
if(args.Length==0) args=new[]{Assembly.GetExecutingAssembly().Location,AppContext.BaseDirectory};
if(args.Length!=2)throw new ArgumentException("Usage: Tests [PATCHED_CORE ASSEMBLIES_DIRECTORY]");
string assemblies=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(_,name)=>{
    string path=Path.Combine(assemblies,name.Name+".dll");
    return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;
};
var core=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
var bridge=core.GetType("Roon.Reviews.RuntimeBridge",true)!;
const string source="RoonUserReview";
int assertions=0;
void Check(bool value,string message){if(!value)throw new Exception(message);assertions++;}
object Call(string name,params object?[] values)=>bridge.GetMethod(name)!.Invoke(null,values)!;
DataList<T> List<T>(params T[] values)=>(DataList<T>)bridge.GetMethod("Wrap",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{typeof(T),values})!;
LocalizedDescriptionContainer Merge(LocalizedDescriptionContainer? online,LocalizedDescriptionText local,bool preferLocal)=>(LocalizedDescriptionContainer)Call("Merge",online,local,preferLocal);
const string exact="First paragraph — user text.\r\n\r\nSecond paragraph.\n";
var local=new LocalizedDescriptionText{Source=source,Language="en",Text=exact,Author="User review from user_review.txt"};
var tivo=new LocalizedDescriptionText{Source="Rovi-albums",Language="en",Text="Provider text",Author="Original author",SourceUrl="https://example.org/review"};
var wiki=new LocalizedDescriptionText{Source="Wikipedia",Language="en",Text="Wikipedia text",SourceUrl="https://en.wikipedia.org/wiki/Example"};
var images=new Dictionary<string,Image>();
var online=new LocalizedDescriptionContainer{Blobs=List(tivo,wiki),Images=images};
var merged=Merge(online,local,false);
Check(merged.Blobs.Count==3,"Three sources required");
Check(ReferenceEquals(merged.Blobs[0],tivo)&&ReferenceEquals(merged.Blobs[1],wiki),"Provider order/objects changed");
Check(ReferenceEquals(merged.SelectedBlob,tivo),"Online review must be default");
Check(merged.Blobs[2].Text==exact,"File text changed");
Check(ReferenceEquals(merged.Images,images),"Provider image dictionary changed");
Check(tivo.Author=="Original author"&&tivo.SourceUrl=="https://example.org/review","Provider attribution changed");
var preferred=Merge(online,local,true);
Check(preferred.SelectedBlob.Source==source&&preferred.Blobs[1]==tivo,"Explicit local selection not honored");
var only=Merge(null,local,false);
Check(only.Blobs.Count==1&&only.SelectedBlob.Text==exact,"Missing-provider fallback failed");
Check(Merge(merged,local,false).Blobs.Count==3,"Local source duplicated");
var available=List(new MetadataContentSource{SourceName="Wikipedia",LanguageTag="en",DisplayName="English Wikipedia"});
var registered=(DataList<MetadataContentSource>)Call("RegisterSource",available);
Check(registered.Count==2&&registered[1].SourceName==source,"Source not registered");
Check(registered[1].DisplayName=="User review.","Incorrect local source label");
Check(ReferenceEquals(Call("RegisterSource",registered),registered),"Source registration not idempotent");

var deliveryType=bridge.GetNestedType("Delivery")!;
object Delivery(ResultCallback<LocalizedDescriptionContainer> callback) {
    var result=Activator.CreateInstance(deliveryType)!;
    deliveryType.GetField("Original")!.SetValue(result,callback);
    deliveryType.GetField("Local")!.SetValue(result,local);
    return result;
}
int delivered=0;Result<LocalizedDescriptionContainer>? received=null;
ResultCallback<LocalizedDescriptionContainer> capture=result=>{delivered++;received=result;};
var delivery=Delivery(capture);
deliveryType.GetMethod("Handle")!.Invoke(delivery,new object[]{(Result<LocalizedDescriptionContainer>)online});
Check(delivered==1&&received!.Value.SelectedBlob.Source=="Rovi-albums","Successful provider result not merged");
deliveryType.GetMethod("Handle")!.Invoke(delivery,new object[]{new Result<LocalizedDescriptionContainer>(Status.NotFound,"none")});
Check(delivered==2&&received!.Value.SelectedBlob.Source==source,"NotFound must retain local review");
var failure=new Result<LocalizedDescriptionContainer>(Status.InvalidArgument,"not a missing review");
deliveryType.GetMethod("Handle")!.Invoke(delivery,new object[]{failure});
Check(delivered==3&&received!.Status==Status.InvalidArgument,"Failure incorrectly converted to absence");
int throws=0;var throwing=Delivery(result=>{throws++;throw new Exception("callback");});
try{deliveryType.GetMethod("Handle")!.Invoke(throwing,new object[]{(Result<LocalizedDescriptionContainer>)online});}catch(TargetInvocationException){}
Check(throws==1,"Callback retried after exception");

// Exercise the real transplanted Prepare code without opening any database.
var dynamic=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ReviewBridgeFixtures"),AssemblyBuilderAccess.Run).DefineDynamicModule("Fixtures");
var shape=dynamic.DefineType("FixtureClosure",TypeAttributes.Public);
foreach(var field in new[]{("id",typeof(object)),("selfid",typeof(long)),("blobtype",typeof(string)),("sourcePrefs",typeof(IList<string>)),("cb",typeof(ResultCallback<LocalizedDescriptionContainer>)),("<>4__this",typeof(object))})shape.DefineField(field.Item1,field.Item2,FieldAttributes.Public);
var closureType=shape.CreateType()!;var closure=Activator.CreateInstance(closureType)!;
void Set(string name,object value)=>closureType.GetField(name)!.SetValue(closure,value);
Set("id","fixture:album");Set("selfid",777L);Set("blobtype","Review");
Set("sourcePrefs",new List<string>{source+":en","Rovi-albums:en","Wikipedia:en"});Set("cb",capture);
string tmp=Path.Combine(Path.GetTempPath(),"roon-bridge-tests-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
string reviewPath=Path.Combine(tmp,"user_review.txt");
File.WriteAllText(reviewPath,exact);
var db=new FakeDatabase();
db.Tracks[1]=new FakeTrack{Local=new FakeLocalTrack{UserVisiblePath=tmp+"/01.flac"}};
Set("<>4__this",new FakeApi{_library=new FakeLibrary{Database=db}});
Call("Prepare",closure);
var prefs=(IList<string>)closureType.GetField("sourcePrefs")!.GetValue(closure)!;
Check(prefs.Count==2&&prefs[0]=="Rovi-albums:en","Custom source leaked into cloud request");
var wrapped=(ResultCallback<LocalizedDescriptionContainer>)closureType.GetField("cb")!.GetValue(closure)!;
Check(!ReferenceEquals(wrapped,capture),"Callback not wrapped");
wrapped(online);
Check(received!.Value.SelectedBlob.Source==source,"Explicit selection lost during Prepare");
Check(received.Value.SelectedBlob.Text==exact,"Prepare changed file text");
Set("blobtype","Biography");Set("cb",capture);Set("sourcePrefs",new List<string>{source+":en","Wikipedia:en"});Call("Prepare",closure);
Check(ReferenceEquals(closureType.GetField("cb")!.GetValue(closure),capture),"Biography intercepted");
Check(((IList<string>)closureType.GetField("sourcePrefs")!.GetValue(closure)!).SequenceEqual(new[]{"Wikipedia:en"}),"Local source leaked into biography request");
Set("blobtype","review");Set("selfid",999L);Set("id","121:0:MW0000000000");Set("sourcePrefs",new List<string>{source+":en","Rovi-albums:en"});
db.Tracks.Clear();Call("Prepare",closure);
Check(ReferenceEquals(closureType.GetField("cb")!.GetValue(closure),capture),"Unmanaged album callback changed");
Check(((IList<string>)closureType.GetField("sourcePrefs")!.GetValue(closure)!).SequenceEqual(new[]{"Rovi-albums:en"}),"Local source leaked for unmanaged album");
// ---- v5: no-overlay folder discovery ----
var library=new FakeLibrary{Database=db,StorageManager=new FakeStorageManager{Prefix=tmp}};
Set("<>4__this",new FakeApi{_library=library});
ResultCallback<LocalizedDescriptionContainer> FolderPrepare() {
    Set("cb",capture);Set("sourcePrefs",new List<string>{"Rovi-albums:en"});
    Call("Prepare",closure);
    return (ResultCallback<LocalizedDescriptionContainer>)closureType.GetField("cb")!.GetValue(closure)!;
}
string LocalText(ResultCallback<LocalizedDescriptionContainer> cb) {
    received=null;cb(new Result<LocalizedDescriptionContainer>(Status.NotFound,"none"));
    return received!.Value.SelectedBlob.Text;
}
db.Tracks[1]=new FakeTrack{Local=new FakeLocalTrack{UserVisiblePath=tmp+"/01.flac"}};
File.WriteAllText(reviewPath,exact,new UTF8Encoding(true));
var folderCb=FolderPrepare();
Check(!ReferenceEquals(folderCb,capture)&&LocalText(folderCb)==exact,"No-overlay album not served verbatim");
folderCb(online);
Check(received!.Value.SelectedBlob.Source=="Rovi-albums"&&received.Value.Blobs.Count==3,"No-overlay album must preserve online priority");
File.WriteAllText(reviewPath,"EDITED\r\n");
Check(LocalText(FolderPrepare())=="EDITED\r\n","Folder file edits not live");
File.Delete(reviewPath);
Check(ReferenceEquals(FolderPrepare(),capture),"No-overlay deletion must hide local review");
File.WriteAllText(reviewPath,exact);
Check(LocalText(FolderPrepare())==exact,"No-overlay restore not live");
db.Data=new Roon.Broker.Music.AlbumData{Overlay=new FakeOverlay{Review=new FakeStoredReview{Author="Provider author",Text="ONLINE OVERLAY"}}};
Check(LocalText(FolderPrepare())==exact&&db.Data.Overlay.Review.Text=="ONLINE OVERLAY","Unmanaged overlay must coexist unchanged");
File.WriteAllBytes(reviewPath,new byte[]{0xC3,0x28});
Check(ReferenceEquals(FolderPrepare(),capture),"Invalid UTF-8 must not become replacement characters or unmanaged fallback");
db.Data.Overlay.Review.Author="roon-reviews:v1:en:file:legacy";db.Data.Overlay.Review.Text="LAST KNOWN";
Check(ReferenceEquals(FolderPrepare(),capture),"Corrupt file must retain stock behavior, not resurrect an overlay");
foreach(var bad in new[]{new byte[]{0xFF,0xFE,0x41,0x00},new byte[]{0xFE,0xFF,0x00,0x41},new byte[]{0x41,0x00,0x42},new byte[]{0x41,0x01,0x42},new byte[256*1024+1]}) {
    File.WriteAllBytes(reviewPath,bad);
    Check(ReferenceEquals(FolderPrepare(),capture),"UTF-16, binary or oversized file must retain stock behavior");
}
File.WriteAllText(reviewPath," \r\n");
Check(ReferenceEquals(FolderPrepare(),capture),"Blank folder file must hide stored review");
File.Delete(reviewPath);
Check(ReferenceEquals(FolderPrepare(),capture),"Missing folder file must hide stored review");
db.Tracks[1].Local!.UserVisiblePath=tmp+"/unavailable/01.flac";
Check(ReferenceEquals(FolderPrepare(),capture),"Unavailable directory must retain stock behavior");
db.Tracks[1].Local!.UserVisiblePath=null;db.Tracks[1].Local!.ClumpingPath="01.flac";
File.WriteAllText(reviewPath,exact);
Check(LocalText(FolderPrepare())==exact,"Storage prefix + clumping path fallback failed");
db.Tracks[1].Local!.ClumpingPath="../escape/01.flac";
Check(ReferenceEquals(FolderPrepare(),capture),"Unsafe path must not use an old managed marker");
db.Data.Overlay=null;
Check(ReferenceEquals(FolderPrepare(),capture),"Unsafe path resolved without managed fallback");
string album=tmp+"/Album";
foreach(string disc in new[]{"Disc1","Disc2","Disc3"}) Directory.CreateDirectory(album+"/"+disc);
db.Tracks.Clear();
db.Tracks[1]=new FakeTrack{Local=new FakeLocalTrack{UserVisiblePath=album+"/Disc1/01.flac"}};
db.Tracks[2]=new FakeTrack{Local=new FakeLocalTrack{UserVisiblePath=album+"/Disc2/02.flac"}};
db.Tracks[3]=new FakeTrack{Local=new FakeLocalTrack{UserVisiblePath=album+"/Disc3/03.flac"}};
File.WriteAllText(album+"/user_review.txt","MULTI DISC\n");
Check(LocalText(FolderPrepare())=="MULTI DISC\n","Multi-disc common parent not discovered");
File.WriteAllText(album+"/Disc1/user_review.txt","AMBIGUOUS");
Check(ReferenceEquals(FolderPrepare(),capture),"Ambiguous folder files must not select arbitrarily");
db.Data.Overlay=new FakeOverlay{Review=new FakeStoredReview{Author="roon-reviews:v1:en:file:legacy",Text="STALE OVERLAY"}};
Check(ReferenceEquals(FolderPrepare(),capture),"Ambiguity must not use stored overlay text");
db.Data.Overlay=null;
File.Delete(album+"/Disc1/user_review.txt");File.Delete(album+"/user_review.txt");
File.WriteAllText(album+"/Disc2/user_review.txt","MIDDLE DISC");
Check(LocalText(FolderPrepare())=="MIDDLE DISC","Expansion to remaining tracks failed");
File.WriteAllText(album+"/Disc1/user_review.txt","FIRST DISC CONFLICT");
Check(ReferenceEquals(FolderPrepare(),capture),"Middle-track ambiguity must not be missed");
File.Delete(album+"/Disc1/user_review.txt");
File.Delete(album+"/Disc2/user_review.txt");
library.StorageManager.Prefix=album;
File.WriteAllText(album+"/user_review.txt","SHARED STORAGE ROOT");
Check(ReferenceEquals(FolderPrepare(),capture),"Shared music-root review must not be used");
Directory.Delete(tmp,true);
Check(db.AlbumReads==0,"Architecture A must never read album overlays");
Console.WriteLine($"PASS: {assertions} assertions; online priority, native sources, callback safety, live files, no-overlay folder discovery, multi-disc, ambiguity, strict UTF-8, stock-on-failure, zero overlay reads");

public sealed class FakeApi{public object? _library;}
public sealed class FakeLibrary{public FakeDatabase? Database{get;set;}public FakeStorageManager? StorageManager{get;set;}}
public sealed class FakeDatabase{
    public Roon.Broker.Music.AlbumData? Data;
    public int AlbumReads;
    public Dictionary<long,FakeTrack> Tracks=new();
    public IEnumerable<long> GetTrackIdsByAlbumId(long id)=>Tracks.Keys;
    private FakeTrack? _GenerateTrack(long id,bool cache)=>Tracks.TryGetValue(id,out var track)?track:null;
    public bool TryGetValue(long id,out Roon.Broker.Music.AlbumData? data,bool committed){AlbumReads++;data=Data;return data!=null;}
}
public sealed class FakeOverlay{public FakeStoredReview? Review{get;set;}}
public sealed class FakeStoredReview{public string? Text{get;set;}public string? Author{get;set;}}
public sealed class FakeTrack{public FakeLocalTrack? Local{get;set;}}
public sealed class FakeLocalTrack{public string? UserVisiblePath{get;set;}public string? ClumpingPath{get;set;}public Guid StorageBackendId{get;set;}=Guid.Parse("11111111-1111-1111-1111-111111111111");}
public sealed class FakeStorageBackend{public Guid UniqueId{get;set;}=Guid.Parse("11111111-1111-1111-1111-111111111111");}
public sealed class FakeStorageManager{public string? Prefix{get;set;}public FakeStorageBackend[] Backends{get;set;}={new()};public string? GetPathPrefix(Guid id)=>Prefix;}
