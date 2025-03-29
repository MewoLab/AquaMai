using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using AquaMai.Config.Attributes;
using DB;
using HarmonyLib;
using Manager;
using PartyLink;
using Process;
using Manager.Party.Party;
using AquaMai.Core.Attributes;
using MAI2.Util;
using Mai2.Mai2Cue;
using static Process.MusicSelectProcess;
using Monitor;
using TMPro;
using AquaMai.Mods.WorldsLink;
using MelonLoader.TinyJSON;
using UnityEngine;

namespace AquaMai.Mods.WorldsLink;

[ConfigSection(
    en: "Enable WorldsLink Multiplayer",
    zh: "启用 WorldsLink 多人游戏",
    defaultOn: false)]
public static class Futari
{
    private static readonly Dictionary<NFSocket, FutariSocket> redirect = new();
    private static FutariClient client;
    private static bool isInit = false;

    private static MethodBase packetWriteUInt;
    private static System.Type StartUpStateType;

    [ConfigEntry(hideWhenDefault: true)]
    public static bool Debug = false;

    #region Init
    private static readonly MethodInfo SetRecruitData = typeof(MusicSelectProcess).GetProperty("RecruitData")!.SetMethod;
    public static void OnBeforePatch()
    {
        Log.Info("Starting WorldsLink patch...");

        packetWriteUInt = typeof(Packet).GetMethod("write_uint", BindingFlags.NonPublic | BindingFlags.Static, null,
            [typeof(PacketType), typeof(int), typeof(uint)], null);
        if (packetWriteUInt == null) Log.Error("write_uint not found");

        StartUpStateType = typeof(StartupProcess).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType;
        if (StartUpStateType == null) Log.Error("StartUpStateType not found");

        // TODO: Make IP configurable
        client = new FutariClient("A1234567890", "futari.aquadx.net", 20101);
    }

    // Entrypoint
    // private void CheckAuth_Proc()
    [HarmonyPrefix]
    [HarmonyPatch(typeof(OperationManager), "CheckAuth_Proc")]
    private static bool CheckAuth_Proc()
    {
        if (isInit) return PrefixRet.RUN_ORIGINAL;
        Log.Info("CheckAuth_Proc");

        var keychip = AMDaemon.System.KeychipId.ShortValue;
        Log.Info($"Keychip ID: {keychip}");
        if (string.IsNullOrEmpty(keychip)) Log.Error("Keychip ID is empty. WorldsLink will not work.");
        client.keychip = keychip;
        client.ConnectAsync();

        isInit = true;
        return PrefixRet.RUN_ORIGINAL;
    }

    #endregion

    #region Misc
    //Force Enable LanAvailable
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AMDaemon.Network), "IsLanAvailable", MethodType.Getter)]
    private static bool PreIsLanAvailable(ref bool __result)
    {
        __result = true;
        return false;
    }
    //Online Display
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommonMonitor), "ViewUpdate")]
    private static void CommonMonitorViewUpdate(CommonMonitor __instance,TextMeshProUGUI ____buildVersionText, GameObject ____developmentBuildText)
    {
        ____buildVersionText.transform.position = ____developmentBuildText.transform.position;
        ____buildVersionText.gameObject.SetActive(true);
        switch (client.StatusCode)
        {
            case -1:
                ____buildVersionText.text = $"WorldLink Offline";
                ____buildVersionText.color = Color.red;
                break;
            case 0:
                ____buildVersionText.text = $"WorldLink Disconnect";
                ____buildVersionText.color = Color.gray;
                break;
            case 1:
                ____buildVersionText.text = $"WorldLink Connecting";
                ____buildVersionText.color = Color.yellow;
                break;
            case 2:
                ____buildVersionText.color = Color.cyan;
                ____buildVersionText.text = PartyMan == null ? $"WorldLink Waiting" : $"WorldLink Recruiting: {PartyMan.GetRecruitList().Count}";
                break;
        }
    }

    // Block irrelevant packets
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SocketBase), "sendClass", typeof(ICommandParam))]
    private static bool sendClass(SocketBase __instance, ICommandParam info)
    {
        switch (info)
        {
            // Block AdvocateDelivery, SettingHostAddress
            case AdvocateDelivery or Setting.SettingHostAddress:
                return PrefixRet.BLOCK_ORIGINAL;
           
            // If it's a Start/FinishRecruit message, send it using http instead
            case StartRecruit or FinishRecruit:
                var inf = info is StartRecruit o ? o.RecruitInfo : ((FinishRecruit) info).RecruitInfo;
                var start = info is StartRecruit ? "start" : "finish";
                var msg = JsonUtility.ToJson(new RecruitRecord {
                    Keychip = client.keychip,
                    RecruitInfo = inf
                }, false);
                $"{FutariClient.LOBBY_BASE}/{start}".PostAsync(msg);
                Log.Info($"Sent {start} recruit message: {msg}");
                return PrefixRet.BLOCK_ORIGINAL;
            
            // Log the actual type of info and the actual type of this class
            default:
                Log.Debug($"SendClass: {Log.BRIGHT_RED}{info.GetType().Name}{Log.RESET} from {__instance.GetType().Name}");
                return PrefixRet.RUN_ORIGINAL;
        }
    }

    // Patch for error logging
    // SocketBase:: protected void error(string message, int no)
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SocketBase), "error", typeof(string), typeof(int))]
    private static bool error(string message, int no)
    {
        Log.Error($"Error: {message} ({no})");
        return PrefixRet.RUN_ORIGINAL;
    }

    // Force isSameVersion to return true
    // Packet:: public bool isSameVersion()
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Packet), "isSameVersion")]
    private static void isSameVersion(ref bool __result)
    {
        Log.Debug($"isSameVersion (original): {__result}, forcing true");
        __result = true;
    }

    // Patch my IP address to a stub
    // public static IPAddress MyIpAddress(int mockID)
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PartyLink.Util), "MyIpAddress", typeof(int))]
    private static bool MyIpAddress(int mockID, ref IPAddress __result)
    {
        __result = FutariExt.MyStubIP().ToIP();
        return PrefixRet.BLOCK_ORIGINAL;
    }

    #endregion
    
    #region Recruit Information
    
    private static readonly MethodInfo RStartRecruit = typeof(Client)
        .GetMethod("RecvStartRecruit", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo RFinishRecruit = typeof(Client)
        .GetMethod("RecvFinishRecruit", BindingFlags.NonPublic | BindingFlags.Instance);
    private static Dictionary<string, RecruitInfo> lastRecruits = [];

    private static string Identity(this RecruitInfo x) => $"{x.IpAddress} : {x.MusicID}";
    
    // Client Constructor
    // Client:: public Client(string name, PartyLink.Party.InitParam initParam)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Client), MethodType.Constructor, typeof(string), typeof(PartyLink.Party.InitParam))]
    private static void Client(Client __instance, string name, PartyLink.Party.InitParam initParam)
    {
        Log.Debug($"new Client({name}, {initParam})");
        10000.Interval(() => 
            lastRecruits = $"{FutariClient.LOBBY_BASE}/list"
                .Post("").Trim().Split('\n')
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(JsonUtility.FromJson<RecruitRecord>).ToList()
                .Also(lst => lst
                    .Select(x => x.RecruitInfo.Identity())
                    .Do(ids => lastRecruits.Keys
                        .Where(key => !ids.Contains(key))
                        .Each(key => RFinishRecruit.Invoke(__instance, [new Packet(lastRecruits[key].IpAddress)
                            .Also(p => p.encode(new FinishRecruit(lastRecruits[key])))
                        ]))
                    )
                )
                .Each(x => RStartRecruit.Invoke(__instance, [new Packet(x.RecruitInfo.IpAddress)
                    .Also(p => p.encode(new StartRecruit(x.RecruitInfo)))
                ]))
                .Select(x => x.RecruitInfo)
                .ToDictionary(x => x.Identity())
        );
    }
    
    // Block start recruit if the song is not available
    // Client:: private void RecvStartRecruit(Packet packet)
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Client), "RecvStartRecruit", typeof(Packet))]
    private static bool RecvStartRecruit(Packet packet)
    {
        var inf = packet.getParam<StartRecruit>().RecruitInfo;
        Log.Info($"RecvStartRecruit: {JsonUtility.ToJson(inf)}");
        if (Singleton<DataManager>.Instance.GetMusic(inf.MusicID) == null)
        {
            Log.Error($"Recruit received but music {inf.MusicID} is not available.");
            Log.Error($"If you want to play with {string.Join(" and ", inf.MechaInfo.UserNames)},");
            Log.Error("make sure you have the same game version and option packs installed.");
            return PrefixRet.BLOCK_ORIGINAL;
        }
        return PrefixRet.RUN_ORIGINAL;
    }
    
    #endregion

    private static IManager PartyMan => Manager.Party.Party.Party.Get();

    //Skip StartupNetworkChecker
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartupProcess), nameof(StartupProcess.OnUpdate))]
    private static void SkipStartupNetworkCheck(ref byte ____state, string[] ____statusMsg, string[] ____statusSubMsg)
    {
        // Status code
        ____statusMsg[7] = "WORLD LINK";
        ____statusSubMsg[7] = client.StatusCode switch
        {
            -1 => "BAD",
            0 => "Not Connect",
            1 => "Connecting",
            2 => "GOOD",
            _ => "Waiting..."
        };

        // Delay
        ____statusMsg[8] = "PING";
        ____statusSubMsg[8] = client._delayAvg == 0 ? "N/A" : $"{client._delayAvg} ms";
        ____statusMsg[9] = "CAT :3";
        ____statusSubMsg[9] = client._delayIndex % 2 == 0 ? "" : "MEOW";

        // If it is in the wait link delivery state, change to ready immediately
        if (____state != 0x04) return;
        ____state = 0x08;

        // Start the services that would have been started by the StartupNetworkChecker
        DeliveryChecker.get().start(true);
        Setting.get().setData(new Setting.Data().Also(x => x.set(false, 4)));
        Setting.get().setRetryEnable(true);
        Advertise.get().initialize(MachineGroupID.ON);
        PartyMan.Start(MachineGroupID.ON);
        Log.Info("Skip Startup Network Check");
    }

    #region NFSocket
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NFSocket), MethodType.Constructor, typeof(AddressFamily), typeof(SocketType), typeof(ProtocolType), typeof(int))]
    private static void NFCreate(NFSocket __instance, AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, int mockID)
    {
        Log.Debug($"new NFSocket({addressFamily}, {socketType}, {protocolType}, {mockID})");
        if (mockID == 3939) return;  // Created in redirected NFAccept as a stub
        var futari = new FutariSocket(addressFamily, socketType, protocolType, mockID);
        redirect.Add(__instance, futari);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NFSocket), MethodType.Constructor, typeof(Socket))]
    private static void NFCreate2(NFSocket __instance, Socket nfSocket)
    {
        Log.Error("new NFSocket(Socket) -- We shouldn't get here.");
        throw new NotImplementedException();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Poll")]
    private static bool NFPoll(NFSocket socket, SelectMode mode, ref bool __result)
    {
        __result = FutariSocket.Poll(redirect[socket], mode);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Send")]
    private static bool NFSend(NFSocket __instance, byte[] buffer, int offset, int size, SocketFlags socketFlags, ref int __result)
    {
        __result = redirect[__instance].Send(buffer, offset, size, socketFlags);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "SendTo")]
    private static bool NFSendTo(NFSocket __instance, byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP, ref int __result)
    {
        __result = redirect[__instance].SendTo(buffer, offset, size, socketFlags, remoteEP);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Receive")]
    private static bool NFReceive(NFSocket __instance, byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode, ref int __result)
    {
        __result = redirect[__instance].Receive(buffer, offset, size, socketFlags, out errorCode);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "ReceiveFrom")]
    private static bool NFReceiveFrom(NFSocket __instance, byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP, ref int __result)
    {
        __result = redirect[__instance].ReceiveFrom(buffer, socketFlags, ref remoteEP);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Bind")]
    private static bool NFBind(NFSocket __instance, EndPoint localEndP)
    {
        Log.Debug("NFBind");
        redirect[__instance].Bind(localEndP);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Listen")]
    private static bool NFListen(NFSocket __instance, int backlog)
    {
        Log.Debug("NFListen");
        redirect[__instance].Listen(backlog);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Accept")]
    private static bool NFAccept(NFSocket __instance, ref NFSocket __result)
    {
        Log.Debug("NFAccept");
        var futariSocket = redirect[__instance].Accept();
        var mockSocket = new NFSocket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp, 3939);
        redirect[mockSocket] = futariSocket;
        __result = mockSocket;
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "ConnectAsync")]
    private static bool NFConnectAsync(NFSocket __instance, SocketAsyncEventArgs e, int mockID, ref bool __result)
    {
        Log.Debug("NFConnectAsync");
        __result = redirect[__instance].ConnectAsync(e, mockID);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "SetSocketOption")]
    private static bool NFSetSocketOption(NFSocket __instance, SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
    {
        redirect[__instance].SetSocketOption(optionLevel, optionName, optionValue);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Close")]
    private static bool NFClose(NFSocket __instance)
    {
        Log.Debug("NFClose");
        redirect[__instance].Close();
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "Shutdown")]
    private static bool NFShutdown(NFSocket __instance, SocketShutdown how)
    {
        Log.Debug("NFShutdown");
        redirect[__instance].Shutdown(how);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "RemoteEndPoint", MethodType.Getter)]
    private static bool NFGetRemoteEndPoint(NFSocket __instance, ref EndPoint __result)
    {
        Log.Debug("NFGetRemoteEndPoint");
        __result = redirect[__instance].RemoteEndPoint;
        return PrefixRet.BLOCK_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NFSocket), "LocalEndPoint", MethodType.Getter)]
    private static bool NFGetLocalEndPoint(NFSocket __instance, ref EndPoint __result)
    {
        Log.Debug("NFGetLocalEndPoint");
        __result = redirect[__instance].LocalEndPoint;
        return PrefixRet.BLOCK_ORIGINAL;
    }
    #endregion

    #region Packet codec

    // Disable encryption
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Packet), "encrypt")]
    private static bool PacketEncrypt(Packet __instance, PacketType ____encrypt, PacketType ____plane)
    {
        ____encrypt.ClearAndResize(____plane.Count);
        Array.Copy(____plane.GetBuffer(), 0, ____encrypt.GetBuffer(), 0, ____plane.Count);
        ____encrypt.ChangeCount(____plane.Count);
        packetWriteUInt.Invoke(null, [____plane, 0, (uint)____plane.Count]);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    // Disable decryption
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Packet), "decrypt")]
    private static bool PacketDecrypt(Packet __instance, PacketType ____encrypt, PacketType ____plane)
    {
        ____plane.ClearAndResize(____encrypt.Count);
        Array.Copy(____encrypt.GetBuffer(), 0, ____plane.GetBuffer(), 0, ____encrypt.Count);
        ____plane.ChangeCount(____encrypt.Count);
        packetWriteUInt.Invoke(null, [____plane, 0, (uint)____plane.Count]);
        return PrefixRet.BLOCK_ORIGINAL;
    }

    #endregion
    
    #region Recruit UI

    private static int musicIdSum;
    private static bool sideMessageFlag;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "OnStart")]
    private static bool MusicSelectProcessOnStart(MusicSelectProcess __instance)
    {
        // 每次重新进入选区菜单之后重新初始化变量
        musicIdSum = 0;
        sideMessageFlag = false;
        return PrefixRet.RUN_ORIGINAL;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "PartyExec")]
    private static bool PartyExec(MusicSelectProcess __instance)
    {
        // 检查联机房间是否有更新，如果更新的话设置 IsConnectingMusic=false 然后刷新列表
        var checkDiff = PartyMan.GetRecruitListWithoutMe().Sum(item => item.MusicID);
        if (musicIdSum != checkDiff)
        {
            musicIdSum = checkDiff;
            __instance.IsConnectingMusic = false;
        }

        if (__instance.IsConnectingMusic && __instance.RecruitData != null && __instance.IsConnectionFolder())
        {
            // 设置房间信息显示
            var info = __instance.RecruitData.MechaInfo;
            var players = "WorldLink Room! Players: " + 
                          string.Join(" and ", info.UserNames.Where((_, i) => info.FumenDifs[i] != -1));
            
            __instance.MonitorArray.Where((_, i) => __instance.IsEntry(i))
                .Each(x => x.SetSideMessage(players));
            
            sideMessageFlag = true;
        }
        else if(!__instance.IsConnectionFolder() && sideMessageFlag)
        {
            __instance.MonitorArray.Where((_, i) => __instance.IsEntry(i))
                .Each(x => x.SetSideMessage(CommonMessageID.Scroll_Music_Select.GetName()));
            
            sideMessageFlag = false;
        }
        return PrefixRet.RUN_ORIGINAL;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicSelectProcess), "RecruitData", MethodType.Getter)]
    private static void RecruitDataOverride(MusicSelectProcess __instance, ref RecruitInfo __result)
    {
        // 开歌时设置当前选择的联机数据
        if (!__instance.IsConnectionFolder() || __result == null) return;
        
        var list = PartyMan.GetRecruitListWithoutMe();
        if (list == null) return;
        if (__instance.CurrentMusicSelect >= 0 && __instance.CurrentMusicSelect < list.Count)
        {
            __result = list[__instance.CurrentMusicSelect];
        }
    }

    private static readonly MethodInfo SetConnData = typeof(MusicSelectProcess).GetMethod("SetConnectData")!;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "IsConnectStart")]
    private static bool RecruitDataOverride(MusicSelectProcess __instance,
        List<CombineMusicSelectData> ____connectCombineMusicDataList,
        SubSequence[] ____currentPlayerSubSequence,
        ref bool __result)
    {
        __result = false;
        
        // 修正 SetConnectData 触发条件，阻止原有 IP 判断重新设置
        var recruits = PartyMan.GetRecruitListWithoutMe();
        if (!__instance.IsConnectingMusic && recruits.Count > 0)
        {
            // var recruit = recruits.Count > 0 ? recruits[0] : null;
            var recruit = recruits[0];
            Log.Debug($"MusicSelectProcess::IsConnectStart recruit data has been set to {recruit}");
            SetRecruitData.Invoke(__instance, [recruit]);
            SetConnectData(__instance, ____connectCombineMusicDataList, ____currentPlayerSubSequence);
            __result = true;
        }
        return PrefixRet.BLOCK_ORIGINAL;
    }
    
    private static readonly MethodInfo SetConnectCategoryEnable = typeof(MusicSelectProcess).GetProperty("IsConnectCategoryEnable")!.SetMethod;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "SetConnectData")]
    private static bool SetConnectData(MusicSelectProcess __instance,
        List<CombineMusicSelectData> ____connectCombineMusicDataList,
        SubSequence[] ____currentPlayerSubSequence)
    {
        ____connectCombineMusicDataList.Clear();
        SetConnectCategoryEnable.Invoke(__instance, [false]);

        // 遍历所有房间并且显示
        foreach (var item in PartyMan.GetRecruitListWithoutMe())
        {
            var musicID = item.MusicID;
            var combineMusicSelectData = new CombineMusicSelectData();
            var music = Singleton<DataManager>.Instance.GetMusic(musicID);
            var notesList = Singleton<NotesListManager>.Instance.GetNotesList()[musicID].NotesList;

            switch (musicID)
            {
                case < 10000:
                    combineMusicSelectData.existStandardScore = true;
                    break;
                case > 10000 and < 20000:
                    combineMusicSelectData.existDeluxeScore = true;
                    break;
            }
            
            for (var i = 0; i < 2; i++)
            {
                combineMusicSelectData.musicSelectData.Add(new MusicSelectData(music, notesList, 0));
            }
            ____connectCombineMusicDataList.Add(combineMusicSelectData);
            try
            {
                var thumbnailName = music.thumbnailName;
                for (var j = 0; j < __instance.MonitorArray.Length; j++)
                {
                    if (!__instance.IsEntry(j)) continue;
                    
                    __instance.MonitorArray[j].SetRecruitInfo(thumbnailName);
                    SoundManager.PlaySE(Cue.SE_INFO_NORMAL, j);
                }
            }
            catch { /* 防止有可能的空 */ }
            
            __instance.IsConnectingMusic = true;
        }
        
        // No data available, add a dummy entry
        if (PartyMan.GetRecruitListWithoutMe().Count == 0)
        {
            ____connectCombineMusicDataList.Add(new CombineMusicSelectData
            {
                musicSelectData = [null, null],
                isWaitConnectScore = true
            });
            __instance.IsConnectingMusic = false;
        }
        
        if (__instance.MonitorArray == null) return PrefixRet.BLOCK_ORIGINAL;
        
        for (var l = 0; l < __instance.MonitorArray.Length; l++)
        {
            if (____currentPlayerSubSequence[l] != SubSequence.Music) continue;
            
            __instance.MonitorArray[l].SetDeployList(false);
            if (!__instance.IsConnectionFolder(0)) continue;
            
            __instance.ChangeBGM();
            if (!__instance.IsEntry(l)) continue;
            
            __instance.MonitorArray[l].SetVisibleButton(__instance.IsConnectingMusic, InputManager.ButtonSetting.Button04);
        }
        return PrefixRet.BLOCK_ORIGINAL;
    }
    #endregion
    
    #region Debug

    [EnableIf(typeof(Futari), nameof(Debug))]
    public class FutariDebug
    {
        // Log ListenSocket creation
        // ListenSocket:: public ListenSocket(string name, int mockID)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ListenSocket), MethodType.Constructor, typeof(string), typeof(int))]
        private static void ListenSocket(ListenSocket __instance, string name, int mockID)
        {
            Log.Debug($"new ListenSocket({name}, {mockID})");
        }

        // Log ListenSocket open
        // ListenSocket:: public bool open(ushort portNumber)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ListenSocket), "open", typeof(ushort))]
        private static bool open(ListenSocket __instance, ushort portNumber)
        {
            Log.Debug($"ListenSocket.open({portNumber}) - {__instance}");
            return PrefixRet.RUN_ORIGINAL;
        }

        // Log packet type
        // Analyzer:: private void procPacketData(Packet packet)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Analyzer), "procPacketData", typeof(Packet))]
        private static bool procPacketData(Packet packet, Dictionary<Command, object> ____commandMap)
        {
            var keys = string.Join(", ", ____commandMap.Keys);
            Log.Debug($"procPacketData: {Log.BRIGHT_RED}{packet.getCommand()}{Log.RESET} in {keys}");
            return PrefixRet.RUN_ORIGINAL;
        }

        // Log host creation
        // Host:: public Host(string name)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Host), MethodType.Constructor, typeof(string))]
        private static void Host(Host __instance, string name)
        {
            Log.Debug($"new Host({name})");
        }

        // Log host state change
        // Host:: private void SetCurrentStateID(PartyPartyHostStateID nextState)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Host), "SetCurrentStateID", typeof(PartyPartyHostStateID))]
        private static bool SetCurrentStateID(PartyPartyHostStateID nextState)
        {
            Log.Debug($"Host::SetCurrentStateID: {nextState}");
            return PrefixRet.RUN_ORIGINAL;
        }

        // Log Member creation
        // Member:: public Member(string name, Host host, NFSocket socket)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Member), MethodType.Constructor, typeof(string), typeof(Host), typeof(NFSocket))]
        private static void Member(Member __instance, string name, Host host, NFSocket socket)
        {
            Log.Debug($"new Member({name}, {host}, {socket})");
        }

        // Log Member state change
        // Member:: public void SetCurrentStateID(PartyPartyClientStateID state)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Member), "SetCurrentStateID", typeof(PartyPartyClientStateID))]
        private static bool SetCurrentStateID(PartyPartyClientStateID state)
        {
            Log.Debug($"Member::SetCurrentStateID: {state}");
            return PrefixRet.RUN_ORIGINAL;
        }

        // Log Member RecvRequestJoin
        // Member:: private void RecvRequestJoin(Packet packet)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Member), "RecvRequestJoin", typeof(Packet))]
        private static bool RecvRequestJoin(Packet packet)
        {
            Log.Debug($"Member::RecvRequestJoin: {packet.getParam<RequestJoin>()}");
            return PrefixRet.RUN_ORIGINAL;
        }

        // Log Member RecvClientState
        // Member:: private void RecvClientState(Packet packet)
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Member), "RecvClientState", typeof(Packet))]
        private static bool RecvClientState(Packet packet)
        {
            Log.Debug($"Member::RecvClientState: {packet.getParam<ClientState>()}");
            return PrefixRet.RUN_ORIGINAL;
        }
    }

    #endregion
}