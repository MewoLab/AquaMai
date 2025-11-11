using System.Collections.Generic;

namespace AquaMai.Core;

// Unity 2017 兼容
public static class Extensions
{
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue defaultValue = default)
    {
        return dic.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
    {
        key = kvp.Key;
        value = kvp.Value;
    }
}