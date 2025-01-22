using System.Globalization;
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
        result = 0;
        return false;
    }
}
