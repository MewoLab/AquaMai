using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MelonLoader.TinyJSON;

namespace AquaMai.Core.Helpers;

public static class JsonHelper
{
    public static bool TryToInt32(Variant variant, out int result)
    {
        if (variant is ProxyNumber proxyNumber)
        {
            try
            {
                result = proxyNumber.ToInt32(CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {}
        }
        else if (variant is ProxyString proxyString)
        {
            return int.TryParse(proxyString.ToString(), out result);
        }
        result = 0;
        return false;
    }

    public static bool TryToInt64(Variant variant, out long result)
    {
        if (variant is ProxyNumber proxyNumber)
        {
            try
            {
                result = proxyNumber.ToInt64(CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {}
        }
        else if (variant is ProxyString proxyString)
        {
            return long.TryParse(proxyString.ToString(), out result);
        }
        result = 0;
        return false;
    }

    public class DeepEqualityComparer : IEqualityComparer<Variant>
    {
        public bool Equals(Variant a, Variant b) => DeepEqual(a, b);
        public int GetHashCode(Variant a) => a.ToJSON().GetHashCode();
    }


    public static bool DeepEqual(Variant a, Variant b) =>
        (a, b) switch {
            (ProxyArray arrayA, ProxyArray arrayB) => Enumerable.SequenceEqual(arrayA, arrayB, new DeepEqualityComparer()),
            (ProxyObject objectA, ProxyObject objectB) =>
                objectA.Keys.Count == objectB.Keys.Count &&
                objectA.All(pair => objectB.TryGetValue(pair.Key, out var valueB) && DeepEqual(pair.Value, valueB)),
            (ProxyBoolean booleanA, ProxyBoolean booleanB) => booleanA.ToBoolean(null) == booleanB.ToBoolean(null),
            (ProxyNumber numberA, ProxyNumber numberB) => numberA.ToString() == numberB.ToString(),
            (ProxyString stringA, ProxyString stringB) => stringA.ToString() == stringB.ToString(),
            (null, null) => true,
            _ => false
        };
}
