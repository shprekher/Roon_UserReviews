// SPDX-License-Identifier: Apache-2.0
// Handwritten minimal test doubles, not Roon implementation or generated schemas.
// These names model only the contract touched by our bridge. Never deploy them.
namespace Base {
    public enum Status { Success, NotFound, InvalidArgument }
    public delegate void ResultCallback<T>(Result<T> result);
    public sealed class Result<T> {
        public Status Status {get;}
        public T Value {get;}
        public bool IsSuccess => Status==Status.Success;
        public Result(Status status,string message) {Status=status;Value=default!;}
        public Result(T value) {Status=Status.Success;Value=value;}
        public static implicit operator Result<T>(T value)=>new(value);
    }
}
namespace Roon.Broker.Api {
    public sealed class DataList<T>:List<T> {public DataList(IEnumerable<T> items):base(items){}}
    public sealed class Image {}
    public sealed class LocalizedDescriptionText {
        public string Source {get;set;}="";
        public string Language {get;set;}="";
        public string Text {get;set;}="";
        public string? Author {get;set;}
        public string? SourceUrl {get;set;}
    }
    public sealed class LocalizedDescriptionContainer {
        public DataList<LocalizedDescriptionText> Blobs {get;set;}=new(Array.Empty<LocalizedDescriptionText>());
        public IDictionary<string,Image> Images {get;set;}=new Dictionary<string,Image>();
        public LocalizedDescriptionText SelectedBlob {get;set;}=null!;
    }
    public sealed class MetadataContentSource {
        public string SourceName {get;set;}="";
        public string LanguageTag {get;set;}="";
        public string LanguageName {get;set;}="";
        public string DisplayName {get;set;}="";
    }
}
namespace Roon.Broker {
    public static class Extensions {
        public static Api.DataList<T> ToDataList<T>(IEnumerable<T> values)=>new(values);
    }
}
