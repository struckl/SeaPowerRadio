using System;
using SeaPower;
using SeaPowerRadio.Core;
using UnityEngine;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Every stock voice line funnels through two private methods of <c>SeaPower.VoiceMessage</c>:
/// <c>SubmitMessage</c> (PlayVoiceMessage / PlayVoiceSequence / PlayAircraftAltitude) and <c>AppendMessage</c>
/// (AddVoiceToSequence). Prefixing both gives us each line's semantic key and speaker; when the mod speaks the
/// line itself (Radio/StockVoiceRouter) the prefix returns false and the stock line is never queued.
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

    // Harmony matches the hooked methods' parameters by name, so both prefixes list them all.
    private static bool SubmitPrefix(string clip, ObjectBase speaker, string nation, int number, string format,
        bool sequence, bool altitude) =>
        Capture(clip, speaker, nation, number, appended: false);

    private static bool AppendPrefix(string clip, ObjectBase speaker, string nation, int number, string format) =>
        Capture(clip, speaker, nation, number, appended: true);

    private static bool Capture(string clip, ObjectBase speaker, string nation, int number, bool appended)
    {
        try
        {
            var request = new StockVoiceRequest
            {
                Key = clip,
                Speaker = speaker,
                Number = number,
                Appended = appended,
                Frame = Time.frameCount,
            };

            if (Settings.LogStockVoice.Value)
            {
                string who = speaker != null ? speaker.getUIDAndName() : "(taskforce " + nation + ")";
                string num = number >= 0 ? " #" + number : "";
                ModLog.Info($"[stock-voice] {(appended ? "+" : "")}{clip}{num} <- {who}");
            }

            return !Radio.StockVoiceRouter.Handle(request);
        }
        catch (Exception ex)
        {
            ModLog.Error($"Stock voice capture failed for '{clip}': {ex.Message}");
            return true;
        }
    }
}
