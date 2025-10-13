using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Mods.Types;
using HarmonyLib;
using JetBrains.Annotations;
using MelonLoader;
using MelonLoader.TinyJSON;
using Monitor;
using Process;
using UnityEngine;
using UnityEngine.Networking;

namespace AquaMai.Mods.Enhancement;

[ConfigSection(
    defaultOn: true,
    exampleHidden: true,
    zh: "加载服务器下发的资源（如果支持）")]
public class ServerResources
{
    [ConfigEntry]
    private static readonly string publicKey =
        "zJo+1YAJXHorMNSs0abC/qQchDX9J7um07mUp+jkYUVh9Y74IUKeSLlSoMrhardJJolQMcy+m7qtdx/xyTmp8pyANBi7xxgUB752SRhHBnK4XQhALd0WmTo4hE6deRHl/SlDxbfZM+c0fW4FMrHUFpHCy+JQoTvJPXkLQRfOCik=";

    [ConfigEntry]
    private static readonly string cacheFile = "LocalAssets/ServerResources";

    private const string FieldName = "_aquaMaiResources";

    private class ServerResourcesEntry : ConditionalMessage
    {
        [CanBeNull] public string url = null;
        [CanBeNull] public string sign = null;
    }

    private class ServerResourcesData
    {
        public ServerResourcesEntry[] entries = [];
    }

    private static ServerResourcesEntry _entry;
    public static AssetBundle bundle;
    private static Download0rder _downloader;

    public static void OnBeforePatch()
    {
        NetPacketHook.OnNetPacketComplete += OnNetPacketComplete;
    }

    private static Variant OnNetPacketComplete(string api, Variant _, Variant response)
    {
        if (bundle != null) return null;
        if (_downloader != null) return null;
        if (_entry != null) return null;
        if (api != "GetGameSettingApi" || response is not ProxyObject obj) return null;
        var serverResourcesJson = obj.Keys.Contains(FieldName) ? obj[FieldName] : null;
        if (serverResourcesJson == null) return null;

        var data = serverResourcesJson.Make<ServerResourcesData>();
        _entry = data.entries.FirstOrDefault(it => it.ShouldShow());
        if (_entry == null) return null;
        if (File.Exists(cacheFile))
        {
            var cached = File.ReadAllBytes(cacheFile);
            if (VerifySignature(cached, _entry.sign)
#if DEBUG
                || File.Exists("LocalAssets/ServerResourcesDebug")
#endif
            )
            {
                LoadFile();
                return null;
            }
#if DEBUG
            MelonLogger.Msg("[ServerResources] Invalid signature for existed file");
#endif
            try
            {
                File.Delete(cacheFile);
            }
            catch
            {
                // ignored
            }
        }

        blinktimer.Start();
        var go = new GameObject("[AquaMai] ServerResourcesDownloader");
        _downloader = go.AddComponent<Download0rder>();

        return null;
    }

    private class Download0rder : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(GetFile());
        }

        private IEnumerator GetFile()
        {
            yield return GetFileImpl();
            Destroy(this);
        }
    }

    private static byte _poweronState = 0;
    private static Stopwatch blinktimer = new Stopwatch();
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerOnMonitor), nameof(PowerOnMonitor.SetMainMessage))]
    public static void PreSetMainMessage(ref string mmessage, ref string smessage)
    {
        if (_poweronState < 4) return;
        if (_downloader == null) return;
        mmessage += "\n\nSERVER RESOURCES";
        if (_poweronState == 255)
        {
            smessage += Util.Utility.isBlinkDisp(blinktimer) ? "\n\nLOADING" : string.Empty;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerOnProcess), "OnUpdate")]
    public static void PrePowerOnStart(ref byte ____state)
    {
        if (____state == 9 && _downloader != null)
        {
            ____state = 255;
        }
        if (____state == 255 && _downloader == null)
        {
            ____state = 9;
        }
        _poweronState = ____state;
    }

    private static IEnumerator GetFileImpl()
    {
#if DEBUG
        yield return new WaitForSeconds(2); // DEBUG
#endif
        using var webRequest = UnityWebRequest.Get(_entry.url);
        yield return webRequest.SendWebRequest();

        if (webRequest.isNetworkError)
        {
            MelonLogger.Warning("[ServerResources] Unable to download file: " + webRequest.error);
            yield break;
        }

        var bytes = webRequest.downloadHandler.data;
        if (!VerifySignature(bytes, _entry.sign))
        {
            MelonLogger.Warning("[ServerResources] Invalid signature for file");
            yield break;
        }

        try
        {
            // 没有文件夹就新建
            if (!Directory.Exists(Path.GetDirectoryName(cacheFile)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
            }
            File.WriteAllBytes(cacheFile, bytes);
            LoadFile();
        }
        catch
        {
            LoadBytes(bytes);
        }
    }

    private static void LoadFile()
    {
        try
        {
            bundle = AssetBundle.LoadFromFile(Path.Combine(Environment.CurrentDirectory, cacheFile));
            LoadFromAssetBundle();
        }
        catch
        {
            // ignored
        }
    }

    private static void LoadBytes(byte[] bytes)
    {
        try
        {
            bundle = AssetBundle.LoadFromMemory(bytes);
            LoadFromAssetBundle();
        }
        catch
        {
            // ignored
        }
    }

    private static void LoadFromAssetBundle()
    {
        if (bundle == null)
        {
            MelonLogger.Error($"[ServerResources] Failed to load asset bundle from {Path.Combine(Environment.CurrentDirectory, cacheFile)}");
            return;
        }
        var compressed = bundle.LoadAsset<TextAsset>("AquaMaiExtension");
        if (compressed == null) return;
        using var ms = new MemoryStream(compressed.bytes);
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);
        using var os = new MemoryStream();
        ds.CopyTo(os);
        var bytes = os.ToArray();
        var asm = AppDomain.CurrentDomain.Load(bytes);
        var ext = asm.GetType("AquaMai.Extension.Entrypoint");
        var main = ext.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);
        main.Invoke(null, []);
    }

    private static bool VerifySignature(byte[] data, string signatureBase64)
    {
        var signature = Convert.FromBase64String(signatureBase64);
        var pubKey = Convert.FromBase64String(publicKey);

        var param = new RSAParameters
        {
            Modulus = pubKey,
            Exponent = [1, 0, 1],
        };
        using var rsa = RSA.Create();
        rsa.ImportParameters(param);
        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}