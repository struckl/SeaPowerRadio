using System;
using SeaPower;
using SeaPowerRadio.Core;
using UnityEngine;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Every stock voice line funnels through two private methods of <c>SeaPower.VoiceMessage</c>:
/// <c>SubmitMessage</c> (PlayVoiceMessage / PlayVoiceSequence / PlayAircraftAltitude) and <c>AppendMessage</c>
/// (AddVoiceToSequence). Prefixing both gives us each line's semantic key and speaker, and returning false mutes it.
/// </summary>
internal static class StockVoicePatches
{
    public const string FeatureName = "StockVoice";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeaPower.VoiceMessage", "SubmitMessage",
            new[] { typeof(string), typeof(ObjectBase), typeof(string), typeof(int), typeof(string), typeof(bool), typeof(bool) },
            typeof(StockVoicePatches), prefix: nameof(SubmitPrefix))
        .Hook("SeaPower.VoiceMessage", "AppendMessage",
            new[] { typeof(string), typeof(ObjectBase), typeof(string), typeof(int), typeof(string) },
            typeof(StockVoicePatches), prefix: nameof(AppendPrefix));

    private static bool SubmitPrefix(string clip, ObjectBase speaker, string nation, int number, string format,
        bool sequence, bool altitude) =>
        Capture(clip, speaker, nation, number, format, sequence, altitude, appended: false);

    private static bool AppendPrefix(string clip, ObjectBase speaker, string nation, int number, string format) =>
        Capture(clip, speaker, nation, number, format, sequence: true, altitude: false, appended: true);

    private static bool Capture(string clip, ObjectBase speaker, string nation, int number, string format,
        bool sequence, bool altitude, bool appended)
    {
        try
        {
            var request = new StockVoiceRequest
            {
                Key = clip,
                Speaker = speaker,
                Nation = nation,
                Number = number,
                Format = format,
                Sequence = sequence,
                Altitude = altitude,
                Appended = appended,
                Time = Time.unscaledTime,
                Frame = Time.frameCount,
            };

            if (Settings.LogStockVoice.Value)
            {
                string who = speaker != null ? speaker.getUIDAndName() : "(taskforce " + nation + ")";
                string num = number >= 0 ? " #" + number : "";
                ModLog.Info($"[stock-voice] {(appended ? "+" : "")}{clip}{num} <- {who}");
            }

            EventBus.Publish(request);
            return !Settings.SuppressStockVoice.Value;
        }
        catch (Exception ex)
        {
            ModLog.Error($"Stock voice capture failed for '{clip}': {ex.Message}");
            return true; // let the game play its line
        }
    }
}
