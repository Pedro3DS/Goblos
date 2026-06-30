#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Neo.ConsolePlus
{
    internal struct NeoCommandResult
    {
        public NeoCommandResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; private set; }
        public string Message { get; private set; }

        public static NeoCommandResult Ok(string message)
        {
            return new NeoCommandResult(true, message);
        }

        public static NeoCommandResult Fail(string message)
        {
            return new NeoCommandResult(false, message);
        }
    }
}
#endif
