using System;

namespace AquaMai.Core.Environment;

public static class MelonLogger
{
    public static Action<string> MsgStringAction = null;
    public static Action<object> MsgObjectAction = null;
    public static Action<string> ErrorStringAction = null;
    public static Action<object> ErrorObjectAction = null;
    public static Action<string> WarningStringAction = null;
    public static Action<object> WarningObjectAction = null;

    public static void Msg(string message)
    {
        MsgStringAction?.Invoke(message);
    }

    public static void Msg(object message)
    {
        MsgObjectAction?.Invoke(message);
    }

    public static void Error(string message)
    {
        ErrorStringAction?.Invoke(message);
    }

    public static void Error(object message)
    {
        ErrorObjectAction?.Invoke(message);
    }

    public static void Warning(string message)
    {
        WarningStringAction?.Invoke(message);
    }

    public static void Warning(object message)
    {
        WarningObjectAction?.Invoke(message);
    }
}
