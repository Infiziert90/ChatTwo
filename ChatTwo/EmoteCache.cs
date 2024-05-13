﻿using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Interface.Internal;
using Dalamud.Utility;
using ImGuiNET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChatTwo;

public static class EmoteCache
{
    private static readonly HttpClient Client = new();

    private const string BetterTTV = "https://api.betterttv.net/3";
    private const string GlobalEmotes = $"{BetterTTV}/cached/emotes/global";
    private const string Top100Emotes = "{0}/emotes/shared/top?before={1}&limit=100";
    private const string EmotePath = "https://cdn.betterttv.net/emote/{0}/1x";

    private struct Top100
    {
        [JsonPropertyName("emote")]
        public Emote Emote { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public struct Emote
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("code")]
        public string Code { get; set; }
        [JsonPropertyName("imageType")]
        public string ImageType { get; set; }
    };

    public enum LoadingState
    {
        Unloaded,
        Loading,
        Done
    }

    // All of this data is uninitalized while State is not `LoadingState.Done`
    public static LoadingState State = LoadingState.Unloaded;

    private static readonly Dictionary<string, Emote> Cache = new();
    private static readonly Dictionary<string, IEmote> EmoteImages = new();

    public static string[] SortedCodeArray = [];

    public static async void LoadData()
    {
        if (State is not LoadingState.Unloaded)
            return;

        State = LoadingState.Loading;
        try
        {
            var global = await Client.GetAsync(GlobalEmotes);
            var globalList = await global.Content.ReadAsStringAsync();

            foreach (var emote in JsonSerializer.Deserialize<Emote[]>(globalList)!)
                Cache.TryAdd(emote.Code, emote);

            var lastId = string.Empty;
            for (var i = 0; i < 15; i++)
            {
                var top = await Client.GetAsync(Top100Emotes.Format(BetterTTV, lastId));
                var topList = await top.Content.ReadAsStringAsync();

                var jsonList = JsonSerializer.Deserialize<List<Top100>>(topList)!;
                foreach (var emote in jsonList)
                    Cache.TryAdd(emote.Emote.Code, emote.Emote);

                lastId = jsonList.Last().Id;
            }

            SortedCodeArray = Cache.Keys.Order().ToArray();
            State = LoadingState.Done;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "BetterTTV cache wasn't initialized");
        }
    }

    internal static bool Exists(string code)
    {
        return State is LoadingState.Done && SortedCodeArray.Contains(code);
    }

    internal static IEmote? GetEmote(string code)
    {
        if (State is not LoadingState.Done)
            return null;

        if (!Cache.TryGetValue(code, out var emoteDetail))
            return null;

        if (EmoteImages.TryGetValue(emoteDetail.Id, out var emote))
            return emote;

        try
        {
            if (emoteDetail.ImageType == "gif")
            {
                var animatedEmote = new ImGuiGif().Prepare(emoteDetail);
                EmoteImages.Add(emoteDetail.Id, animatedEmote);
                return animatedEmote;
            }

            var staticEmote = new ImGuiEmote().Prepare(emoteDetail);
            EmoteImages.Add(emoteDetail.Id, staticEmote);

            return staticEmote;
        }
        catch
        {
            Plugin.Log.Error("Failed to convert");
            return null;
        }
    }

    public class IEmote
    {
        public bool Failed;
        public bool IsLoaded;

        public IDalamudTextureWrap Texture;

        public virtual void Draw(Vector2 size)
        {
            ImGui.Image(Texture.ImGuiHandle, size);
        }

        internal static async Task<byte[]> LoadAsync(Emote emote)
        {
            var dir = Path.Join(Plugin.Interface.ConfigDirectory.FullName, "emotes");
            Directory.CreateDirectory(dir);

            byte[] image;
            var filePath = Path.Join(dir, $"{emote.Id}.{emote.ImageType}");
            if (File.Exists(filePath))
            {
                image = await File.ReadAllBytesAsync(filePath);
            }
            else
            {
                var content = await new HttpClient().GetAsync(EmotePath.Format(emote.Id));
                image = await content.Content.ReadAsByteArrayAsync();

                await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                stream.Write(image, 0, image.Length);
            }

            return image;
        }
    }

    public sealed class ImGuiEmote : IEmote
    {
        public ImGuiEmote Prepare(Emote emote)
        {
            Task.Run(() => Load(emote));
            return this;
        }

        private async void Load(Emote emote)
        {
            try
            {
                var image = await LoadAsync(emote);
                if (image.Length <= 0)
                    return;

                Texture = await Plugin.Interface.UiBuilder.LoadImageAsync(image);
                IsLoaded = true;
            }
            catch (Exception ex)
            {
                Failed = true;
                Plugin.Log.Error(ex, $"Unable to load {emote.Code} with id {emote.Id}");
            }
        }
    }

    public sealed class ImGuiGif : IEmote
    {
        private List<(IDalamudTextureWrap Texture, float Delay)> Frames = [];
        private float FrameTimer;
        private int CurrentFrame;
        private ulong GlobalFrameCount;

        public bool IsPaused;

        public override void Draw(Vector2 size)
        {
            if (Frames.Count == 0)
                return;

            if (CurrentFrame >= Frames.Count)
            {
                CurrentFrame = 0;
                FrameTimer = -1f;
            }

            var frame = Frames[CurrentFrame];
            if (FrameTimer <= 0.0f)
                FrameTimer = frame.Delay;

            ImGui.Image(frame.Texture.ImGuiHandle, size);

            if (IsPaused)
                return;

            if (GlobalFrameCount != Plugin.Interface.UiBuilder.FrameCount)
            {
                GlobalFrameCount = Plugin.Interface.UiBuilder.FrameCount;

                FrameTimer -= ImGui.GetIO().DeltaTime;
                if (FrameTimer <= 0f)
                    CurrentFrame++;
            }
        }

        public void Dispose()
        {
            Frames.ForEach(f => f.Texture.Dispose());
            Frames.Clear();
        }

        public ImGuiGif Prepare(Emote emote)
        {
            Task.Run(() => Load(emote));
            return this;
        }

        private async void Load(Emote emote)
        {
            try
            {
                var image = await LoadAsync(emote);
                if (image.Length <= 0)
                    return;

                using var ms = new MemoryStream(image);
                using var img = Image.Load<Rgba32>(ms);
                if (img.Frames.Count == 0)
                    return;

                var frames = new List<(IDalamudTextureWrap Tex, float Delay)>();
                foreach (var frame in img.Frames)
                {
                    var delay = frame.Metadata.GetGifMetadata().FrameDelay / 100f;

                    // Follows the same pattern as browsers, anything under 0.02s delay will be rounded up to 0.1s
                    if (delay < 0.02f)
                        delay = 0.1f;

                    var buffer = new byte[4 * frame.Width * frame.Height];
                    frame.CopyPixelDataTo(buffer);
                    var tex = await Plugin.Interface.UiBuilder.LoadImageRawAsync(buffer, frame.Width, frame.Height, 4);
                    frames.Add((tex, delay));
                }

                Frames = frames;
                IsLoaded = true;
            }
            catch (Exception ex)
            {
                Failed = true;
                Plugin.Log.Error(ex, $"Unable to load {emote.Code} with id {emote.Id}");
            }
        }
    }
}