using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.MaiStudio;
using Manager.UserDatas;
using MelonLoader;
using Process;
using UnityEngine;
using UnityEngine.Video;
using Monitor;

namespace AquaMai.Mods.Fancy;

[ConfigSection(
    en: "Play custom intro cinematic before track start",
    zh: "播放自定义乐曲开场视频")]
public class CustomIntroCinematic
{
    [ConfigEntry(
        en: "Movie file directory\n" +
            "The folder should contain intro movie files (mp4/acb/awb)\n" +
            "Video file naming: Enter_xxx.mp4 (same video for both screens) OR Enter_xxx_L.mp4 + Enter_xxx_R.mp4 (separate videos for left/right screens)\n" +
            "Audio file naming: Enter_xxx.acb + Enter_xxx.awb (optional, video plays silently without audio files)\n" +
            "xxx = music ID, the folder can contain intro videos for multiple songs\n" +
            "Easy ACB/AWB generation tool: https://github.com/ck2739046/MaiChartManager/tree/main/Audio_Convert\n" +
            "Demo video: https://www.bilibili.com/video/BV18QHjzMEyT",
        zh: "开场视频文件夹的路径\n" +
            "该文件夹应包含开场视频文件 (mp4/acb/awb)\n" +
            "视频文件命名: Enter_xxx.mp4 (左右屏播放同一视频) 或 Enter_xxx_L.mp4 + Enter_xxx_R.mp4 (左右屏播放不同的视频)\n" +
            "音频文件命名: Enter_xxx.acb + Enter_xxx.awb (可选, 无音频文件时静音播放)\n" +
            "xxx = 乐曲ID, 文件夹内可以包含多个乐曲的开场视频\n" +
            "简单一键生成 ACB/AWB 工具: https://github.com/ck2739046/MaiChartManager/tree/main/Audio_Convert\n" +
            "效果演示: https://www.bilibili.com/video/BV18QHjzMEyT")]
    private static readonly string IntroMovieDir = "LocalAssets";

    private static bool _isInitialized = false;

    private static Dictionary<int, (string leftPath, string rightPath, string acbPath, string awbPath)> _targetIDMovieDict = new Dictionary<int, (string leftPath, string rightPath, string acbPath, string awbPath)>();
    private static HashSet<int> _targetMusicIds = new HashSet<int>();



    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (_isInitialized) return true;

        // 解析视频文件夹路径
        string resolvedDir = FileSystem.ResolvePath(IntroMovieDir.Trim());
        if (!Directory.Exists(resolvedDir))
        {
            MelonLogger.Msg($"[CustomIntroCinematic] Movie directory does not exist: {resolvedDir}");
            return false;
        }

        // 读取文件夹内的所有视频和音频文件
        var videoFiles = Directory.GetFiles(resolvedDir, "*.mp4", SearchOption.TopDirectoryOnly);
        var audioFiles = Directory.GetFiles(resolvedDir, "*.acb", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(resolvedDir, "*.awb", SearchOption.TopDirectoryOnly))
            .ToArray();
        
        // 用于存储解析结果的临时字典
        var tempVideoDict = new Dictionary<int, List<(string path, string type)>>();
        var tempAudioDict = new Dictionary<int, List<(string path, string type)>>();

        // 解析视频文件
        foreach (var filePath in videoFiles)
        {
            // 统一小写
            string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
            
            // 检查文件名是否以"enter_"开头
            if (!fileName.StartsWith("enter_"))
                continue;

            // 分割文件名
            string[] parts = fileName.Split('_');

            // 需要2个或3个部分："enter" 乐曲ID (l/r)
            if (parts.Length != 2 && parts.Length != 3)
                continue;

            // 尝试解析乐曲ID
            if (!int.TryParse(parts[1], out int musicId) || musicId <= 0)
                continue;

            // 添加到目标乐曲ID集合
            _targetMusicIds.Add(musicId);

            // 检查文件类型
            string fileType = "single"; // 默认单文件
            if (parts.Length == 3)
            {
                if (parts[2] == "l" || parts[2] == "r")
                {
                    fileType = parts[2];
                }
                else
                {
                    // 如果有第三个部分但不是l或r，跳过
                    continue;
                }
            }

            // 添加到临时字典
            if (!tempVideoDict.ContainsKey(musicId))
            {
                tempVideoDict[musicId] = new List<(string path, string type)>();
            }
            tempVideoDict[musicId].Add((filePath, fileType));
        }

        // 解析音频文件
        foreach (var filePath in audioFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
            
            // 检查文件名是否以"enter_"开头
            if (!fileName.StartsWith("enter_"))
                continue;

            // 分割文件名
            string[] parts = fileName.Split('_');

            // 需要2个部分："enter" 乐曲ID
            if (parts.Length != 2)
                continue;

            // 尝试解析乐曲ID
            if (!int.TryParse(parts[1], out int musicId) || musicId <= 0)
                continue;

            // 添加到目标乐曲ID集合
            _targetMusicIds.Add(musicId);

            // 检查文件类型
            string fileType = Path.GetExtension(filePath).ToLower();
            if (fileType != ".acb" && fileType != ".awb")
                continue;

            // 添加到临时字典
            if (!tempAudioDict.ContainsKey(musicId))
            {
                tempAudioDict[musicId] = new List<(string path, string type)>();
            }
            tempAudioDict[musicId].Add((filePath, fileType));
        }

        // 验证文件配置并构建最终字典
        foreach (var kvp in tempVideoDict)
        {
            int musicId = kvp.Key;
            var videoList = kvp.Value;

            // 检查视频合法性
            int singleCount = videoList.Count(v => v.type == "single");
            int leftCount = videoList.Count(v => v.type == "l");
            int rightCount = videoList.Count(v => v.type == "r");

            string leftPath = null;
            string rightPath = null;

            // 有single文件，并且没有l/r文件
            if (singleCount == 1 && leftCount == 0 && rightCount == 0)
            {
                var singleVideo = videoList.Find(v => v.type == "single");
                leftPath = singleVideo.path;
                rightPath = singleVideo.path;
            }
            // 同时有l和r文件，并且没有single文件
            else if (singleCount == 0 && leftCount == 1 && rightCount == 1)
            {
                var leftVideo = videoList.Find(v => v.type == "l");
                var rightVideo = videoList.Find(v => v.type == "r");
                leftPath = leftVideo.path;
                rightPath = rightVideo.path;
            }
            else
            {
                // 其他情况都不合法
                MelonLogger.Msg($"[CustomIntroCinematic] Invalid video configuration for music {musicId}: must have either single file or both l and r files");
                continue;
            }

            // 检查音频文件配置
            string acbPath = null;
            string awbPath = null;
            if (tempAudioDict.ContainsKey(musicId))
            {
                var audioList = tempAudioDict[musicId];
                int acbCount = audioList.Count(a => a.type == ".acb");
                int awbCount = audioList.Count(a => a.type == ".awb");

                // acb和awb必须同时出现，且每个只能有一个
                if (acbCount == 1 && awbCount == 1)
                {
                    acbPath = audioList.Find(a => a.type == ".acb").path;
                    awbPath = audioList.Find(a => a.type == ".awb").path;
                    //MelonLogger.Msg($"[CustomIntroCinematic] Found acb/awb audio for music {musicId}");
                }
                else
                {
                    MelonLogger.Msg($"[CustomIntroCinematic] Invalid audio configuration for music {musicId}: must have exactly one acb and one awb file");
                }
            }

            // 添加到最终字典
            _targetIDMovieDict[musicId] = (leftPath, rightPath, acbPath, awbPath);
        }

        // 检查是否有目标音乐ID没有对应的视频文件
        foreach (var musicId in _targetMusicIds)
        {
            if (!_targetIDMovieDict.ContainsKey(musicId))
            {
                MelonLogger.Msg($"[CustomIntroCinematic] No valid video found for music {musicId}");
                return false;
            }
        }

        MelonLogger.Msg($"[CustomIntroCinematic] Initialized with {_targetIDMovieDict.Count} songs.");
        _isInitialized = true;
        return true;
    }


    public class SimpleMovieTrackStartProcess : ProcessBase
    {
        private enum SimpleMovieState
        {
            StartWait,      // 等待淡入完成阶段
            VideoPrepare,   // 视频准备阶段  
            VideoPlay,      // 视频播放阶段
            VideoEnd,       // 视频结束阶段
            EndWait,        // 视频结束后的等待阶段（等待1秒再进入Release）
            Release,        // 释放资源阶段
            Released        // 完成阶段
        }

        private SimpleMovieState _state;
        private VideoPlayer[] _videoPlayers;
        private GameObject[] _movieMaskObjs;
        private GameObject[] _monitorObjects;
        private SpriteRenderer[] _movieSprites;
        private float _videoTimer;
        private bool[] _isVideoPrepared;
        private string _videoFilePathLeft;
        private string _videoFilePathRight;
        private float _videoDuration;
        private float _startWaitTimer;
        private float _endWaitTimer;
        private string _acbFilePath;
        private string _awbFilePath;
        private bool _isAudioPrepared;

        public SimpleMovieTrackStartProcess(ProcessDataContainer dataContainer, (string leftPath, string rightPath, string acbPath, string awbPath) videoPaths) 
            : base(dataContainer)
        {
            _videoFilePathLeft = videoPaths.leftPath;
            _videoFilePathRight = videoPaths.rightPath;
            _acbFilePath = videoPaths.acbPath;
            _awbFilePath = videoPaths.awbPath;
            _videoPlayers = new VideoPlayer[2];
            _movieMaskObjs = new GameObject[2];
            _monitorObjects = new GameObject[2];
            _movieSprites = new SpriteRenderer[2];
            _isVideoPrepared = new bool[2];
            _isAudioPrepared = false;
        }

        public override void OnAddProcess()
        {
        }

        public override void OnRelease()
        {
            // 停止音频
            if (_isAudioPrepared)
            {
                SoundManager.StopMusic();
                // 卸载CueSheet以释放音频资源
                Singleton<SoundCtrl>.Instance.UnloadCueSheet(1);
                //MelonLogger.Msg("[CustomIntroCinematic] Unloaded CueSheet 1");
                _isAudioPrepared = false;
            }
            
            // 恢复背景显示
            try
            {
                var containerField = typeof(ProcessBase).GetField("container", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var container = (ProcessDataContainer)containerField.GetValue(this);
                
                for (int i = 0; i < 2; i++)
                {
                    //SetBackGroundDisp
                    container.processManager.SendMessage(new Message(ProcessType.CommonProcess, 50004, i, true));
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[CustomIntroCinematic] OnRelease error when restoring background: {e}");
            }
            
            // 清理所有创建的对象
            for (int i = 0; i < 2; i++)
            {
                if (_videoPlayers[i] != null)
                {
                    _videoPlayers[i].Stop();
                    UnityEngine.Object.Destroy(_videoPlayers[i]);
                }
                if (_movieMaskObjs[i] != null)
                {
                    UnityEngine.Object.Destroy(_movieMaskObjs[i]);
                }
                // 销毁Monitor GameObject
                if (_monitorObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(_monitorObjects[i]);
                    _monitorObjects[i] = null;
                }
                // Ensure sprite renderers are disabled and cleared
                try
                {
                    if (_movieSprites != null && _movieSprites[i] != null)
                    {
                        _movieSprites[i].enabled = false;
                        _movieSprites[i] = null;
                    }
                }
                catch { }

                _videoPlayers[i] = null;
                _movieMaskObjs[i] = null;
            }
        }

        public override void OnStart()
        {
            try
            {
                // 隐藏普通背景
                var containerField = typeof(ProcessBase).GetField("container", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var container = (ProcessDataContainer)containerField.GetValue(this);

                for (int i = 0; i < 2; i++)
                {
                    // SetBackGroundDisp()
                    container.processManager.SendMessage(new Message(ProcessType.CommonProcess, 50004, i, false));
                }
                
                // 不知道怎么回事，副屏角色图片不显示了，要重新发送信息
                SendCharacterInformation(container);
                
                // 准备音频（如果有）
                if (!string.IsNullOrEmpty(_acbFilePath) && !string.IsNullOrEmpty(_awbFilePath))
                {
                    // 使用SoundManager准备音频文件
                    // 需要提供不带扩展名的基本路径
                    string audioBasePath = Path.Combine(Path.GetDirectoryName(_acbFilePath), Path.GetFileNameWithoutExtension(_acbFilePath));
                    _isAudioPrepared = SoundManager.MusicPrepareForFileName(audioBasePath);
                    if (_isAudioPrepared)
                    {
                        //MelonLogger.Msg($"[CustomIntroCinematic] Audio prepared successfully: {audioBasePath}");
                    }
                    else
                    {
                        MelonLogger.Msg($"[CustomIntroCinematic] Failed to prepare audio: {audioBasePath}");
                    }
                }
                
                // 通知淡入完成
                container.processManager.NotificationFadeIn();
                
                // 开始等待1秒
                _state = SimpleMovieState.StartWait;
                _startWaitTimer = 0f;
                _videoTimer = 0f;
                _isVideoPrepared[0] = false;
                _isVideoPrepared[1] = false;

                //MelonLogger.Msg("[CustomIntroCinematic] Set black background and notified fade in completion, waiting 1 second");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[CustomIntroCinematic] OnStart error: {e}");
                FallbackToGameProcess();
            }
        }

        private void SendCharacterInformation(ProcessDataContainer container)
        {
            try
            {
                // 复刻 MusicSelectMonitor.Initialize()
                DataManager instance = Singleton<DataManager>.Instance;
                for (int playerIndex = 0; playerIndex < 2; playerIndex++)
                {
                    var userData = Singleton<UserDataManager>.Instance.GetUserData(playerIndex);
                    if (!userData.IsEntry)
                        continue;
                    int[] charaSlot = userData.Detail.CharaSlot;
                    List<UserChara> charaList = userData.CharaList;
                    for (int i = 0; i < charaSlot.Length; i++)
                    {
                        if (charaSlot[i] != 0)
                        {
                            CharaData charaData = instance.GetChara(charaSlot[i]);
                            UserChara userChara = charaList.Find((UserChara a) => a.ID == charaData.GetID());
                            if (userChara != null)
                            {
                                MapColorData mapColorData = instance.GetMapColorData(charaData.color.id);
                                Color regionColor = new Color32(mapColorData.Color.R, mapColorData.Color.G, mapColorData.Color.B, byte.MaxValue);
                                Texture2D characterTexture2D = container.assetManager.GetCharacterTexture2D(charaData.imageFile);
                                MessageCharactorInfomationData messageCharactorInfomationData = new MessageCharactorInfomationData(i, charaData.genre.id, characterTexture2D, userChara.DisplayLevel, userChara.Awakening, userChara.NextAwakePercent, regionColor, userChara.Reincarnation);
                                container.processManager.SendMessage(new Message(ProcessType.CommonProcess, 20021, playerIndex, messageCharactorInfomationData));
                            }
                        }
                    }
                    // 激活角色信息显示
                    container.processManager.SendMessage(new Message(ProcessType.CommonProcess, 20020, playerIndex, true));
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[CustomIntroCinematic] SendCharacterInformation error: {e}");
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            try
            {
                switch (_state)
                {
                    case SimpleMovieState.StartWait:
                        // 等待1秒确保淡入动画完成
                        if (_startWaitTimer > 1f)
                        {
                            // 开始准备视频
                            StartVideoPreparation();
                            _state = SimpleMovieState.VideoPrepare;
                            //MelonLogger.Msg("[CustomIntroCinematic] Fade in wait completed, starting video preparation");
                        }
                        else
                        {
                            _startWaitTimer += Time.deltaTime;
                        }
                        break;

                    case SimpleMovieState.VideoPrepare:
                        // 等待视频准备就绪，最多等待5秒
                        if (_isVideoPrepared[0] && _isVideoPrepared[1])
                        {
                            // Enable the movie sprites and set them to normal color just before playback to avoid white flashes
                            try
                            {
                                if (_movieSprites[0] != null)
                                {
                                    _movieSprites[0].color = Color.white;
                                    _movieSprites[0].enabled = true;
                                }
                                if (_movieSprites[1] != null)
                                {
                                    _movieSprites[1].color = Color.white;
                                    _movieSprites[1].enabled = true;
                                }
                            }
                            catch (Exception e)
                            {
                                MelonLogger.Msg($"[CustomIntroCinematic] Error enabling movie sprites: {e}");
                            }

                            _state = SimpleMovieState.VideoPlay;
                            _videoPlayers[0].Play();
                            _videoPlayers[1].Play();
                            
                            // 开始播放音频（如果有）
                            if (_isAudioPrepared)
                            {
                                SoundManager.StartMusic();
                                //MelonLogger.Msg("[CustomIntroCinematic] Both videos and audio prepared, starting playback");
                            }
                            else
                            {
                                //MelonLogger.Msg("[CustomIntroCinematic] Both videos prepared, starting playback (no audio)");
                            }
                        }
                        else if (_videoTimer > 5f) // 5秒超时
                        {
                            MelonLogger.Msg("[CustomIntroCinematic] Video preparation timeout");
                            FallbackToGameProcess();
                        }
                        else
                        {
                            _videoTimer += Time.deltaTime;
                        }
                        break;

                    case SimpleMovieState.VideoPlay:

                        // 视频结束了立即黑屏
                        try
                        {   
                            if (!_videoPlayers[0].isPlaying)
                                if (_movieSprites[0] != null) _movieSprites[0].enabled = false;
                            if (!_videoPlayers[1].isPlaying)
                                if (_movieSprites[1] != null) _movieSprites[1].enabled = false;
                        }
                        catch (Exception e)
                        {
                            MelonLogger.Msg($"[CustomIntroCinematic] Error disabling movie sprites on end: {e}");
                        }

                        // 检查视频是否结束（两个视频都结束了才认为全部结束）
                        bool videoEnded = (!_videoPlayers[0].isPlaying && !_videoPlayers[1].isPlaying) || _videoTimer >= _videoDuration;
                        // 如果有音频，还需要检查音频是否结束
                        bool audioEnded = !_isAudioPrepared || SoundManager.IsEndMusic();
                        
                        if (videoEnded && audioEnded)
                        {
                            _state = SimpleMovieState.VideoEnd;
                            //MelonLogger.Msg("[CustomIntroCinematic] Videos and audio finished");
                        }
                        else
                        {
                            _videoTimer += Time.deltaTime;
                        }
                        break;

                    case SimpleMovieState.VideoEnd:
                        // Stop playback and enter EndWait (1s) before Release

                        // 再次禁用，确保没有白屏
                        try
                        {
                            if (_movieSprites[0] != null) _movieSprites[0].enabled = false;
                            if (_movieSprites[1] != null) _movieSprites[1].enabled = false;
                        }
                        catch (Exception e)
                        {
                            MelonLogger.Msg($"[CustomIntroCinematic] Error disabling movie sprites on end: {e}");
                        }

                        // Stop players
                        if (_videoPlayers[0] != null) _videoPlayers[0].Stop();
                        if (_videoPlayers[1] != null) _videoPlayers[1].Stop();

                        // Stop audio if playing
                        if (_isAudioPrepared)
                        {
                            SoundManager.StopMusic();
                            Singleton<SoundCtrl>.Instance.UnloadCueSheet(1);
                            //MelonLogger.Msg("[CustomIntroCinematic] Unloaded CueSheet 1");
                            _isAudioPrepared = false;
                        }

                        // Start end-wait timer
                        _endWaitTimer = 0f;
                        _state = SimpleMovieState.EndWait;
                        //MelonLogger.Msg("[CustomIntroCinematic] Entering EndWait (1s) before release");
                        break;

                    case SimpleMovieState.EndWait:
                        // 等待1秒后进入Release以截断流程
                        if (_endWaitTimer > 1f)
                        {
                            _state = SimpleMovieState.Release;
                            //.Msg("[CustomIntroCinematic] EndWait completed, entering Release");
                        }
                        else
                        {
                            _endWaitTimer += Time.deltaTime;
                        }
                        break;

                    case SimpleMovieState.Release:
                        // 清理资源，进入游戏
                        _state = SimpleMovieState.Released;
                        
                        // 使用反射获取 container 字段
                        var containerField = typeof(ProcessBase).GetField("container", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var container = (ProcessDataContainer)containerField.GetValue(this);
                        
                        container.processManager.AddProcess(
                            new FadeProcess(container, this, new GameProcess(container), FadeProcess.FadeType.Type3), 50);
                        container.processManager.SetVisibleTimers(isVisible: false);
                        break;

                    case SimpleMovieState.Released:
                        // 完成状态，不做任何事
                        break;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[CustomIntroCinematic] OnUpdate error: {e}");
                FallbackToGameProcess();
            }
        }

        private void StartVideoPreparation()
        {
            try
            {
                // 检查视频文件是否存在
                if (!System.IO.File.Exists(_videoFilePathLeft))
                {
                    MelonLogger.Msg($"[CustomIntroCinematic] Left video file not found: {_videoFilePathLeft}");
                    FallbackToGameProcess();
                    return;
                }

                if (!System.IO.File.Exists(_videoFilePathRight))
                {
                    MelonLogger.Msg($"[CustomIntroCinematic] Right video file not found: {_videoFilePathRight}");
                    FallbackToGameProcess();
                    return;
                }

                // 加载MovieTrackStartProcess预制体来获取movieMaskObj
                GameObject prefs = Resources.Load<GameObject>("Process/MovieTrackStart/MovieTrackStartProcess");
                if (prefs == null)
                {
                    MelonLogger.Msg("[CustomIntroCinematic] Failed to load MovieTrackStartProcess prefab");
                    FallbackToGameProcess();
                    return;
                }

                var containerField = typeof(ProcessBase).GetField("container", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var container = (ProcessDataContainer)containerField.GetValue(this);

                // 创建左右两个monitor实例
                var leftMonitor = UnityEngine.Object.Instantiate(prefs, container.LeftMonitor);
                var rightMonitor = UnityEngine.Object.Instantiate(prefs, container.RightMonitor);

                // 保存Monitor GameObject引用以便后续销毁
                _monitorObjects[0] = leftMonitor;
                _monitorObjects[1] = rightMonitor;

                // Ensure instantiated monitors align exactly with their parent (reset local transform)
                try {
                    leftMonitor.transform.localPosition = Vector3.zero;
                    leftMonitor.transform.localRotation = Quaternion.identity;
                    leftMonitor.transform.localScale = Vector3.one;

                    rightMonitor.transform.localPosition = Vector3.zero;
                    rightMonitor.transform.localRotation = Quaternion.identity;
                    rightMonitor.transform.localScale = Vector3.one;
                } catch (Exception e) { MelonLogger.Msg($"[CustomIntroCinematic] Error resetting monitor transforms: {e}"); }

                // 获取MovieTrackStartMonitor组件
                var leftMonitorComp = leftMonitor.GetComponent<Monitor.MovieTrackStart.MovieTrackStartMonitor>();
                var rightMonitorComp = rightMonitor.GetComponent<Monitor.MovieTrackStart.MovieTrackStartMonitor>();

                if (leftMonitorComp == null || rightMonitorComp == null)
                {
                    MelonLogger.Msg("[CustomIntroCinematic] Failed to get MovieTrackStartMonitor components");
                    FallbackToGameProcess();
                    return;
                }

                // 初始化monitors
                leftMonitorComp.Initialize(0, true);
                rightMonitorComp.Initialize(1, true);

                // 获取movieMaskObj
                var leftMovieMaskField = typeof(Monitor.MovieTrackStart.MovieTrackStartMonitor).GetField("_movieMaskObj", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rightMovieMaskField = typeof(Monitor.MovieTrackStart.MovieTrackStartMonitor).GetField("_movieMaskObj", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var leftMovieMaskObj = (GameObject)leftMovieMaskField.GetValue(leftMonitorComp);
                var rightMovieMaskObj = (GameObject)rightMovieMaskField.GetValue(rightMonitorComp);

                if (leftMovieMaskObj == null || rightMovieMaskObj == null)
                {
                    MelonLogger.Msg("[CustomIntroCinematic] Failed to get movieMaskObj");
                    FallbackToGameProcess();
                    return;
                }

                _movieMaskObjs[0] = leftMovieMaskObj;
                _movieMaskObjs[1] = rightMovieMaskObj;

                // 创建视频播放器
                _videoPlayers[0] = leftMovieMaskObj.AddComponent<VideoPlayer>();
                _videoPlayers[1] = rightMovieMaskObj.AddComponent<VideoPlayer>();

                // 配置左VideoPlayer
                _videoPlayers[0].url = _videoFilePathLeft;
                _videoPlayers[0].playOnAwake = false;
                _videoPlayers[0].isLooping = false;
                _videoPlayers[0].renderMode = VideoRenderMode.MaterialOverride;
                _videoPlayers[0].audioOutputMode = VideoAudioOutputMode.None;

                // 配置右VideoPlayer
                _videoPlayers[1].url = _videoFilePathRight;
                _videoPlayers[1].playOnAwake = false;
                _videoPlayers[1].isLooping = false;
                _videoPlayers[1].renderMode = VideoRenderMode.MaterialOverride;
                _videoPlayers[1].audioOutputMode = VideoAudioOutputMode.None;

                // 获取movie sprite并设置材质
                var leftMovieField = typeof(Monitor.MovieTrackStart.MovieTrackStartMonitor).GetField("_movieSprite", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rightMovieField = typeof(Monitor.MovieTrackStart.MovieTrackStartMonitor).GetField("_movieSprite", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var leftMovieSprite = (SpriteRenderer)leftMovieField.GetValue(leftMonitorComp);
                var rightMovieSprite = (SpriteRenderer)rightMovieField.GetValue(rightMonitorComp);

                if (leftMovieSprite != null && rightMovieSprite != null)
                {
                    leftMovieSprite.material = new Material(Shader.Find("Sprites/Default"));
                    rightMovieSprite.material = new Material(Shader.Find("Sprites/Default"));
                    
                    _videoPlayers[0].targetMaterialRenderer = leftMovieSprite;
                    _videoPlayers[1].targetMaterialRenderer = rightMovieSprite;

                }

                // Store and initialize movie sprite renderers. Keep them disabled (black) until playback
                _movieSprites[0] = leftMovieSprite;
                _movieSprites[1] = rightMovieSprite;
                try
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (_movieSprites[i] != null)
                        {
                            // Ensure the sprite draws black when enabled initially, and keep it disabled to avoid flashes
                            _movieSprites[i].color = Color.black;
                            _movieSprites[i].enabled = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Msg($"[CustomIntroCinematic] Error initializing movie sprites: {e}");
                }

                // 设置准备完成回调
                int preparedCount = 0;
                Action onVideoPrepared = () => {
                    preparedCount++;
                    if (preparedCount == 2)
                    {
                        _isVideoPrepared[0] = true;
                        _isVideoPrepared[1] = true;
                        _videoDuration = Mathf.Max((float)_videoPlayers[0].length, (float)_videoPlayers[1].length); // 取较长的时长

                        // 设置视频尺寸
                        var leftHeight = _videoPlayers[0].height;
                        var leftWidth = _videoPlayers[0].width;
                        var rightHeight = _videoPlayers[1].height;
                        var rightWidth = _videoPlayers[1].width;

                        // 按照比例缩放到合适尺寸
                        uint leftDisplayHeight, leftDisplayWidth, rightDisplayHeight, rightDisplayWidth;

                        if (leftHeight > leftWidth)
                        {
                            leftDisplayHeight = 1080;
                            leftDisplayWidth = (uint)(1080.0 * leftWidth / leftHeight);
                        }
                        else
                        {
                            leftDisplayHeight = (uint)(1080.0 * leftHeight / leftWidth);
                            leftDisplayWidth = 1080;
                        }

                        if (rightHeight > rightWidth)
                        {
                            rightDisplayHeight = 1080;
                            rightDisplayWidth = (uint)(1080.0 * rightWidth / rightHeight);
                        }
                        else
                        {
                            rightDisplayHeight = (uint)(1080.0 * rightHeight / rightWidth);
                            rightDisplayWidth = 1080;
                        }

                        // 调用SetMovieSize方法设置视频尺寸
                        var setMovieSizeMethod = typeof(Monitor.MovieTrackStart.MovieTrackStartMonitor).GetMethod("SetMovieSize", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                        if (setMovieSizeMethod != null)
                        {
                            setMovieSizeMethod.Invoke(leftMonitorComp, new object[] { leftDisplayHeight, leftDisplayWidth });
                            setMovieSizeMethod.Invoke(rightMonitorComp, new object[] { rightDisplayHeight, rightDisplayWidth });
                            //MelonLogger.Msg($"[CustomIntroCinematic] Set video sizes: left={leftDisplayWidth}x{leftDisplayHeight}, right={rightDisplayWidth}x{rightDisplayHeight}");
                        }

                        //MelonLogger.Msg($"[CustomIntroCinematic] Both videos prepared, duration: {_videoDuration}s");
                    }
                };

                _videoPlayers[0].prepareCompleted += (source) => {
                    onVideoPrepared();
                };

                _videoPlayers[1].prepareCompleted += (source) => {
                    onVideoPrepared();
                };

                // 设置错误回调
                _videoPlayers[0].errorReceived += (source, message) => {
                    MelonLogger.Msg($"[CustomIntroCinematic] Left video player error: {message}");
                    FallbackToGameProcess();
                };

                _videoPlayers[1].errorReceived += (source, message) => {
                    MelonLogger.Msg($"[CustomIntroCinematic] Right video player error: {message}");
                    FallbackToGameProcess();
                };

                // 开始准备视频
                _videoPlayers[0].Prepare();
                _videoPlayers[1].Prepare();
                _videoTimer = 0f;

                //MelonLogger.Msg($"[CustomIntroCinematic] Started preparing videos: left={_videoFilePathLeft}, right={_videoFilePathRight}");
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[CustomIntroCinematic] StartVideoPreparation error: {e}");
                FallbackToGameProcess();
            }
        }

        protected override void UpdateInput(int monitorId)
        {
        }

        public override void OnLateUpdate()
        {
        }

        private void FallbackToGameProcess()
        {
            try
            {
                MelonLogger.Msg("[CustomIntroCinematic] Falling back to GameProcess");
                // 停止音频
                if (_isAudioPrepared)
                {
                    SoundManager.StopMusic();
                    // 卸载CueSheet以释放音频资源
                    Singleton<SoundCtrl>.Instance.UnloadCueSheet(1);
                    //MelonLogger.Msg("[CustomIntroCinematic] Unloaded CueSheet 1 in fallback");
                    _isAudioPrepared = false;
                }
                
                // 恢复背景显示
                var containerField = typeof(ProcessBase).GetField("container", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var container = (ProcessDataContainer)containerField.GetValue(this);
                
                for (int i = 0; i < 2; i++)
                {
                    // SetBackGroundDisp
                    container.processManager.SendMessage(new Message(ProcessType.CommonProcess, 50004, i, true));
                }

                // 直接进入乐曲
                container.processManager.AddProcess(
                    new FadeProcess(container, this, new GameProcess(container), FadeProcess.FadeType.Type3), 50);
                container.processManager.SetVisibleTimers(isVisible: false);
                container.processManager.ReleaseProcess(this);
            }
            catch (Exception e)
            {
                MelonLogger.Msg($"[CustomIntroCinematic] FallbackToGameProcess error: {e}");
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicSelectProcess), "GameStart")]
    public static bool GameStartPrefix(MusicSelectProcess __instance)
    {
        try
        {
            if (!_isInitialized) return true;
            if (!GameManager.IsNormalMode) return true; // 仅在 Normal 模式下生效

            // 检查当前选择的歌曲是否为目标歌曲
            var musicId = GameManager.SelectMusicID[0];
            if (_targetMusicIds.Contains(musicId) && _targetIDMovieDict.TryGetValue(musicId, out var videoPath))
            {
                MelonLogger.Msg($"[CustomIntroCinematic] Play enter movie for music {musicId}");
                
                // 使用反射获取 MusicSelectProcess 的 container 字段
                var containerField = typeof(ProcessBase).GetField("container", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var container = (ProcessDataContainer)containerField.GetValue(__instance);
                
                // 使用自定义的 SimpleMovieTrackStartProcess 替换 TrackStartProcess
                container.processManager.AddProcess(
                    new FadeProcess(container, __instance, 
                        new SimpleMovieTrackStartProcess(container, videoPath), 
                        FadeProcess.FadeType.Type3), 50);
                
                SoundManager.PreviewEnd();
                SoundManager.StopBGM(2);
                
                return false; // 阻止原方法执行
            }
        }
        catch (Exception e)
        {
            MelonLogger.Msg($"[CustomIntroCinematic] GameStartPrefix error: {e}");
        }
        
        return true; // 正常执行原方法
    }


}
