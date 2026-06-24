using System.Threading;

namespace NexivraChatBackend.Services
{
    // Sinh ID tạm thời (âm) duy nhất cho tin nhắn AI đang stream,
    // tránh trùng và tránh cấp phát Random mỗi lần như trước.
    public static class TempMessageId
    {
        private static int _counter = 0;

        public static int Next()
        {
            return Interlocked.Decrement(ref _counter);
        }
    }
}
