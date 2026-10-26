using System.Reflection;
using Base;
using Roon.Broker.Api;

namespace Roon.Reviews;

/// <summary>
/// Copied into the existing broker assembly by the version-checked patcher.
/// No network calls, database writes, background service, or external helper DLL.
/// </summary>
public static class RuntimeBridge {
    public const string Source = "RoonUserReview";
    public const string Language = "en";
    const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Runs on Roon's existing library thread before its cloud request.</summary>
    public static void Prepare(object closure) {
        try {
            var preferences = (IList<string>?)Field(closure,"sourcePrefs");
            bool preferLocal = preferences != null && preferences.Count>0 && preferences[0]==Source+":"+Language;
            var cleanPreferences = new List<string>();
            bool containsLocal = false;
            if (preferences != null) foreach(string item in preferences) {
                if (item.StartsWith(Source+":",StringComparison.Ordinal)) containsLocal=true;
                else cleanPreferences.Add(item);
            }
            // This source can appear in the client's global preferences, including
            // biography/work requests and albums with no local review. Never send
            // our private source ID upstream, even when no local callback is added.
            if (containsLocal) closure.GetType().GetField("sourcePrefs",Members)!.SetValue(closure,cleanPreferences);
            if (!String.Equals((string?)Field(closure,"blobtype"),"review",StringComparison.OrdinalIgnoreCase)) return;
            long self = (long)Field(closure,"selfid")!;
            object? api = Field(closure,"<>4__this");
            object? library = api == null ? null : Field(api,"_library");
            object? database = library?.GetType().GetProperty("Database",Members)?.GetValue(library);
            if (database == null) return;
            // Architecture A: files are the only review state. Never inspect an
            // album overlay, stored text, marker, registry, or pilot identity.
            List<string>? folders = AlbumFolders(database,library,self);
            if (folders==null || ReadAlbumReview(folders,out string? text)!=FileState.Valid) return;

            FieldInfo callbackField = closure.GetType().GetField("cb",Members)!;
            var callback = (ResultCallback<LocalizedDescriptionContainer>)callbackField.GetValue(closure)!;
            if (callback.Target is Delivery) return;
            var delivery = new Delivery {
                Original=callback,
                Local = new LocalizedDescriptionText {
                    Source=Source,Language=Language,Text=text,
                    Author="User review from user_review.txt"
                },
                PreferLocal=preferLocal,
            };
            callbackField.SetValue(closure,new ResultCallback<LocalizedDescriptionContainer>(delivery.Handle));
        } catch {
            // Unknown versions/types must retain Roon's unmodified behavior.
        }
    }

    /// <summary>Preserves provider order, text, attribution, URLs and image objects.</summary>
    public static LocalizedDescriptionContainer Merge(LocalizedDescriptionContainer? online,LocalizedDescriptionText local,bool preferLocal) {
        var items = new List<LocalizedDescriptionText>();
        if (preferLocal) items.Add(local);
        if (online?.Blobs != null) {
            int count = online.Blobs.Count;
            for(int index=0;index<count;index++) {
                LocalizedDescriptionText item = online.Blobs[index];
                if (item.Source != Source && !String.IsNullOrWhiteSpace(item.Text)) items.Add(item);
            }
        }
        if (!preferLocal) items.Add(local);
        return new LocalizedDescriptionContainer {
            Blobs=(DataList<LocalizedDescriptionText>)Wrap(typeof(LocalizedDescriptionText),items),
            Images=online?.Images ?? new Dictionary<string,Image>(),
            SelectedBlob=items[0],
        };
    }

    /// <summary>Adds a genuine named source; no TiVo/Wikipedia impersonation.</summary>
    public static DataList<MetadataContentSource> RegisterSource(DataList<MetadataContentSource> sources) {
        try {
            var items = new List<MetadataContentSource>();
            int count = sources.Count;
            for(int index=0;index<count;index++) {
                MetadataContentSource item=sources[index];
                if(item.SourceName==Source && item.LanguageTag==Language) return sources;
                items.Add(item);
            }
            items.Add(new MetadataContentSource {
                SourceName=Source,LanguageTag=Language,LanguageName="English",DisplayName="User review."
            });
            object wrapped=Wrap(typeof(MetadataContentSource),items);
            return wrapped is DataList<MetadataContentSource> list ? list : sources;
        } catch { return sources; }
    }

    public sealed class Delivery {
        public ResultCallback<LocalizedDescriptionContainer> Original=null!;
        public LocalizedDescriptionText Local=null!;
        public bool PreferLocal;

        public void Handle(Result<LocalizedDescriptionContainer> result) {
            Result<LocalizedDescriptionContainer> outgoing=result;
            try {
                if(result.IsSuccess && result.Value != null) outgoing=Merge(result.Value,Local,PreferLocal);
                else if(result.Status==Status.NotFound) outgoing=Merge(null,Local,PreferLocal);
                // Authentication, timeouts and outages are not "no review".
            } catch { outgoing=result; }
            Original(outgoing); // Outside the catch: never call a callback twice.
        }
    }

    // ---- folder discovery (v5) ---------------------------------------------
    enum FileState { Missing, Blank, Valid, Invalid }

    static object? Property(object? value,string name) => value?.GetType().GetProperty(name,Members)?.GetValue(value);

    static List<string>? AlbumFolders(object database,object? library,long albumId) {
        try {
            var ids=database.GetType().GetMethod("GetTrackIdsByAlbumId",Members,null,new[]{typeof(long)},null);
            var generate=database.GetType().GetMethod("_GenerateTrack",Members,null,new[]{typeof(long),typeof(bool)},null);
            if(ids==null||generate==null) return null;
            if(ids.Invoke(database,new object[]{albumId}) is not IEnumerable<long> rows) return null;
            var tracks=new List<long>(rows);
            if(tracks.Count==0) return null;
            var folders=new List<string>();
            string? common=null;
            // Inspect all track folders: first/last alone can miss a conflicting
            // review or a different disc in the middle of Roon's enumeration.
            foreach(long track in tracks) {
                string? folder=TrackDirectory(database,generate,library,track);
                if(folder==null) return null;
                if(!folders.Contains(folder)) folders.Add(folder);
                common=common==null?folder:CommonFolder(common,folder);
                if(common==null) return null;
            }
            if(common!=null&&!folders.Contains(common)&&!StorageRoot(library,common)) folders.Add(common);
            return folders;
        } catch { return null; }
    }

    static string? TrackDirectory(object database,MethodInfo generate,object? library,long id) {
        object? track=generate.Invoke(database,new object[]{id,true});
        object? local=Property(track,"Local");
        if(local==null) return null; // Streaming-only tracks have no local folder.
        string? path=Property(local,"UserVisiblePath") as string;
        if(!AbsolutePath(path)) {
            path=Property(local,"ClumpingPath") as string;
            if(!AbsolutePath(path)) {
                object? backend=Property(local,"StorageBackendId");
                string? prefix=backend is Guid guid?PathPrefix(library,guid):null;
                if(!AbsolutePath(prefix)||String.IsNullOrEmpty(path)) return null;
                path=prefix!.TrimEnd('/')+"/"+path!.TrimStart('/');
            }
        }
        if(!AbsolutePath(path)) return null;
        int slash=path!.LastIndexOf('/');
        return slash<=0?null:path.Substring(0,slash);
    }

    static bool AbsolutePath(string? path) {
        if(String.IsNullOrEmpty(path)||path![0]!='/'||path.IndexOf('\0')>=0) return false;
        // Do not permit traversal or URI/display-path guesses.
        foreach(string part in path.Split('/')) if(part==".."||part==".") return false;
        return true;
    }

    static string? PathPrefix(object? library,Guid id) {
        object? manager=Property(library,"StorageManager");
        return manager?.GetType().GetMethod("GetPathPrefix",Members,null,new[]{typeof(Guid)},null)?.Invoke(manager,new object[]{id}) as string;
    }

    static bool StorageRoot(object? library,string folder) {
        object? backends=Property(Property(library,"StorageManager"),"Backends");
        if(backends is System.Collections.IEnumerable rows) foreach(object backend in rows) {
            if(Property(backend,"UniqueId") is Guid id && PathPrefix(library,id)?.TrimEnd('/')==folder) return true;
        }
        return folder=="/";
    }

    static string? CommonFolder(string a,string b) {
        while(a!=b&&!b.StartsWith(a+"/",StringComparison.Ordinal)) {
            int slash=a.LastIndexOf('/');
            if(slash<=0) return null;
            a=a.Substring(0,slash);
        }
        return a;
    }

    static FileState ReadAlbumReview(List<string> folders,out string? text) {
        text=null;
        FileState state=FileState.Missing;
        int found=0;
        foreach(string folder in folders) {
            FileState next=ReadReview(folder+"/user_review.txt",out string? candidate);
            if(next==FileState.Missing) continue;
            if(++found>1) {text=null;return FileState.Invalid;}
            state=next;text=candidate;
        }
        return state;
    }

    // ---- live file reading -------------------------------------------------
    // Reviews are re-read from user_review.txt on every request. All
    // framework access is late-bound through typeof(object).Assembly because
    // facade type references previously caused a TypeLoadException (v2 lesson).
    static volatile Type? s_file;
    static volatile MethodInfo? s_getString;
    static volatile MethodInfo? s_openRead;
    static volatile MethodInfo? s_directoryExists;
    static volatile object? s_strictUtf8;

    static FileState ReadReview(string path,out string? text) {
        text=null;
        try {
            EnsureTypes();
            if(s_openRead==null||s_getString==null||s_strictUtf8==null) return FileState.Invalid;
            object? stream;
            try {stream=s_openRead.Invoke(null,new object[]{path});}
            catch(TargetInvocationException error) {
                string? kind=error.InnerException?.GetType().FullName;
                // A missing mount/directory is unknown, not an intentional deletion.
                int slash=path.LastIndexOf('/');
                bool directory=slash>0 && s_directoryExists!=null && (bool)s_directoryExists.Invoke(null,new object[]{path.Substring(0,slash)})!;
                return kind=="System.IO.FileNotFoundException"&&directory?FileState.Missing:FileState.Invalid;
            }
            if(stream==null) return FileState.Invalid;
            byte[] buffer=new byte[256*1024+1];
            int count=0;
            try {
                MethodInfo read=stream.GetType().GetMethod("Read",new[]{typeof(byte[]),typeof(int),typeof(int)})!;
                while(count<buffer.Length) {
                    int got=(int)read.Invoke(stream,new object[]{buffer,count,buffer.Length-count})!;
                    if(got==0) break;
                    count+=got;
                }
            } finally { ((IDisposable)stream).Dispose(); }
            if(count>256*1024) return FileState.Invalid;
            if(count==0) return FileState.Blank;
            if(count>=2&&((buffer[0]==0xFF&&buffer[1]==0xFE)||(buffer[0]==0xFE&&buffer[1]==0xFF))) return FileState.Invalid;
            byte[] data=new byte[count];Array.Copy(buffer,data,count);
            string decoded=(string)s_getString.Invoke(s_strictUtf8,new object[]{data})!;
            if(decoded.Length>0&&decoded[0]=='\uFEFF') decoded=decoded.Substring(1);
            foreach(char ch in decoded) if((ch<' '&&ch!='\r'&&ch!='\n'&&ch!='\t')||ch=='\u007f') return FileState.Invalid;
            if(String.IsNullOrWhiteSpace(decoded)) return FileState.Blank;
            text=decoded;return FileState.Valid;
        } catch {return FileState.Invalid;}
    }

    static void EnsureTypes() {
        if(s_file!=null) return;
        var core=typeof(object).Assembly;
        Type? file=core.GetType("System.IO.File",false);
        Type? enc=core.GetType("System.Text.Encoding",false);
        if(file==null||enc==null) return;
        s_getString=enc.GetMethod("GetString",BindingFlags.Public|BindingFlags.Instance,null,new[]{typeof(byte[])},null);
        s_openRead=file.GetMethod("OpenRead",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(string)},null);
        s_directoryExists=core.GetType("System.IO.Directory",false)?.GetMethod("Exists",new[]{typeof(string)});
        s_strictUtf8=core.GetType("System.Text.UTF8Encoding",false)?.GetConstructor(new[]{typeof(bool),typeof(bool)})?.Invoke(new object[]{false,true});
        s_file=file; // Set last; unavailable methods retain stock Roon behavior.
    }

    static object? Field(object instance,string name) => instance.GetType().GetField(name,Members)?.GetValue(instance);

    static object Wrap(Type item,object items) {
        Type extensions=typeof(RuntimeBridge).Assembly.GetType("Roon.Broker.Extensions",true)!;
        foreach(MethodInfo method in extensions.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)) {
            if(method.Name!="ToDataList" || !method.IsGenericMethodDefinition) continue;
            ParameterInfo[] p=method.GetParameters();
            if(p.Length==1 && p[0].ParameterType.IsGenericType &&
                p[0].ParameterType.GetGenericTypeDefinition()==typeof(IEnumerable<>))
                return method.MakeGenericMethod(item).Invoke(null,new[]{items})!;
        }
        return null!;  // caller treats null as failure and preserves Roon's original behavior
    }
}
