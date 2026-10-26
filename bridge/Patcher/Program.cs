using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

// SPDX-License-Identifier: Apache-2.0
// Release hardening: offline-only output and verify before writing any candidate.
if(args.Length!=3)throw new ArgumentException("Usage: Patcher ORIGINAL_CORE RUNTIME_HELPER OUTPUT_CORE");
string original=Path.GetFullPath(args[0]),helper=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[2]);
if(output==original||output==helper||output.Split(Path.DirectorySeparatorChar).Any(p=>p.EndsWith(".app",StringComparison.OrdinalIgnoreCase))||output.StartsWith("/Volumes/",StringComparison.Ordinal))
    throw new IOException("Output must be a new local scratch file, not an application bundle, input, or mounted share");
const string baseline="a61a72bc683db159a13fd08a14921f781cf7c2685a7c9ec193bcae075db2612d";
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
if(Hash(original)!=baseline)throw new Exception("Unsupported original Roon assembly: do not patch an unknown version or an already patched DLL");
if(File.Exists(output)||File.Exists(output+".json"))throw new IOException("Output already exists");
var resolver=new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(original)!);
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
using var target=AssemblyDefinition.ReadAssembly(original,new ReaderParameters{AssemblyResolver=resolver});
using var source=AssemblyDefinition.ReadAssembly(helper);
if(source.Name.Name!="RoonReviews.Runtime"||source.MainModule.GetType("Roon.Reviews.RuntimeBridge")==null)
    throw new Exception("Expected a locally built RoonReviews.Runtime helper");
if(target.Name.HasPublicKey)throw new Exception("Unexpected strong name");
var module=target.MainModule;
var library=module.GetType("Roon.Broker.Music.LibraryApi");
var closure=library.NestedTypes.Single(t=>t.Name=="<>c__DisplayClass180_0");
var request=closure.Methods.Single(m=>m.Name=="<GetLocalizedEntityText>b__0");
var sources=library.Methods.Single(m=>m.Name=="get_SupportedMetadataContentSources");
var changed=new HashSet<string>{request.FullName,sources.FullName};
var before=Fingerprint.All(module.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody&&!changed.Contains(m.FullName))
    .GroupBy(m=>m.FullName).ToDictionary(g=>g.Key,g=>g.Select(Fingerprint.Of).ToArray());
var clone=new Transplant(module);
clone.Copy(source.MainModule.GetType("Roon.Reviews.RuntimeBridge"));
var runtime=module.GetType("Roon.Reviews.RuntimeBridge");
var prepare=runtime.Methods.Single(m=>m.Name=="Prepare");
var register=runtime.Methods.Single(m=>m.Name=="RegisterSource");
var il=request.Body.GetILProcessor();var first=request.Body.Instructions[0];
il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0));
il.InsertBefore(first,Instruction.Create(OpCodes.Call,prepare));
il=sources.Body.GetILProcessor();
foreach(var ret in sources.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())il.InsertBefore(ret,Instruction.Create(OpCodes.Call,register));
if(module.AssemblyReferences.Any(a=>a.Name==source.Name.Name))throw new Exception("Helper must be embedded, not an external runtime dependency");
using var stream=new MemoryStream();
target.Write(stream);stream.Position=0;
using var reread=AssemblyDefinition.ReadAssembly(stream);
var after=Fingerprint.All(reread.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody)
    .GroupBy(m=>m.FullName).ToDictionary(g=>g.Key,g=>g.Select(Fingerprint.Of).ToArray());
foreach(var pair in before)if(!after[pair.Key].SequenceEqual(pair.Value))throw new Exception("Unrelated method changed: "+pair.Key);
if(Hash(original)!=baseline)throw new Exception("Input changed while building");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
stream.Position=0;
using(var file=new FileStream(output,FileMode.CreateNew))stream.CopyTo(file);
var report=new {
    adapter="roon-2.74-build-1712",baselineSHA256=baseline,patchedSHA256=Hash(output),
    unrelatedMethodBodiesVerified=before.Values.Sum(a=>a.Length),modifiedMethods=changed,
    sourceId="RoonUserReview",sourceFile="user_review.txt",defaultPolicy="online-first",
    scope="Architecture A: track-folder user_review.txt; zero registration or overlay access",externalRuntimeDependency=false,
};
using(var file=new FileStream(output+".json",FileMode.CreateNew))JsonSerializer.Serialize(file,report,new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine(JsonSerializer.Serialize(report));

static class Fingerprint {
    public static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types) {
        foreach(var type in types){yield return type;foreach(var nested in All(type.NestedTypes))yield return nested;}
    }
    public static string Of(MethodDefinition method) {
        string Operand(object? value)=>value switch {
            Instruction i=>"@"+method.Body.Instructions.IndexOf(i),
            Instruction[] items=>String.Join(",",items.Select(i=>"@"+method.Body.Instructions.IndexOf(i))),
            VariableDefinition v=>"V"+v.Index,ParameterDefinition p=>"P"+p.Index,null=>"",_=>value.ToString()!,
        };
        return String.Join("\n",method.Body.Instructions.Select(i=>i.OpCode.Name+" "+Operand(i.Operand)))+
            "\nEH:"+String.Join(";",method.Body.ExceptionHandlers.Select(e=>$"{e.HandlerType}:{Operand(e.TryStart)}:{Operand(e.TryEnd)}:{Operand(e.HandlerStart)}:{Operand(e.HandlerEnd)}:{e.CatchType}"));
    }
}
