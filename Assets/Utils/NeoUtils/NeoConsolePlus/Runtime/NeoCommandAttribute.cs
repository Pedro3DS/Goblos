using System;
using System.Diagnostics;

namespace Neo.ConsolePlus
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal enum NeoCommandScope
    {
        Both,
        EditorOnly,
        RuntimeOnly
    }

    internal enum NeoCommandExecutionContext
    {
        Editor,
        Runtime
    }
#endif

    /// <summary>
    /// Marks a method as a Neo Console command available in both the Editor Console and Runtime Console.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class NeoCommandAttribute : Attribute
    {
        /// <summary>
        /// Creates a console command with the given name and no description.
        /// Command names must not start with '/' and must not contain whitespace.
        /// </summary>
        /// <param name="name">The command name used after the '/' prefix.</param>
        public NeoCommandAttribute(string name)
        {
            Name = name;
            Description = string.Empty;
        }

        /// <summary>
        /// Creates a console command with the given name and description.
        /// Command names must not start with '/' and must not contain whitespace.
        /// </summary>
        /// <param name="name">The command name used after the '/' prefix.</param>
        /// <param name="description">A short explanation shown in help and autocomplete.</param>
        public NeoCommandAttribute(string name, string description)
        {
            Name = name;
            Description = description ?? string.Empty;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal NeoCommandScope Scope
        {
            get { return NeoCommandScope.Both; }
        }
#endif
    }

    /// <summary>
    /// Marks a method as a Neo Console command available only in the Unity Editor Console.
    /// This command will not be listed or executed by the Runtime Console.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class NeoCommandEditorOnlyAttribute : Attribute
    {
        /// <summary>
        /// Creates an editor-only console command with the given name and no description.
        /// Command names must not start with '/' and must not contain whitespace.
        /// </summary>
        /// <param name="name">The command name used after the '/' prefix.</param>
        public NeoCommandEditorOnlyAttribute(string name)
        {
            Name = name;
            Description = string.Empty;
        }

        /// <summary>
        /// Creates an editor-only console command with the given name and description.
        /// Command names must not start with '/' and must not contain whitespace.
        /// </summary>
        /// <param name="name">The command name used after the '/' prefix.</param>
        /// <param name="description">A short explanation shown in help and autocomplete.</param>
        public NeoCommandEditorOnlyAttribute(string name, string description)
        {
            Name = name;
            Description = description ?? string.Empty;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal NeoCommandScope Scope
        {
            get { return NeoCommandScope.EditorOnly; }
        }
#endif
    }

    /// <summary>
    /// Marks a method as a Neo Console command available only in the Runtime Console.
    /// This command will not be listed or executed by the Editor Console.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class NeoCommandRuntimeOnlyAttribute : Attribute
    {
        /// <summary>
        /// Creates a runtime-only console command with the given name and no description.
        /// Command names must not start with '/' and must not contain whitespace.
        /// </summary>
        /// <param name="name">The command name used after the '/' prefix.</param>
        public NeoCommandRuntimeOnlyAttribute(string name)
        {
            Name = name;
            Description = string.Empty;
        }

        /// <summary>
        /// Creates a runtime-only console command with the given name and description.
        /// Command names must not start with '/' and must not contain whitespace.
        /// </summary>
        /// <param name="name">The command name used after the '/' prefix.</param>
        /// <param name="description">A short explanation shown in help and autocomplete.</param>
        public NeoCommandRuntimeOnlyAttribute(string name, string description)
        {
            Name = name;
            Description = description ?? string.Empty;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal NeoCommandScope Scope
        {
            get { return NeoCommandScope.RuntimeOnly; }
        }
#endif
    }
}