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
    public delegate Variant NetPacketResponseHook(string api, Variant json);

    public static event NetPacketResponseHook OnNetPacketResponse;

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
                var response = client.GetResponse().ToArray();
                var decryptedResponse = Shim.NetHttpClientDecryptsResponse ? response : Shim.DecryptNetPacketBody(response);
                var json = JSON.Load(Encoding.UTF8.GetString(decryptedResponse));
                var modified = false;
                foreach (var handler in OnNetPacketResponse?.GetInvocationList())
                {
                    try
                    {
                        if (handler.DynamicInvoke(api, json) is Variant result)
                        {
                            json = result;
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
                    var modifiedResponse = Encoding.UTF8.GetBytes(json.ToString());
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
