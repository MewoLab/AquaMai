using System;
using System.Text;
using Net;
using Net.Packet;
using MelonLoader;
using MelonLoader.TinyJSON;
using HarmonyLib;
using AquaMai.Core.Helpers;
using System.IO;

namespace AquaMai.Mods.Utils;

public class NetPacketHook
{
    // Returns true if the packet was modified
    public delegate Variant NetPacketCompleteHook(string api, Variant request, Variant response);

    public static event NetPacketCompleteHook OnNetPacketComplete;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Packet), "ProcImpl")]
    public static void PreProcImpl(Packet __instance)
    {
        try
        {
            if (
                __instance.State == PacketState.Process &&
                Traverse.Create(__instance).Field("Client").GetValue() is NetHttpClient client &&
                client.State == NetHttpClient.StateDone)
            {
                var netQuery = __instance.Query;
                var api = Shim.RemoveApiSuffix(netQuery.Api);
                var responseBytes = client.GetResponse().ToArray();
                var decryptedResponse = Shim.NetHttpClientDecryptsResponse ? responseBytes : Shim.DecryptNetPacketBody(responseBytes);
                var responseJson = JSON.Load(Encoding.UTF8.GetString(decryptedResponse));
                var requestJson = JSON.Load(netQuery.GetRequest());
                var modified = false;
                foreach (var handler in OnNetPacketComplete?.GetInvocationList())
                {
                    try
                    {
                        if (handler.DynamicInvoke(api, requestJson, responseJson) is Variant result)
                        {
                            responseJson = result;
                            modified = true;
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[NetPacketExtension] Error in handler: {e}");
                    }
                }
                if (
                    modified &&
                    Traverse.Create(client).Field("_memoryStream").GetValue() is MemoryStream memoryStream)
                {
                    var modifiedResponse = Encoding.UTF8.GetBytes(responseJson.ToString());
                    memoryStream.SetLength(0);
                    memoryStream.Write(modifiedResponse, 0, modifiedResponse.Length);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    MelonLogger.Msg($"[NetPacketExtension] Modified response for {api}");
                }
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error($"[NetPacketExtension] Failed to process NetPacket: {e}");
        }
    }
}
