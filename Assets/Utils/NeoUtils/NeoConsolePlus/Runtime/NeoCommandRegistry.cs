#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Neo.ConsolePlus
{
    internal static class NeoCommandRegistry
    {
        private static readonly Dictionary<string, NeoCommandInfo> Commands = new Dictionary<string, NeoCommandInfo>(StringComparer.OrdinalIgnoreCase);
        private static NeoCommandInfo[] sortedCommands = new NeoCommandInfo[0];
        private static NeoCommandInfo[] sortedEditorCommands = new NeoCommandInfo[0];
        private static NeoCommandInfo[] sortedRuntimeCommands = new NeoCommandInfo[0];
        private static bool initialized;
        private static int version;
        private static NeoCommandExecutionContext currentExecutionContext = NeoCommandExecutionContext.Runtime;
        private static readonly HashSet<string> LoggedCommandWarnings = new HashSet<string>(StringComparer.Ordinal);

#if UNITY_EDITOR
        private static readonly Dictionary<MethodInfo, CommandSourceLocation> CommandSourceLocationCache = new Dictionary<MethodInfo, CommandSourceLocation>();
        private static readonly CommandDiagnosticSourceTraceMode CommandSourceTraceMode = CommandDiagnosticSourceTraceMode.DeepFallback;
#endif

        public static int Version
        {
            get { return version; }
        }

        internal static NeoCommandExecutionContext CurrentExecutionContext
        {
            get { return currentExecutionContext; }
        }

        public static void Refresh()
        {
            ClearRegistryCache();

#if UNITY_EDITOR
            RegisterCommandsFromEditorTypeCache();
#else
            RegisterCommandsFromLoadedAssemblies();
#endif

            RebuildSortedCaches();
            initialized = true;
            version++;
        }

        internal static void MarkDirty()
        {
            initialized = false;
            Commands.Clear();
            sortedCommands = new NeoCommandInfo[0];
            sortedEditorCommands = new NeoCommandInfo[0];
            sortedRuntimeCommands = new NeoCommandInfo[0];
            NeoCommandTargetCache.Clear();
            version++;
        }

        private static void ClearRegistryCache()
        {
            Commands.Clear();
            sortedCommands = new NeoCommandInfo[0];
            sortedEditorCommands = new NeoCommandInfo[0];
            sortedRuntimeCommands = new NeoCommandInfo[0];
            NeoCommandTypeUtility.ClearCache();
#if UNITY_EDITOR
            CommandSourceLocationCache.Clear();
#endif
        }

        private static void RebuildSortedCaches()
        {
            sortedCommands = BuildSortedCommandCache();
            sortedEditorCommands = BuildSortedCommandCache(NeoCommandExecutionContext.Editor);
            sortedRuntimeCommands = BuildSortedCommandCache(NeoCommandExecutionContext.Runtime);
            NeoCommandTargetCache.Clear();
        }

#if UNITY_EDITOR
        private static void RegisterCommandsFromEditorTypeCache()
        {
            HashSet<MethodInfo> visitedMethods = new HashSet<MethodInfo>();
            RegisterCommandsFromEditorTypeCache<NeoCommandAttribute>(visitedMethods);
            RegisterCommandsFromEditorTypeCache<NeoCommandEditorOnlyAttribute>(visitedMethods);
            RegisterCommandsFromEditorTypeCache<NeoCommandRuntimeOnlyAttribute>(visitedMethods);
        }

        private static void RegisterCommandsFromEditorTypeCache<TAttribute>(HashSet<MethodInfo> visitedMethods) where TAttribute : Attribute
        {
            UnityEditor.TypeCache.MethodCollection methods = UnityEditor.TypeCache.GetMethodsWithAttribute<TAttribute>();

            foreach (MethodInfo method in methods)
            {
                if (method == null || !visitedMethods.Add(method))
                    continue;

                RegisterCommandFromMethod(method);
            }
        }
#endif

        private static void RegisterCommandsFromLoadedAssemblies()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null || assembly.IsDynamic)
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type == null)
                        continue;

                    RegisterCommandsFromType(type);
                }
            }
        }

        public static NeoCommandInfo[] GetCommands()
        {
            EnsureInitialized();
            return sortedCommands;
        }

        public static NeoCommandInfo[] GetCommands(NeoCommandExecutionContext context)
        {
            EnsureInitialized();
            return context == NeoCommandExecutionContext.Editor ? sortedEditorCommands : sortedRuntimeCommands;
        }

        private static NeoCommandInfo[] BuildSortedCommandCache()
        {
            NeoCommandInfo[] result = new NeoCommandInfo[Commands.Count];
            Commands.Values.CopyTo(result, 0);
            Array.Sort(result, CompareCommandsByName);
            return result;
        }

        private static NeoCommandInfo[] BuildSortedCommandCache(NeoCommandExecutionContext context)
        {
            List<NeoCommandInfo> filtered = new List<NeoCommandInfo>();
            foreach (NeoCommandInfo command in Commands.Values)
            {
                if (command != null && command.IsAllowedInContext(context))
                    filtered.Add(command);
            }

            NeoCommandInfo[] result = filtered.ToArray();
            Array.Sort(result, CompareCommandsByName);
            return result;
        }

        private static int CompareCommandsByName(NeoCommandInfo left, NeoCommandInfo right)
        {
            string leftName = left != null ? left.Name : string.Empty;
            string rightName = right != null ? right.Name : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetCommand(string name, out NeoCommandInfo command)
        {
            EnsureInitialized();
            return Commands.TryGetValue(name ?? string.Empty, out command);
        }

        public static bool TryGetCommand(string name, NeoCommandExecutionContext context, out NeoCommandInfo command)
        {
            EnsureInitialized();
            if (!Commands.TryGetValue(name ?? string.Empty, out command))
                return false;

            if (command == null || !command.IsAllowedInContext(context))
            {
                command = null;
                return false;
            }

            return true;
        }

        public static NeoCommandResult Execute(string input)
        {
            return Execute(input, NeoCommandExecutionContext.Runtime);
        }

        public static NeoCommandResult Execute(string input, NeoCommandExecutionContext context)
        {
            EnsureInitialized();

            string safeInput = (input ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(safeInput))
                return NeoCommandResult.Fail("No command entered.");

            if (!safeInput.StartsWith(NeoCommandAutoComplete.CommandPrefix.ToString(), StringComparison.Ordinal))
                return NeoCommandResult.Fail("Commands must start with '/'.");

            safeInput = safeInput.Substring(1).TrimStart();
            string[] parts = NeoCommandParser.SplitArguments(safeInput);
            if (parts.Length == 0)
                return NeoCommandResult.Fail("No command entered.");

            string commandName = parts[0];
            NeoCommandInfo command;
            if (!TryGetCommand(commandName, context, out command))
                return NeoCommandResult.Fail("Command not found in " + context + " context: " + commandName);

            NeoCommandExecutionContext previousContext = currentExecutionContext;
            currentExecutionContext = context;
            try
            {
                if (command.IsStatic)
                    return ExecuteStaticCommand(command, parts);

                if (command.IsAllVariant)
                    return ExecuteInstanceCommandOnAllTargets(command, parts);

                return ExecuteInstanceCommandOnSingleTarget(command, parts);
            }
            finally
            {
                currentExecutionContext = previousContext;
            }
        }

        private static NeoCommandResult ExecuteStaticCommand(NeoCommandInfo command, string[] parts)
        {
            ParameterInfo[] parameters = command.Parameters;
            int providedArgumentCount = parts.Length - 1;
            if (providedArgumentCount != parameters.Length)
                return InvalidArgumentCount(command, parameters.Length, providedArgumentCount);

            object[] arguments;
            NeoCommandResult parseError;
            if (!TryBuildArguments(parts, 1, parameters, out arguments, out parseError))
                return parseError;

            return InvokeCommand(command, null, arguments, string.Empty);
        }

        private static NeoCommandResult ExecuteInstanceCommandOnSingleTarget(NeoCommandInfo command, string[] parts)
        {
            ParameterInfo[] parameters = command.Parameters;
            int providedArgumentCount = parts.Length - 1;

            if (providedArgumentCount == parameters.Length + 1)
            {
                string explicitTargetToken = parts[1];
                return ExecuteInstanceCommandWithExplicitTarget(command, parts, parameters, explicitTargetToken, 2);
            }

            if (providedArgumentCount == parameters.Length)
            {
                if (providedArgumentCount > 0 && LooksLikeExplicitTargetArgument(command, parts[1]))
                    return MissingArgumentsAfterTarget(command, parameters.Length, providedArgumentCount - 1);

                object[] implicitArguments;
                NeoCommandResult parseError;
                if (!TryBuildArguments(parts, 1, parameters, out implicitArguments, out parseError))
                    return parseError;

                MonoBehaviour implicitTarget;
                NeoCommandResult targetError;
                if (!TryResolveSingleTarget(command, string.Empty, out implicitTarget, out targetError))
                    return targetError;

                string implicitSuffix = " on " + GetTargetDisplayName(implicitTarget);
                return InvokeCommand(command, implicitTarget, implicitArguments, implicitSuffix);
            }

            if (providedArgumentCount > 0 && providedArgumentCount < parameters.Length + 1 && LooksLikeExplicitTargetArgument(command, parts[1]))
                return MissingArgumentsAfterTarget(command, parameters.Length, providedArgumentCount - 1);

            return NeoCommandResult.Fail("Invalid argument count. Expected " + parameters.Length + " argument(s), or " + (parameters.Length + 1) + " with target. Usage: " + command.Signature);
        }

        private static NeoCommandResult ExecuteInstanceCommandWithExplicitTarget(NeoCommandInfo command, string[] parts, ParameterInfo[] parameters, string targetToken, int methodArgumentStartIndex)
        {
            MonoBehaviour target;
            NeoCommandResult targetError;
            if (!TryResolveSingleTarget(command, targetToken, out target, out targetError))
                return targetError;

            object[] arguments;
            NeoCommandResult parseError;
            if (!TryBuildArguments(parts, methodArgumentStartIndex, parameters, out arguments, out parseError))
                return parseError;

            string suffix = " on " + GetTargetDisplayName(target);
            return InvokeCommand(command, target, arguments, suffix);
        }

        private static NeoCommandResult ExecuteInstanceCommandOnAllTargets(NeoCommandInfo command, string[] parts)
        {
            ParameterInfo[] parameters = command.Parameters;
            int providedArgumentCount = parts.Length - 1;
            if (providedArgumentCount != parameters.Length)
                return InvalidArgumentCount(command, parameters.Length, providedArgumentCount);

            object[] arguments;
            NeoCommandResult parseError;
            if (!TryBuildArguments(parts, 1, parameters, out arguments, out parseError))
                return parseError;

            MonoBehaviour[] targets = FindCommandTargets(command.TargetType);
            if (targets.Length == 0)
                return NeoCommandResult.Fail("No active instances found for target: " + GetTargetTypeName(command));

            StringBuilder returnValues = new StringBuilder();
            bool hasReturnValues = command.Method.ReturnType != typeof(void);

            for (int i = 0; i < targets.Length; i++)
            {
                MonoBehaviour target = targets[i];
                NeoCommandResult result = InvokeCommand(command, target, arguments, " on " + GetTargetDisplayName(target));
                if (!result.Success)
                    return NeoCommandResult.Fail(GetTargetDisplayName(target) + ": " + result.Message);

                if (hasReturnValues)
                {
                    if (returnValues.Length > 0)
                        returnValues.AppendLine();

                    returnValues.Append(GetTargetDisplayName(target)).Append(": ").Append(result.Message);
                }
            }

            if (hasReturnValues)
                return NeoCommandResult.Ok(returnValues.ToString());

            return NeoCommandResult.Ok("Executed: " + command.Name + " on " + targets.Length + " target(s).");
        }

        private static bool TryBuildArguments(string[] parts, int startIndex, ParameterInfo[] parameters, out object[] arguments, out NeoCommandResult errorResult)
        {
            arguments = new object[parameters.Length];
            errorResult = default(NeoCommandResult);

            for (int i = 0; i < parameters.Length; i++)
            {
                int partIndex = startIndex + i;
                object parsedValue;
                string error;
                if (!TryParseValue(parts[partIndex], parameters[i].ParameterType, out parsedValue, out error))
                {
                    errorResult = NeoCommandResult.Fail("Invalid value for parameter '" + parameters[i].Name + "': " + error);
                    return false;
                }

                arguments[i] = parsedValue;
            }

            return true;
        }

        private static NeoCommandResult InvalidArgumentCount(NeoCommandInfo command, int expected, int received)
        {
            return NeoCommandResult.Fail("Invalid argument count. Expected " + expected + ", received " + received + ". Usage: " + command.Signature);
        }

        private static NeoCommandResult MissingArgumentsAfterTarget(NeoCommandInfo command, int expectedMethodArguments, int receivedMethodArguments)
        {
            return NeoCommandResult.Fail("Invalid argument count. The first argument looks like a target, but the command is missing method argument(s). Expected " + expectedMethodArguments + " argument(s) after target, received " + receivedMethodArguments + ". Usage: " + command.Signature);
        }

        private static bool LooksLikeExplicitTargetArgument(NeoCommandInfo command, string possibleTargetToken)
        {
            if (command == null || string.IsNullOrEmpty(possibleTargetToken))
                return false;

            MonoBehaviour[] targets = FindCommandTargets(command.TargetType);
            for (int i = 0; i < targets.Length; i++)
            {
                if (TargetMatches(targets[i], possibleTargetToken))
                    return true;
            }

            return false;
        }

        private static NeoCommandResult InvokeCommand(NeoCommandInfo command, object target, object[] arguments, string successSuffix)
        {
            try
            {
                object returnValue = command.Method.Invoke(target, arguments);
                if (command.Method.ReturnType == typeof(void))
                    return NeoCommandResult.Ok("Executed: " + command.Name + successSuffix);

                return NeoCommandResult.Ok(returnValue != null ? returnValue.ToString() : "null");
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                return NeoCommandResult.Fail(inner.GetType().Name + ": " + inner.Message);
            }
            catch (Exception exception)
            {
                return NeoCommandResult.Fail(exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static bool TryResolveSingleTarget(NeoCommandInfo command, string targetToken, out MonoBehaviour target, out NeoCommandResult errorResult)
        {
            target = null;
            errorResult = default(NeoCommandResult);

            MonoBehaviour[] targets = FindCommandTargets(command.TargetType);
            if (targets.Length == 0)
            {
                errorResult = NeoCommandResult.Fail("No active instances found for target: " + GetTargetTypeName(command));
                return false;
            }

            if (string.IsNullOrEmpty(targetToken))
            {
                if (targets.Length == 1)
                {
                    target = targets[0];
                    return true;
                }

                errorResult = NeoCommandResult.Fail("Multiple active instances found. Specify the target name or instance ID.");
                return false;
            }

            List<MonoBehaviour> matches = new List<MonoBehaviour>();
            for (int i = 0; i < targets.Length; i++)
            {
                if (TargetMatches(targets[i], targetToken))
                    matches.Add(targets[i]);
            }

            if (matches.Count == 0)
            {
                errorResult = NeoCommandResult.Fail("Target not found: " + targetToken);
                return false;
            }

            if (matches.Count > 1)
            {
                errorResult = NeoCommandResult.Fail("Multiple targets match '" + targetToken + "'. Use a GameObject or component instance ID.");
                return false;
            }

            target = matches[0];
            return true;
        }

        private static MonoBehaviour[] FindCommandTargets(Type targetType)
        {
            return NeoCommandTargetCache.FindActiveTargets(targetType);
        }

        private static bool TargetMatches(MonoBehaviour target, string targetToken)
        {
            if (target == null || string.IsNullOrEmpty(targetToken))
                return false;

            string token = Unquote(targetToken.Trim());
            GameObject gameObject = target.gameObject;

            if (string.Equals(GetTargetDisplayName(target), token, StringComparison.OrdinalIgnoreCase))
                return true;

            int hashIndex = token.LastIndexOf('#');
            if (hashIndex >= 0 && hashIndex < token.Length - 1)
                token = token.Substring(hashIndex + 1);
            else if (token.StartsWith("#", StringComparison.Ordinal))
                token = token.Substring(1);

            if (gameObject != null && string.Equals(gameObject.name, token, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(target.name, token, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ObjectIdentifierMatches(target, token))
                return true;

            if (ObjectIdentifierMatches(gameObject, token))
                return true;

            return false;
        }

        private static bool ObjectIdentifierMatches(UnityEngine.Object unityObject, string token)
        {
            if (unityObject == null || string.IsNullOrEmpty(token))
                return false;

            string normalizedToken = NormalizeIdentifierToken(token);
            if (string.IsNullOrEmpty(normalizedToken))
                return false;

            string identifier = GetUnityObjectIdentifier(unityObject);
            if (string.IsNullOrEmpty(identifier))
                return false;

            return string.Equals(identifier, normalizedToken, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeIdentifierToken(string token)
        {
            string value = (token ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int hashIndex = value.LastIndexOf('#');
            if (hashIndex >= 0 && hashIndex < value.Length - 1)
                value = value.Substring(hashIndex + 1);
            else if (value.StartsWith("#", StringComparison.Ordinal))
                value = value.Substring(1);

            return value.Trim();
        }

        private static string GetUnityObjectIdentifier(UnityEngine.Object unityObject)
        {
            return NeoUnityObjectUtility.GetObjectIdentifier(unityObject);
        }

        private static string GetTargetDisplayName(MonoBehaviour target)
        {
            if (target == null)
                return "null";

            GameObject gameObject = target.gameObject;
            string objectName = gameObject != null ? gameObject.name : target.name;
            return objectName;
        }

        private static string GetTargetTypeName(NeoCommandInfo command)
        {
            if (command == null || command.TargetType == null)
                return "unknown";

            return command.TargetType.Name;
        }

        private static string GetCommandMethodTrace(MethodInfo method)
        {
            if (method == null)
                return "unknown method";

            Type declaringType = method.DeclaringType;
            string typeName = declaringType != null ? declaringType.FullName : "unknown type";
            return typeName + "." + method.Name + "()";
        }

        private static string BuildIgnoredCommandWarning(string commandName, MethodInfo method, string reason)
        {
            return "[NeoConsolePlus] Ignored command '" + commandName + "' because " + reason;
        }

        private static string BuildIgnoredCommandMethodWarning(MethodInfo method, string reason)
        {
            return "[NeoConsolePlus] Ignored command method '" + GetCommandMethodTrace(method) + "' because " + reason;
        }

        private static void LogIgnoredCommandWarning(string commandName, MethodInfo method, string reason)
        {
            LogCommandWarning(method, BuildIgnoredCommandWarning(commandName, method, reason));
        }

        private static void LogIgnoredCommandMethodWarning(MethodInfo method, string reason)
        {
            LogCommandWarning(method, BuildIgnoredCommandMethodWarning(method, reason));
        }

        private static void LogCommandWarning(MethodInfo method, string message)
        {
            string warningKey = BuildCommandWarningKey(method, message);
            if (!LoggedCommandWarnings.Add(warningKey))
                return;

#if UNITY_EDITOR
            string sessionKey = "NeoConsolePlus.CommandWarning." + warningKey.GetHashCode().ToString(CultureInfo.InvariantCulture);
            if (UnityEditor.SessionState.GetBool(sessionKey, false))
                return;

            UnityEditor.SessionState.SetBool(sessionKey, true);

            UnityEngine.Object context = GetCommandScriptContext(method);
            string sourceTrace = GetCommandSourceTrace(method);

            if (!string.IsNullOrEmpty(sourceTrace))
                message += "\n" + sourceTrace;

            if (context != null)
            {
                LogCommandWarningMessage(message, context);
                return;
            }
#endif

            LogCommandWarningMessage(message, null);
        }

        private static void LogCommandWarningMessage(string message, UnityEngine.Object context)
        {
#if UNITY_EDITOR
            StackTraceLogType previousStackTrace = Application.GetStackTraceLogType(LogType.Warning);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            try
            {
                if (context != null)
                    Debug.LogWarning(message, context);
                else
                    Debug.LogWarning(message);
            }
            finally
            {
                Application.SetStackTraceLogType(LogType.Warning, previousStackTrace);
            }
#else
            Debug.LogWarning(message);
#endif
        }

        private static string BuildCommandWarningKey(MethodInfo method, string message)
        {
            string methodKey = BuildStableMethodKey(method);
            return methodKey + "|" + (message ?? string.Empty);
        }

        private static string BuildStableMethodKey(MethodInfo method)
        {
            if (method == null)
                return "<unknown>";

            StringBuilder builder = new StringBuilder();
            Type declaringType = method.DeclaringType;
            builder.Append(declaringType != null ? declaringType.FullName : "<unknown type>");
            builder.Append('.');
            builder.Append(method.Name);
            builder.Append('(');

            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');

                Type parameterType = parameters[i].ParameterType;
                builder.Append(parameterType != null ? parameterType.FullName : "<unknown>");
            }

            builder.Append(')');
            return builder.ToString();
        }

#if UNITY_EDITOR
        private enum CommandDiagnosticSourceTraceMode
        {
            FastOnly,
            DeepFallback
        }

        private struct CommandSourceLocation
        {
            public UnityEngine.Object Context;
            public string AssetPath;
            public int Line;
            public bool Found;
        }

        private static UnityEngine.Object GetCommandScriptContext(MethodInfo method)
        {
            CommandSourceLocation location = GetCommandSourceLocation(method);
            return location.Found ? location.Context : null;
        }

        private static string GetCommandSourceTrace(MethodInfo method)
        {
            CommandSourceLocation location = GetCommandSourceLocation(method);
            if (!location.Found || string.IsNullOrEmpty(location.AssetPath) || location.Line <= 0)
                return string.Empty;

            return "(at " + location.AssetPath + ":" + location.Line.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private static CommandSourceLocation GetCommandSourceLocation(MethodInfo method)
        {
            if (method == null)
                return new CommandSourceLocation();

            CommandSourceLocation cached;
            if (CommandSourceLocationCache.TryGetValue(method, out cached))
                return cached;

            CommandSourceLocation resolved = ResolveCommandSourceLocation(method);
            CommandSourceLocationCache[method] = resolved;
            return resolved;
        }

        private static CommandSourceLocation ResolveCommandSourceLocation(MethodInfo method)
        {
            Type declaringType = method.DeclaringType;
            if (declaringType == null)
                return new CommandSourceLocation();

            // Fast path: standard Unity layout where the script file name matches the type name.
            string[] scriptGuids = UnityEditor.AssetDatabase.FindAssets(declaringType.Name + " t:MonoScript");
            CommandSourceLocation location;
            if (TryResolveCommandSourceLocationFromGuids(method, scriptGuids, out location))
                return location;

            if (CommandSourceTraceMode != CommandDiagnosticSourceTraceMode.DeepFallback)
                return new CommandSourceLocation();

            // Detailed fallback: only runs for deduplicated command diagnostics that failed
            // the fast lookup. It is more expensive, but keeps ignored-command logs clickable
            // even when the file name does not match the class name.
            string[] allScriptGuids = UnityEditor.AssetDatabase.FindAssets("t:MonoScript");
            if (TryResolveCommandSourceLocationFromGuids(method, allScriptGuids, out location))
                return location;

            return new CommandSourceLocation();
        }

        private static bool TryResolveCommandSourceLocationFromGuids(MethodInfo method, string[] scriptGuids, out CommandSourceLocation location)
        {
            location = new CommandSourceLocation();

            if (scriptGuids == null || scriptGuids.Length == 0)
                return false;

            Type declaringType = method.DeclaringType;
            if (declaringType == null)
                return false;

            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                UnityEditor.MonoScript script = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(assetPath);
                if (script == null)
                    continue;

                Type scriptClass = script.GetClass();
                bool classMatches = scriptClass == declaringType;

                if (!classMatches && !ScriptFileCouldContainCommand(assetPath, declaringType, method))
                    continue;

                int line = FindCommandDeclarationLine(assetPath, method);
                if (line <= 0 && !classMatches)
                    continue;

                location.Context = script;
                location.AssetPath = assetPath;
                location.Line = line > 0 ? line : 1;
                location.Found = true;
                return true;
            }

            return false;
        }

        private static bool ScriptFileCouldContainCommand(string assetPath, Type declaringType, MethodInfo method)
        {
            if (string.IsNullOrEmpty(assetPath) || declaringType == null || method == null || !System.IO.File.Exists(assetPath))
                return false;

            string text;
            try
            {
                text = System.IO.File.ReadAllText(assetPath);
            }
            catch
            {
                return false;
            }

            return text.IndexOf(declaringType.Name, StringComparison.Ordinal) >= 0 &&
                   text.IndexOf(method.Name, StringComparison.Ordinal) >= 0 &&
                   text.IndexOf("NeoCommand", StringComparison.Ordinal) >= 0;
        }

        private static int FindCommandDeclarationLine(string assetPath, MethodInfo method)
        {
            if (string.IsNullOrEmpty(assetPath) || method == null || !System.IO.File.Exists(assetPath))
                return 0;

            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(assetPath);
            }
            catch
            {
                return 0;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf(method.Name, StringComparison.Ordinal) < 0 || line.IndexOf("(", StringComparison.Ordinal) < 0)
                    continue;

                int attributeLine = FindNearestCommandAttributeLine(lines, i);
                return attributeLine > 0 ? attributeLine : i + 1;
            }

            return 0;
        }

        private static int FindNearestCommandAttributeLine(string[] lines, int methodLineIndex)
        {
            int firstLineToCheck = Math.Max(0, methodLineIndex - 12);

            for (int i = methodLineIndex - 1; i >= firstLineToCheck; i--)
            {
                string line = lines[i];
                if (line.IndexOf("NeoCommand", StringComparison.Ordinal) >= 0)
                    return i + 1;

                if (line.IndexOf("}", StringComparison.Ordinal) >= 0)
                    break;
            }

            return 0;
        }
#endif

        public static string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(char)) return "char";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(long)) return "long";
            if (type.IsEnum) return type.Name;
            return type.Name;
        }

        public static bool IsCustomJsonParameterType(Type type)
        {
            if (type == null)
                return false;

            if (type == typeof(string) || type == typeof(char) || type == typeof(int) || type == typeof(float) ||
                type == typeof(double) || type == typeof(bool) || type == typeof(long) || type.IsEnum)
                return false;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            if (type.IsInterface || type.IsAbstract || type.IsPointer)
                return false;

            if (type.IsArray || IsDictionaryType(type) || IsCollectionType(type))
                return false;

            return type.IsClass || (type.IsValueType && !type.IsPrimitive);
        }

        internal static void EnsureInitialized()
        {
            if (!initialized)
                Refresh();
        }

        private static void RegisterCommandsFromType(Type type)
        {
            if (type == null)
                return;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            MethodInfo[] methods = type.GetMethods(Flags);

            for (int i = 0; i < methods.Length; i++)
                RegisterCommandFromMethod(methods[i]);
        }

        private static void RegisterCommandFromMethod(MethodInfo method)
        {
            if (method == null)
                return;

            Type type = method.DeclaringType;
            if (type == null)
                return;

            NeoCommandScope attributeScope;
            if (!TryGetCommandAttribute(method, out string attributeName, out string attributeDescription, out attributeScope))
                return;

            string commandName = string.IsNullOrEmpty(attributeName) ? method.Name : attributeName.Trim();
            if (string.IsNullOrEmpty(commandName))
                return;

            if (commandName.StartsWith(NeoCommandAutoComplete.CommandPrefix.ToString(), StringComparison.Ordinal))
            {
                LogIgnoredCommandWarning(commandName, method, "command names must not start with '/'. Add '/' only when typing the command in the console.");
                return;
            }

            if (ContainsWhitespace(commandName))
            {
                LogIgnoredCommandWarning(commandName, method, "command names cannot contain whitespace. Use '.', '_' or '-' instead.");
                return;
            }

            if (IsReservedBuiltInCommandName(commandName) && !IsReservedBuiltInCommandOwner(method))
            {
                LogIgnoredCommandWarning(commandName, method, "the 'neo.' prefix is reserved for NeoConsolePlus built-in commands.");
                return;
            }

            if (method.ContainsGenericParameters)
            {
                LogIgnoredCommandWarning(commandName, method, "generic command methods are not supported.");
                return;
            }

            if (!method.IsStatic && !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                LogIgnoredCommandWarning(commandName, method, "non-static commands are supported only on MonoBehaviour types.");
                return;
            }

            if (!method.IsStatic && type.IsAbstract)
            {
                LogIgnoredCommandWarning(commandName, method, "instance commands cannot be declared on abstract MonoBehaviour types.");
                return;
            }

            string unsupportedParameter;
            if (!ParametersAreSupported(method, out unsupportedParameter))
            {
                LogIgnoredCommandWarning(commandName, method, "unsupported parameter type: " + unsupportedParameter + ".");
                return;
            }

            if (Commands.ContainsKey(commandName))
            {
                LogIgnoredCommandWarning(commandName, method, "another command with the same name was already registered.");
                return;
            }

            Commands.Add(commandName, new NeoCommandInfo(commandName, attributeDescription, method, false, attributeScope));

            if (!method.IsStatic && !commandName.EndsWith(".All", StringComparison.OrdinalIgnoreCase))
                RegisterAllVariant(commandName, attributeDescription, method, attributeScope);
        }

        private static void RegisterAllVariant(string commandName, string description, MethodInfo method, NeoCommandScope scope)
        {
            string allName = commandName + ".All";
            if (Commands.ContainsKey(allName))
            {
                LogIgnoredCommandWarning(allName, method, "the generated .All variant conflicts with an existing command.");
                return;
            }

            string allDescription = string.IsNullOrEmpty(description)
                ? "Executes " + commandName + " on all active instances."
                : description + " (all active instances)";

            Commands.Add(allName, new NeoCommandInfo(allName, allDescription, method, true, scope));
        }


        private static bool TryGetCommandAttribute(MethodInfo method, out string name, out string description, out NeoCommandScope scope)
        {
            name = string.Empty;
            description = string.Empty;
            scope = NeoCommandScope.Both;

            NeoCommandAttribute bothAttribute = GetSingleAttribute<NeoCommandAttribute>(method);
            NeoCommandEditorOnlyAttribute editorOnlyAttribute = GetSingleAttribute<NeoCommandEditorOnlyAttribute>(method);
            NeoCommandRuntimeOnlyAttribute runtimeOnlyAttribute = GetSingleAttribute<NeoCommandRuntimeOnlyAttribute>(method);

            int attributeCount = 0;
            if (bothAttribute != null) attributeCount++;
            if (editorOnlyAttribute != null) attributeCount++;
            if (runtimeOnlyAttribute != null) attributeCount++;

            if (attributeCount == 0)
                return false;

            if (attributeCount > 1)
            {
                LogIgnoredCommandMethodWarning(method, "it has more than one NeoCommand attribute.");
                return false;
            }

            if (bothAttribute != null)
            {
                name = bothAttribute.Name;
                description = bothAttribute.Description;
                scope = bothAttribute.Scope;
                return true;
            }

            if (editorOnlyAttribute != null)
            {
                name = editorOnlyAttribute.Name;
                description = editorOnlyAttribute.Description;
                scope = editorOnlyAttribute.Scope;
                return true;
            }

            name = runtimeOnlyAttribute.Name;
            description = runtimeOnlyAttribute.Description;
            scope = runtimeOnlyAttribute.Scope;
            return true;
        }

        private static T GetSingleAttribute<T>(MethodInfo method) where T : Attribute
        {
            object[] attributes = method.GetCustomAttributes(typeof(T), false);
            if (attributes == null || attributes.Length == 0)
                return null;

            return attributes[0] as T;
        }

        private static bool ContainsWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private static bool IsReservedBuiltInCommandName(string commandName)
        {
            return !string.IsNullOrEmpty(commandName) &&
                   commandName.StartsWith("neo.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReservedBuiltInCommandOwner(MethodInfo method)
        {
            if (method == null || method.DeclaringType == null)
                return false;

            Assembly assembly = method.DeclaringType.Assembly;
            if (assembly == null)
                return false;

            string assemblyName = assembly.GetName().Name;
            return string.Equals(assemblyName, "Neo.ConsolePlus", StringComparison.Ordinal) ||
                   string.Equals(assemblyName, "Neo.ConsolePlus.Editor", StringComparison.Ordinal);
        }

        private static bool ParametersAreSupported(MethodInfo method, out string unsupportedParameter)
        {
            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                Type type = parameters[i].ParameterType;
                if (IsSupportedParameterType(type))
                    continue;

                unsupportedParameter = type.FullName;
                return false;
            }

            unsupportedParameter = string.Empty;
            return true;
        }

        private static bool IsSupportedParameterType(Type type)
        {
            return type == typeof(string) ||
                   type == typeof(char) ||
                   type == typeof(int) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(bool) ||
                   type == typeof(long) ||
                   type.IsEnum ||
                   IsDictionaryType(type) ||
                   IsCollectionType(type) ||
                   IsCustomJsonParameterType(type);
        }

        private static bool TryParseValue(string rawValue, Type targetType, out object parsedValue, out string error)
        {
            if (targetType == typeof(string))
            {
                parsedValue = rawValue;
                error = string.Empty;
                return true;
            }

            if (targetType == typeof(char))
            {
                string value = IsQuoted(rawValue) ? Unquote(rawValue) : rawValue;
                if (value != null && value.Length == 0)
                {
                    parsedValue = '\0';
                    error = string.Empty;
                    return true;
                }

                if (value != null && value.Length == 1)
                {
                    parsedValue = value[0];
                    error = string.Empty;
                    return true;
                }

                parsedValue = null;
                error = "expected a single character or an empty quoted value";
                return false;
            }

            if (targetType == typeof(int))
            {
                int value;
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    parsedValue = value;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(long))
            {
                long value;
                if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    parsedValue = value;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(float))
            {
                float value;
                if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    parsedValue = value;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(double))
            {
                double value;
                if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    parsedValue = value;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(bool))
            {
                bool value;
                if (bool.TryParse(rawValue, out value))
                {
                    parsedValue = value;
                    error = string.Empty;
                    return true;
                }

                if (rawValue == "1")
                {
                    parsedValue = true;
                    error = string.Empty;
                    return true;
                }

                if (rawValue == "0")
                {
                    parsedValue = false;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType.IsEnum)
            {
                try
                {
                    parsedValue = Enum.Parse(targetType, rawValue, true);
                    error = string.Empty;
                    return true;
                }
                catch
                {
                    parsedValue = null;
                    error = "expected one of: " + string.Join(", ", Enum.GetNames(targetType));
                    return false;
                }
            }
            else if (IsDictionaryType(targetType) || IsCollectionType(targetType) || IsCustomJsonParameterType(targetType))
            {
                if (TryParseJsonLikeValue(rawValue, targetType, out parsedValue, out error))
                    return true;

                error = "expected value for " + GetFriendlyTypeName(targetType) + ". Example: " + NeoCommandAutoComplete.GetSchemaForType(targetType) + ". " + error;
                return false;
            }

            parsedValue = null;
            error = "could not parse '" + rawValue + "' as " + targetType.Name;
            return false;
        }

        private static bool TryParseJsonLikeValue(string rawValue, Type targetType, out object parsedValue, out string error)
        {
            parsedValue = null;
            error = string.Empty;

            if (targetType == null)
            {
                error = "target type is null";
                return false;
            }

            string value = (rawValue ?? string.Empty).Trim();

            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    parsedValue = null;
                    return true;
                }

                error = "null is not valid for " + targetType.Name;
                return false;
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
                return TryParseJsonLikeValue(value, nullableType, out parsedValue, out error);

            if (targetType == typeof(string))
            {
                parsedValue = IsQuoted(value) ? Unquote(value) : value;
                return true;
            }

            if (targetType == typeof(char))
            {
                string charValue = IsQuoted(value) ? Unquote(value) : value;
                if (charValue != null && charValue.Length == 0)
                {
                    parsedValue = '\0';
                    return true;
                }

                if (charValue != null && charValue.Length == 1)
                {
                    parsedValue = charValue[0];
                    return true;
                }

                error = "expected a single character or an empty quoted value";
                return false;
            }

            if (targetType == typeof(int))
            {
                int number;
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    parsedValue = number;
                    return true;
                }

                error = "expected int";
                return false;
            }

            if (targetType == typeof(long))
            {
                long number;
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    parsedValue = number;
                    return true;
                }

                error = "expected long";
                return false;
            }

            if (targetType == typeof(float))
            {
                float number;
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    parsedValue = number;
                    return true;
                }

                error = "expected float";
                return false;
            }

            if (targetType == typeof(double))
            {
                double number;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    parsedValue = number;
                    return true;
                }

                error = "expected double";
                return false;
            }

            if (targetType == typeof(bool))
            {
                bool boolean;
                if (bool.TryParse(value, out boolean))
                {
                    parsedValue = boolean;
                    return true;
                }

                if (value == "1")
                {
                    parsedValue = true;
                    return true;
                }

                if (value == "0")
                {
                    parsedValue = false;
                    return true;
                }

                error = "expected bool";
                return false;
            }

            if (targetType.IsEnum)
            {
                try
                {
                    parsedValue = Enum.Parse(targetType, IsQuoted(value) ? Unquote(value) : value, true);
                    return true;
                }
                catch
                {
                    error = "expected one of: " + string.Join(", ", Enum.GetNames(targetType));
                    return false;
                }
            }

            if (IsDictionaryType(targetType))
                return TryParseDictionary(value, targetType, out parsedValue, out error);

            if (IsCollectionType(targetType))
                return TryParseCollection(value, targetType, out parsedValue, out error);

            if (IsCustomJsonParameterType(targetType))
                return TryParseObject(value, targetType, out parsedValue, out error);

            error = "unsupported type " + targetType.Name;
            return false;
        }

        private static bool TryParseObject(string value, Type targetType, out object parsedValue, out string error)
        {
            parsedValue = null;
            error = string.Empty;

            string content;
            if (!TryGetObjectContent(value, out content))
            {
                error = "expected object literal { ... }";
                return false;
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(targetType);
            }
            catch (Exception exception)
            {
                error = "could not create " + targetType.Name + ": " + exception.Message;
                return false;
            }

            if (string.IsNullOrEmpty(content))
            {
                parsedValue = instance;
                return true;
            }

            FieldInfo[] fields = GetSerializableFields(targetType);
            List<string> tokens = SplitTopLevel(content, ',');
            bool namedFormat = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (FindTopLevelNameValueSeparator(tokens[i]) >= 0)
                {
                    namedFormat = true;
                    break;
                }
            }

            if (namedFormat)
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    string token = (tokens[i] ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(token))
                        continue;

                    int separator = FindTopLevelNameValueSeparator(token);
                    if (separator < 0)
                    {
                        error = "expected name/value pair in object: " + token;
                        return false;
                    }

                    string rawName = token.Substring(0, separator).Trim();
                    string rawFieldValue = token.Substring(separator + 1).Trim();
                    string fieldName = Unquote(rawName);
                    FieldInfo field = FindField(fields, fieldName);
                    if (field == null)
                    {
                        error = "unknown field: " + fieldName;
                        return false;
                    }

                    object fieldValue;
                    string fieldError;
                    if (!TryParseJsonLikeValue(rawFieldValue, field.FieldType, out fieldValue, out fieldError))
                    {
                        error = "field '" + field.Name + "': " + fieldError;
                        return false;
                    }

                    field.SetValue(instance, fieldValue);
                }
            }
            else
            {
                int count = Math.Min(tokens.Count, fields.Length);
                for (int i = 0; i < count; i++)
                {
                    FieldInfo field = fields[i];
                    object fieldValue;
                    string fieldError;
                    if (!TryParseJsonLikeValue(tokens[i], field.FieldType, out fieldValue, out fieldError))
                    {
                        error = "field '" + field.Name + "': " + fieldError;
                        return false;
                    }

                    field.SetValue(instance, fieldValue);
                }
            }

            parsedValue = instance;
            return true;
        }

        private static bool TryParseCollection(string value, Type targetType, out object parsedValue, out string error)
        {
            parsedValue = null;
            error = string.Empty;

            Type elementType;
            if (!TryGetCollectionElementType(targetType, out elementType))
            {
                error = "could not determine collection element type";
                return false;
            }

            string content;
            if (!TryGetArrayContent(value, out content))
            {
                error = "expected array literal [ ... ]";
                return false;
            }

            List<string> tokens = string.IsNullOrEmpty(content) ? new List<string>() : SplitTopLevel(content, ',');

            if (targetType.IsArray)
            {
                Array array = Array.CreateInstance(elementType, tokens.Count);
                for (int i = 0; i < tokens.Count; i++)
                {
                    object elementValue;
                    string elementError;
                    if (!TryParseJsonLikeValue(tokens[i], elementType, out elementValue, out elementError))
                    {
                        error = "element " + i + ": " + elementError;
                        return false;
                    }

                    array.SetValue(elementValue, i);
                }

                parsedValue = array;
                return true;
            }

            object collection;
            if (!TryCreateCollectionInstance(targetType, elementType, out collection, out error))
                return false;

            for (int i = 0; i < tokens.Count; i++)
            {
                object elementValue;
                string elementError;
                if (!TryParseJsonLikeValue(tokens[i], elementType, out elementValue, out elementError))
                {
                    error = "element " + i + ": " + elementError;
                    return false;
                }

                if (!TryAddCollectionElement(collection, elementValue, out error))
                    return false;
            }

            parsedValue = collection;
            return true;
        }

        private static bool TryParseDictionary(string value, Type targetType, out object parsedValue, out string error)
        {
            parsedValue = null;
            error = string.Empty;

            Type keyType;
            Type valueType;
            if (!TryGetDictionaryTypes(targetType, out keyType, out valueType))
            {
                error = "could not determine dictionary key/value types";
                return false;
            }

            string content;
            if (!TryGetObjectContent(value, out content))
            {
                error = "expected dictionary literal { key: value }";
                return false;
            }

            object dictionary;
            if (!TryCreateDictionaryInstance(targetType, keyType, valueType, out dictionary, out error))
                return false;

            if (string.IsNullOrEmpty(content))
            {
                parsedValue = dictionary;
                return true;
            }

            List<string> tokens = SplitTopLevel(content, ',');
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = (tokens[i] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                int separator = FindTopLevelNameValueSeparator(token);
                if (separator < 0)
                {
                    error = "expected key/value pair in dictionary: " + token;
                    return false;
                }

                string rawKey = token.Substring(0, separator).Trim();
                string rawDictionaryValue = token.Substring(separator + 1).Trim();

                object key;
                string keyError;
                if (!TryParseJsonLikeValue(rawKey, keyType, out key, out keyError))
                {
                    error = "dictionary key: " + keyError;
                    return false;
                }

                object dictionaryValue;
                string valueError;
                if (!TryParseJsonLikeValue(rawDictionaryValue, valueType, out dictionaryValue, out valueError))
                {
                    error = "dictionary value for '" + rawKey + "': " + valueError;
                    return false;
                }

                if (!TryAddDictionaryElement(dictionary, key, dictionaryValue, out error))
                    return false;
            }

            parsedValue = dictionary;
            return true;
        }

        private static bool TryGetObjectContent(string value, out string content)
        {
            content = string.Empty;
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                return false;

            content = trimmed.Substring(1, trimmed.Length - 2).Trim();
            return true;
        }

        private static bool TryGetArrayContent(string value, out string content)
        {
            content = string.Empty;
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[trimmed.Length - 1] != ']')
                return false;

            content = trimmed.Substring(1, trimmed.Length - 2).Trim();
            return true;
        }

        private static bool TryCreateCollectionInstance(Type targetType, Type elementType, out object collection, out string error)
        {
            collection = null;
            error = string.Empty;

            Type collectionType = targetType;
            if (targetType.IsInterface || targetType.IsAbstract)
            {
                if (IsGenericTypeDefinition(targetType, typeof(ISet<>)))
                    collectionType = typeof(HashSet<>).MakeGenericType(elementType);
                else
                    collectionType = typeof(List<>).MakeGenericType(elementType);
            }

            try
            {
                collection = Activator.CreateInstance(collectionType);
                return true;
            }
            catch (Exception exception)
            {
                error = "could not create collection " + targetType.Name + ": " + exception.Message;
                return false;
            }
        }

        private static bool TryCreateDictionaryInstance(Type targetType, Type keyType, Type valueType, out object dictionary, out string error)
        {
            dictionary = null;
            error = string.Empty;

            Type dictionaryType = targetType;
            if (targetType.IsInterface || targetType.IsAbstract)
                dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);

            try
            {
                dictionary = Activator.CreateInstance(dictionaryType);
                return true;
            }
            catch (Exception exception)
            {
                error = "could not create dictionary " + targetType.Name + ": " + exception.Message;
                return false;
            }
        }

        private static bool TryAddCollectionElement(object collection, object value, out string error)
        {
            error = string.Empty;

            IList list = collection as IList;
            if (list != null)
            {
                list.Add(value);
                return true;
            }

            MethodInfo addMethod = collection.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                addMethod.Invoke(collection, new[] { value });
                return true;
            }

            MethodInfo enqueueMethod = collection.GetType().GetMethod("Enqueue", BindingFlags.Public | BindingFlags.Instance);
            if (enqueueMethod != null)
            {
                enqueueMethod.Invoke(collection, new[] { value });
                return true;
            }

            MethodInfo pushMethod = collection.GetType().GetMethod("Push", BindingFlags.Public | BindingFlags.Instance);
            if (pushMethod != null)
            {
                pushMethod.Invoke(collection, new[] { value });
                return true;
            }

            error = "collection type does not expose Add, Enqueue or Push";
            return false;
        }

        private static bool TryAddDictionaryElement(object dictionary, object key, object value, out string error)
        {
            error = string.Empty;

            IDictionary nonGenericDictionary = dictionary as IDictionary;
            if (nonGenericDictionary != null)
            {
                nonGenericDictionary.Add(key, value);
                return true;
            }

            MethodInfo addMethod = dictionary.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                addMethod.Invoke(dictionary, new[] { key, value });
                return true;
            }

            error = "dictionary type does not expose Add";
            return false;
        }

        private static bool IsDictionaryType(Type type)
        {
            return NeoCommandTypeUtility.IsDictionaryLike(type);
        }

        private static bool IsCollectionType(Type type)
        {
            return NeoCommandTypeUtility.IsCollectionLike(type);
        }

        private static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            return NeoCommandTypeUtility.TryGetDictionaryTypes(type, out keyType, out valueType);
        }

        private static bool TryGetCollectionElementType(Type type, out Type elementType)
        {
            return NeoCommandTypeUtility.TryGetCollectionElementType(type, out elementType);
        }

        private static bool IsGenericTypeDefinition(Type type, Type genericTypeDefinition)
        {
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition;
        }

        private static string NormalizeJsonLike(string rawValue, Type targetType)
        {
            if (string.IsNullOrEmpty(rawValue))
                return rawValue;

            string trimmed = rawValue.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                return trimmed;

            string content = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return "{}";

            FieldInfo[] fields = GetSerializableFields(targetType);
            List<string> tokens = SplitTopLevel(content, ',');
            bool namedFormat = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (FindTopLevelNameValueSeparator(tokens[i]) >= 0)
                {
                    namedFormat = true;
                    break;
                }
            }

            if (namedFormat)
                return NormalizeNamedObject(tokens, fields);

            return NormalizePositionalObject(tokens, fields);
        }

        private static string NormalizeNamedObject(List<string> tokens, FieldInfo[] fields)
        {
            List<string> pairs = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = (tokens[i] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                int separator = FindTopLevelNameValueSeparator(token);
                if (separator < 0)
                    continue;

                string rawName = token.Substring(0, separator).Trim();
                string rawValue = token.Substring(separator + 1).Trim();
                string fieldName = Unquote(rawName);
                FieldInfo field = FindField(fields, fieldName);
                Type fieldType = field != null ? field.FieldType : typeof(string);

                pairs.Add(QuoteJsonString(fieldName) + ":" + NormalizeJsonValue(rawValue, fieldType));
            }

            return "{" + string.Join(",", pairs.ToArray()) + "}";
        }

        private static string NormalizePositionalObject(List<string> tokens, FieldInfo[] fields)
        {
            if (fields == null || fields.Length == 0)
                return "{" + string.Join(",", tokens.ToArray()) + "}";

            List<string> pairs = new List<string>();
            int count = Math.Min(tokens.Count, fields.Length);
            for (int i = 0; i < count; i++)
            {
                FieldInfo field = fields[i];
                string rawValue = (tokens[i] ?? string.Empty).Trim();
                pairs.Add(QuoteJsonString(field.Name) + ":" + NormalizeJsonValue(rawValue, field.FieldType));
            }

            return "{" + string.Join(",", pairs.ToArray()) + "}";
        }

        private static string NormalizeJsonValue(string rawValue, Type fieldType)
        {
            string value = (rawValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value))
            {
                if (fieldType == typeof(string))
                    return "\"\"";

                return "null";
            }

            if (fieldType == typeof(string) || fieldType == typeof(char))
            {
                if (IsQuoted(value))
                    return QuoteJsonString(Unquote(value));

                return QuoteJsonString(value);
            }

            if (fieldType != null && fieldType.IsEnum)
            {
                if (IsQuoted(value))
                    return QuoteJsonString(Unquote(value));

                return QuoteJsonString(value);
            }

            if (fieldType == typeof(bool))
            {
                if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
                    return "true";

                if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
                    return "false";

                return value.ToLowerInvariant();
            }

            if (IsCustomJsonParameterType(fieldType) && value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal))
                return NormalizeJsonLike(value, fieldType);

            return value;
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            return NeoCommandTypeUtility.GetSerializableFields(type);
        }

        private static FieldInfo FindField(FieldInfo[] fields, string name)
        {
            return NeoCommandTypeUtility.FindSerializableField(fields, name);
        }

        private static List<string> SplitTopLevel(string input, char separator)
        {
            List<string> parts = new List<string>();
            if (string.IsNullOrEmpty(input))
                return parts;

            StringBuilder current = new StringBuilder();
            bool insideQuotes = false;
            char quote = '\0';
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool escaped = i > 0 && input[i - 1] == '\\';

                if (!escaped && (c == '"' || c == '\''))
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }
                }

                if (!insideQuotes)
                {
                    if (c == '{') objectDepth++;
                    else if (c == '}' && objectDepth > 0) objectDepth--;
                    else if (c == '[') arrayDepth++;
                    else if (c == ']' && arrayDepth > 0) arrayDepth--;

                    if (c == separator && objectDepth == 0 && arrayDepth == 0)
                    {
                        parts.Add(current.ToString().Trim());
                        current.Length = 0;
                        continue;
                    }
                }

                current.Append(c);
            }

            parts.Add(current.ToString().Trim());
            return parts;
        }

        private static int FindTopLevelNameValueSeparator(string token)
        {
            if (string.IsNullOrEmpty(token))
                return -1;

            bool insideQuotes = false;
            char quote = '\0';
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                bool escaped = i > 0 && token[i - 1] == '\\';

                if (!escaped && (c == '"' || c == '\''))
                {
                    if (!insideQuotes)
                    {
                        insideQuotes = true;
                        quote = c;
                    }
                    else if (quote == c)
                    {
                        insideQuotes = false;
                        quote = '\0';
                    }
                }

                if (insideQuotes)
                    continue;

                if (c == '{') objectDepth++;
                else if (c == '}' && objectDepth > 0) objectDepth--;
                else if (c == '[') arrayDepth++;
                else if (c == ']' && arrayDepth > 0) arrayDepth--;

                if ((c == ':' || c == ';') && objectDepth == 0 && arrayDepth == 0)
                    return i;
            }

            return -1;
        }

        private static bool IsQuoted(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
                return false;

            return (value[0] == '"' && value[value.Length - 1] == '"') ||
                   (value[0] == '\'' && value[value.Length - 1] == '\'');
        }

        private static string Unquote(string value)
        {
            string safe = (value ?? string.Empty).Trim();
            if (!IsQuoted(safe))
                return safe;

            return safe.Substring(1, safe.Length - 2);
        }

        private static string QuoteJsonString(string value)
        {
            string safe = value ?? string.Empty;
            return "\"" + safe.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
#endif
