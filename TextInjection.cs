using System.Windows.Forms; // 需要添加对 System.Windows.Forms 的引用

namespace full_AI_tovch
{
    public static class TextInjection
    {
        public static void Send(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            SendKeys.SendWait(text);
        }
    }
}