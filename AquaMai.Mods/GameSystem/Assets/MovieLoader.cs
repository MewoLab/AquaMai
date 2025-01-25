using System.Collections.Generic;
using System.IO;
using AquaMai.Config.Attributes;
using AquaMai.Core.Helpers;
using CriMana;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Monitor.Game;
using UnityEngine;
using UnityEngine.Video;

namespace AquaMai.Mods.GameSystem.Assets;

[ConfigSection(
    en: "Use mp4 files or the png jacket above as PV if no .dat found in the movie folder.",
    zh: "使用封面作为 PV 以及直读 MP4 格式 PV 的选项")]
public class MovieLoader
{
    [ConfigEntry(
        en: """
            Use jacket as movie
            Use together with `LoadLocalImages`.
            """,
        zh: """
            用封面作为背景 PV
            请和 `LoadLocalImages` 一起用
            """)]
    private static bool jacketAsMovie = true;


    [ConfigEntry(
        en: "Load Movie from game source",
        zh: "加载游戏自带的 PV")]
    private static bool loadSourceMovie = true;
    

    [ConfigEntry(
        en: "Load MP4 files from LocalAssets",
        zh: "从 LocalAssets 中加载 MP4 文件作为 PV")]
    private static bool loadMp4Movie = true;

    [ConfigEntry(
        en: "MP4 files directory",
        zh: "加载 MP4 文件的路径")]
    private static readonly string movieAssetsDir = "LocalAssets";

    private static readonly Dictionary<string, string> optionFileMap = [];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DataManager), "LoadMusicBase")]
    public static void LoadMusicPostfix(List<string> ____targetDirs)
    {
        foreach (var aDir in ____targetDirs)
        {
            if (!Directory.Exists(Path.Combine(aDir, "MovieData"))) continue;
            var files = Directory.GetFiles(Path.Combine(aDir, "MovieData"), "*.mp4");
            foreach (var file in files)
            {
                optionFileMap[Path.GetFileName(file)] = file;
            }
        }
    }

    private static VideoPlayer[] _videoPlayers = new VideoPlayer[2];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameCtrl), "Initialize")]
    public static void LoadLocalBgaAwake(GameObject ____movieMaskObj, int ___monitorIndex)
    {
        // 优先级：首先读游戏自带的bga, 然后读本地mp4 bga, 最后读jacket

        var music = Singleton<DataManager>.Instance.GetMusic(GameManager.SelectMusicID[0]);
        if (music is null) return;
        
        // Load source bga
        if (loadSourceMovie) {
            var moviePath = Singleton<OptionDataManager>.Instance.GetMovieDataPath($"{music.movieName.id:000000}") + ".dat";
            if (!moviePath.Contains("dummy")) return;
        }

        // Load localasset mp4 bga
        var mp4Exists = false;
        var mp4Path = "";
        if (loadMp4Movie) {
            var resolvedDir = FileSystem.ResolvePath(movieAssetsDir);
            if (!optionFileMap.TryGetValue($"{music.movieName.id:000000}.mp4", out mp4Path))
            {
                mp4Path = Path.Combine(resolvedDir, $"{music.movieName.id:000000}.mp4");
            }
            mp4Exists = File.Exists(mp4Path);
        }

        // Load jacket
        Texture2D jacket = null;
        if (jacketAsMovie) {
            jacket = LoadLocalImages.GetJacketTexture2D(music.movieName.id);
            if (jacket is null) {
                var filename = $"Jacket/UI_Jacket_{music.movieName.id:000000}.png";
                jacket = AssetManager.Instance().GetJacketTexture2D(filename);
            }
        }

        if (!mp4Exists && jacket is null)
        {
            MelonLogger.Msg("No jacket found for music " + music);
            return;
        }

        if (mp4Exists)
        {
            if (_videoPlayers[___monitorIndex] == null)
            {
# if DEBUG
                MelonLogger.Msg("Init _videoPlayer");
# endif
                _videoPlayers[___monitorIndex] = ____movieMaskObj.AddComponent<VideoPlayer>();
            }
# if DEBUG
            else
            {
                MelonLogger.Msg("_videoPlayer already exists");
            }
# endif
            _videoPlayers[___monitorIndex].url = mp4Path;
            _videoPlayers[___monitorIndex].playOnAwake = false;
            _videoPlayers[___monitorIndex].renderMode = VideoRenderMode.MaterialOverride;
            _videoPlayers[___monitorIndex].audioOutputMode = VideoAudioOutputMode.None;
            // 似乎 MaterialOverride 没法保持视频的长宽比，用 RenderTexture 的话放在 SpriteRenderer 里面会比较麻烦。
            // 所以就不保持了，在塞 pv 的时候自己转吧，反正原本也要根据 first 加 padding
        }

        var movie = ____movieMaskObj.transform.Find("Movie");

        // If I create a new RawImage component, the jacket will be not be displayed
        // I think it will be difficult to make it work with RawImage
        // So I change the material that plays video to default sprite material
        // The original player is actually a sprite renderer and plays video with a custom material
        var sprite = movie.GetComponent<SpriteRenderer>();
        if (mp4Exists)
        {
            sprite.material = new Material(Shader.Find("Sprites/Default"));
            _videoPlayers[___monitorIndex].targetMaterialRenderer = sprite;
        }
        else
        {
            sprite.sprite = Sprite.Create(jacket, new Rect(0, 0, jacket.width, jacket.height), new Vector2(0.5f, 0.5f));
            sprite.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "Play")]
    public static void Play(int frame)
    {
        foreach (var player in _videoPlayers)
        {
            if (player == null) continue;
            player.frame = frame;
            player.Play();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "Pause")]
    public static void Pause(bool pauseFlag)
    {
        foreach (var player in _videoPlayers)
        {
            if (player == null) continue;
            if (pauseFlag)
            {
                player.Pause();
            }
            else
            {
                player.Play();
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "SetSpeed")]
    public static void SetSpeed(float speed)
    {
        foreach (var player in _videoPlayers)
        {
            if (player == null) continue;
            player.playbackSpeed = speed;
        }
    }
}