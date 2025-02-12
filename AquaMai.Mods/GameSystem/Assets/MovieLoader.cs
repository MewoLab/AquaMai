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
using Process;
using System;
using System.Threading.Tasks; 

namespace AquaMai.Mods.GameSystem.Assets;

[ConfigSection(
    en: "Custom Play Song Background\nPriority: game source PV > local mp4 PV > jacket",
    zh: "自定义歌曲游玩界面背景\n优先级: 首先读游戏自带 PV, 然后读本地 mp4 格式 PV, 最后读封面")]
public class MovieLoader
{
    [ConfigEntry(
        en: "Load Movie from game source",
        zh: "加载游戏自带的 PV")]
    private static bool loadSourceMovie = true;


    [ConfigEntry(
        en: "Load Movie from LocalAssets mp4 files",
        zh: "从 LocalAssets 中加载 MP4 文件作为 PV")]
    private static bool loadMp4Movie = false; // default false


    [ConfigEntry(
        en: "MP4 files directory",
        zh: "加载 MP4 文件的路径")]
    private static readonly string movieAssetsDir = "LocalAssets";


    [ConfigEntry(
        en: "Use jacket as movie\nUse together with `LoadLocalImages`.",
        zh: "用封面作为背景 PV\n请和 `LoadLocalImages` 一起用")]
    private static bool jacketAsMovie = false; // default false


    [ConfigEntry(
        en: "Jacket post-process\nUse together with `JacketAsMovie`\n" +
            "Will save jacket as input.png, run run.bat, then read output.png as background\n" +
            "Post-processor can be downloaded here, or customize your own\n" +
            "https://github.com/ck2739046/mai-post-processor/releases\n" +
            "Post-process time should not exceed 4.5s, will fallback to original jacket if timeout",
        zh: "封面后处理\n请和 `JacketAsMovie` 一起用\n" +
            "会先将歌曲封面保存为input.png, 再调用run.bat, 最后读取output.png作为背景\n" +
            "后处理程序可以在此下载，或者自行设定\n" +
            "https://github.com/ck2739046/mai-post-processor/releases\n" +
            "后处理时间请不要超过4.5秒, 如果超时将回退到原封面")]
    private static bool jacketPostProcess = false; // default false


    [ConfigEntry(
        en: "jacket post-processor directory",
        zh: "后处理程序的文件夹路径")]
    private static readonly string PostProcessorDir = "";


    private static readonly Dictionary<string, string> optionFileMap = [];
    private static uint[] bgaSize = [0, 0];

    private class MovieInfo
    {
        public enum MovieType{
            None,
            SourceMovie,
            Mp4Movie,
            Jacket,
            JacketProcessing
        }

        public MovieType Type { get; set; } = MovieType.None;
        public string Mp4Path { get; set; } = "";
        public Texture2D JacketTexture { get; set; }
        public string MusicId { get; set; } = "";
        public bool IsValid{
            get {
                if (string.IsNullOrEmpty(MusicId)) return false;
                return Type switch {
                    MovieType.None => false,
                    MovieType.SourceMovie => true,
                    MovieType.Mp4Movie => !string.IsNullOrEmpty(Mp4Path),
                    MovieType.Jacket => JacketTexture != null,
                    MovieType.JacketProcessing => JacketTexture != null,
                    _ => false
                };
            }
        }
    }
    private static MovieInfo movieInfo = new MovieInfo();


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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrackStartProcess), "OnStart")]
    public static async void GetMovie() {

        try {

            movieInfo = new MovieInfo(); // reset
            var music = Singleton<DataManager>.Instance.GetMusic(GameManager.SelectMusicID[0]);
            if (music is null) return;
            var musicID = $"{music.movieName.id:000000}";
            movieInfo.MusicId = musicID;

            // Load source movie
            if (loadSourceMovie) {
                var moviePath = Singleton<OptionDataManager>.Instance.GetMovieDataPath($"{musicID}") + ".dat";
                if (!moviePath.Contains("dummy")) {
                    movieInfo.Type = MovieInfo.MovieType.SourceMovie;
                    return;
                }
            }

            // Load localasset mp4 bga
            if (loadMp4Movie) {
                var resolvedDir = FileSystem.ResolvePath(movieAssetsDir);
                if (!optionFileMap.TryGetValue($"{musicID}.mp4", out var mp4Path)) {
                    mp4Path = Path.Combine(resolvedDir, $"{musicID}.mp4");
                }
                if (File.Exists(mp4Path)) {
                    movieInfo.Type = MovieInfo.MovieType.Mp4Movie;
                    movieInfo.Mp4Path = mp4Path; 
                    return;
                }   
            }

            // Load jacket
            if (jacketAsMovie) {
                // 尝试从game或LocalAssets获取jacket
                var jacket = LoadLocalImages.GetJacketTexture2D(music.movieName.id);
                if (jacket is null) {
                    var filename = $"Jacket/UI_Jacket_{musicID}.png";
                    jacket = AssetManager.Instance().GetJacketTexture2D(filename);
                    if (jacket is null) {
                        MelonLogger.Msg($"[MovieLoader] No jacket for {musicID}");
                        return;
                    }
                }
                movieInfo.JacketTexture = jacket;

                // 如果未开启后处理，直接将type设为jacket并返回，否则将type设为jacket_processing
                if (!jacketPostProcess) {
                    movieInfo.Type = MovieInfo.MovieType.Jacket;
                    return;
                } else {
                    movieInfo.Type = MovieInfo.MovieType.JacketProcessing; // 标记为开始后处理
                }

                // 异步调用后处理函数
                jacket = await JacketPostProcess(jacket);
                if (jacket is null) {
                    MelonLogger.Msg($"[MovieLoader] post-process return null for {musicID}");
                    return;
                }
                movieInfo.Type = MovieInfo.MovieType.Jacket; // 后处理完成
                movieInfo.JacketTexture = jacket;
            }
        } catch (System.Exception e) {MelonLogger.Msg($"[MovieLoader] GetMovie() error: {e}");}
    }

    private static async Task<Texture2D> JacketPostProcess(Texture2D jacket) {
        
        try {
            var PostProcessorDir_r = FileSystem.ResolvePath(PostProcessorDir);
            if (!Directory.Exists(PostProcessorDir_r)) {
                MelonLogger.Msg($"[MovieLoader] Directory not found: {PostProcessorDir_r}");
                return null;
            }
            var inputPath = Path.Combine(PostProcessorDir_r, "input.png");
            if (File.Exists(inputPath)) File.Delete(inputPath);
            var outputPath = Path.Combine(PostProcessorDir_r, "output.png");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            // Output jacket as input.png for post-processing
            // Use render texture becuase jacket is not readable
            // Create temp render texture
            var renderTexture = RenderTexture.GetTemporary(jacket.width, jacket.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            // Copy jacket to render texture
            Graphics.Blit(jacket, renderTexture);
            var jacket_copy = new Texture2D(jacket.width, jacket.height, TextureFormat.RGBA32, false);
            jacket_copy.ReadPixels(new Rect(0, 0, jacket.width, jacket.height), 0, 0);
            jacket_copy.Apply();
            // Restore previous render texture
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            // Save to disk
            File.WriteAllBytes(inputPath, jacket_copy.EncodeToPNG());
            UnityEngine.Object.Destroy(jacket_copy);
            if (!File.Exists(inputPath)) {
                MelonLogger.Msg($"[MovieLoader] failed to save input.png: {inputPath}");
                return null;
            }

            // Check post-processor bat exists
            var bat_path = Path.Combine(PostProcessorDir_r, "run.bat");
            if (!File.Exists(bat_path)) {
                MelonLogger.Msg($"[MovieLoader] post-process bat not found: {bat_path}");
                return null;
            }

            // Run post-processor asynchronously
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c run.bat",
                WorkingDirectory = PostProcessorDir_r,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            };
            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();
            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode == 0 && File.Exists(outputPath)) {
                // Load processed texture
                var processedTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                processedTexture.LoadImage(File.ReadAllBytes(outputPath));
                jacket = processedTexture;
            } else {
                MelonLogger.Msg($"[MovieLoader] post-process failed: {process.ExitCode}");
                return null;
            }
            
            // Clean up
            if (File.Exists(inputPath)) File.Delete(inputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            return jacket;

        } catch (System.Exception e) {
            MelonLogger.Msg($"[MovieLoader] post-process error: {e}");
            return null;
        }
    }

    private static VideoPlayer[] _videoPlayers = new VideoPlayer[2];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameCtrl), "Initialize")]
    public static void LoadLocalBgaAwake(GameObject ____movieMaskObj, int ___monitorIndex)
    {
        if (!movieInfo.IsValid) return;
        if (movieInfo.Type == MovieInfo.MovieType.SourceMovie) return;

        string mp4Path = "";
        bool mp4Exists = false;
        Texture2D jacket = null;

        if (movieInfo.Type == MovieInfo.MovieType.Mp4Movie) {
            mp4Path = movieInfo.Mp4Path;
            mp4Exists = File.Exists(mp4Path);
        }
        if (movieInfo.Type == MovieInfo.MovieType.Jacket) {
            jacket = movieInfo.JacketTexture;
        }
        if (movieInfo.Type == MovieInfo.MovieType.JacketProcessing) {
            MelonLogger.Msg($"[MovieLoader] {movieInfo.MusicId} Post-process failed " + 
                "or time out, using jacket as fallback");
            jacket = movieInfo.JacketTexture;
        }

        if (!mp4Exists && jacket is null) {
            MelonLogger.Msg($"[MovieLoader] No jacket or bga for {movieInfo.MusicId}");
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

            // 异步等待视频准备好，获取视频的长宽
            var player = _videoPlayers[___monitorIndex];
            player.prepareCompleted += (source) => {
                var vp = source as VideoPlayer;
                if (vp != null) {
                    var height = vp.height;
                    var width = vp.width;
                    // 按照比例缩放到(1080, 1080)
                    if (height > width) {
                        bgaSize = [1080, (uint)(1080.0 * width / height)];
                    } else {
                        bgaSize = [(uint)(1080.0 * height / width), 1080];
                    }
                }
            };
        }
        else
        {
            sprite.sprite = Sprite.Create(jacket, new Rect(0, 0, jacket.width, jacket.height), new Vector2(0.5f, 0.5f));
            sprite.material = new Material(Shader.Find("Sprites/Default"));
            bgaSize = [1080, 1080];
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "GetMovieHeight")]
    public static void GetMovieHeightPostfix(ref uint __result)
    {
        if (bgaSize[0] > 0) {
            __result = bgaSize[0];
            bgaSize[0] = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovieController), "GetMovieWidth")]
    public static void GetMovieWidthPostfix(ref uint __result)
    {
        if (bgaSize[1] > 0) {
            __result = bgaSize[1];
            bgaSize[1] = 0;
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