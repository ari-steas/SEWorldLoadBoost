
namespace ClientPlugin
{
    static class AccessExtensions
    {
        public static object Call(this object o, string methodName, params object[] args)
        {
            var mi = o.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return mi?.Invoke(o, args);
        }
    }
}
