#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Reflection;

namespace Neo.ConsolePlus
{
    internal sealed class NeoCommandInfo
    {
        internal NeoCommandInfo(string name, string description, MethodInfo method, bool isAllVariant, NeoCommandScope scope)
        {
            Name = name;
            Description = description ?? string.Empty;
            Method = method;
            Parameters = method.GetParameters();
            TargetType = method.DeclaringType;
            IsStatic = method.IsStatic;
            IsAllVariant = isAllVariant;
            Scope = scope;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
        public ParameterInfo[] Parameters { get; private set; }
        public Type TargetType { get; private set; }
        public bool IsStatic { get; private set; }
        public bool IsAllVariant { get; private set; }
        internal NeoCommandScope Scope { get; private set; }

        public bool IsInstanceCommand
        {
            get { return !IsStatic; }
        }

        public bool RequiresTargetArgument
        {
            get { return IsInstanceCommand && !IsAllVariant; }
        }

        internal MethodInfo Method { get; private set; }

        internal bool IsAllowedInContext(NeoCommandExecutionContext context)
        {
            if (Scope == NeoCommandScope.Both)
                return true;

            if (Scope == NeoCommandScope.EditorOnly)
                return context == NeoCommandExecutionContext.Editor;

            if (Scope == NeoCommandScope.RuntimeOnly)
                return context == NeoCommandExecutionContext.Runtime;

            return false;
        }

        public string Signature
        {
            get
            {
                string signature = "/" + Name;

                if (RequiresTargetArgument)
                    signature += " \"target name\"";

                if (Parameters != null)
                {
                    for (int i = 0; i < Parameters.Length; i++)
                        signature += " <" + NeoCommandRegistry.GetFriendlyTypeName(Parameters[i].ParameterType) + ">";
                }

                return signature;
            }
        }
    }
}
#endif
