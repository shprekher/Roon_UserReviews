using Mono.Cecil;
using Mono.Cecil.Cil;

/// <summary>Embeds only our small, non-generic helper types in the broker.</summary>
sealed class Transplant(ModuleDefinition target) {
    readonly Dictionary<string,TypeDefinition> types=new();
    readonly Dictionary<string,MethodDefinition> methods=new();
    readonly Dictionary<string,FieldDefinition> fields=new();
    readonly List<TypeDefinition> originals=new();

    public void Copy(TypeDefinition root) {
        DeclareType(root,null);
        foreach(var original in originals)DeclareMembers(original);
        foreach(var original in originals)foreach(var method in original.Methods)CopyBody(method);
    }
    void DeclareType(TypeDefinition original,TypeDefinition? parent) {
        if(original.HasGenericParameters)throw new NotSupportedException("Helper generic types are not supported");
        var type=new TypeDefinition(original.Namespace,original.Name,original.Attributes,target.TypeSystem.Object);
        types.Add(original.FullName,type);originals.Add(original);
        if(parent==null)target.Types.Add(type);else parent.NestedTypes.Add(type);
        foreach(var child in original.NestedTypes)DeclareType(child,type);
    }
    void DeclareMembers(TypeDefinition original) {
        var type=types[original.FullName];type.BaseType=Type(original.BaseType);
        foreach(var field in original.Fields) {
            var copy=new FieldDefinition(field.Name,field.Attributes,Type(field.FieldType));
            if(field.HasConstant)copy.Constant=field.Constant;
            fields.Add(field.FullName,copy);type.Fields.Add(copy);
        }
        foreach(var method in original.Methods) {
            if(method.HasGenericParameters)throw new NotSupportedException("Helper generic methods are not supported");
            var copy=new MethodDefinition(method.Name,method.Attributes,Type(method.ReturnType)) {
                ImplAttributes=method.ImplAttributes,CallingConvention=method.CallingConvention,
            };
            foreach(var parameter in method.Parameters)copy.Parameters.Add(new ParameterDefinition(parameter.Name,parameter.Attributes,Type(parameter.ParameterType)));
            methods.Add(method.FullName,copy);type.Methods.Add(copy);
        }
    }
    TypeReference Type(TypeReference original) {
        if(types.TryGetValue(original.FullName,out var mapped))return mapped;
        if(original is GenericInstanceType generic) {
            var copy=new GenericInstanceType(Type(generic.ElementType));
            foreach(var argument in generic.GenericArguments)copy.GenericArguments.Add(Type(argument));return copy;
        }
        if(original is ArrayType array)return new ArrayType(Type(array.ElementType),array.Rank);
        if(original is ByReferenceType reference)return new ByReferenceType(Type(reference.ElementType));
        if(original is PointerType pointer)return new PointerType(Type(pointer.ElementType));
        if(original is RequiredModifierType required)return new RequiredModifierType(Type(required.ModifierType),Type(required.ElementType));
        if(original is OptionalModifierType optional)return new OptionalModifierType(Type(optional.ModifierType),Type(optional.ElementType));
        return target.ImportReference(original);
    }
    MethodReference Method(MethodReference original) {
        if(methods.TryGetValue(original.FullName,out var mapped))return mapped;
        if(original is GenericInstanceMethod generic) {
            var copy=new GenericInstanceMethod(Method(generic.ElementMethod));
            foreach(var argument in generic.GenericArguments)copy.GenericArguments.Add(Type(argument));return copy;
        }
        return target.ImportReference(original);
    }
    FieldReference Field(FieldReference original)=>fields.TryGetValue(original.FullName,out var mapped)?mapped:target.ImportReference(original);
    void CopyBody(MethodDefinition original) {
        if(!original.HasBody)return;
        var copy=methods[original.FullName];
        copy.Body=new MethodBody(copy){InitLocals=original.Body.InitLocals,MaxStackSize=original.Body.MaxStackSize};
        foreach(var variable in original.Body.Variables)copy.Body.Variables.Add(new VariableDefinition(Type(variable.VariableType)));
        var instructions=new Dictionary<Instruction,Instruction>();
        foreach(var instruction in original.Body.Instructions) {
            var item=Instruction.Create(OpCodes.Nop);item.OpCode=instruction.OpCode;
            instructions.Add(instruction,item);copy.Body.Instructions.Add(item);
        }
        foreach(var instruction in original.Body.Instructions)instructions[instruction].Operand=instruction.Operand switch {
            Instruction branch=>instructions[branch],Instruction[] branches=>branches.Select(i=>instructions[i]).ToArray(),
            VariableDefinition variable=>copy.Body.Variables[variable.Index],
            ParameterDefinition parameter=>parameter.Index<0?copy.Body.ThisParameter:copy.Parameters[parameter.Index],
            MethodReference method=>Method(method),FieldReference field=>Field(field),TypeReference type=>Type(type),
            CallSite=>throw new NotSupportedException("calli is not permitted in the bridge"),
            _=>instruction.Operand,
        };
        Instruction? Map(Instruction? instruction)=>instruction==null?null:instructions[instruction];
        foreach(var handler in original.Body.ExceptionHandlers)copy.Body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType) {
            TryStart=Map(handler.TryStart),TryEnd=Map(handler.TryEnd),HandlerStart=Map(handler.HandlerStart),
            HandlerEnd=Map(handler.HandlerEnd),FilterStart=Map(handler.FilterStart),CatchType=handler.CatchType==null?null:Type(handler.CatchType),
        });
    }
}
