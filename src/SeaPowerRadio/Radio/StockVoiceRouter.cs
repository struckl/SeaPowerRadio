using SeaPowerRadio.Core;
using SeaPowerRadio.Roster;

namespace SeaPowerRadio.Radio;

/// <summary>
/// Decides, for every voice line the game asks for, whether the mod speaks it instead. Called synchronously from
/// the VoiceMessage prefix; returning true mutes the stock line.
/// <list type="bullet">
/// <item>Known key with a phrase → the speaking unit's own voice and callsign say it.</item>
/// <item>Known key without a phrase (contact report building blocks…) → muted; the mod reports contacts itself.</item>
/// <item>Pieces the game appends to a call it already made ("…track 7034", "salvo") → muted with the call.</item>
/// <item>Unknown key (another mod's lines) → the stock system plays it as before.</item>
/// </list>
/// </summary>
internal static class StockVoiceRouter
{
    public static RadioPlayer Player;

    public static bool Handle(StockVoiceRequest request)
    {
        if (!Settings.ReplaceStockVoices.Value || !Catalog.Loaded || Player == null)
            return false;
        Roster.Roster roster = RosterService.Current;
        if (roster == null)
            return false; // menus, editor: no mission roster to speak with
        if (!Catalog.TryStock(request.Key, out string phrase, out string numberKind))
            // Unknown key. A piece appended to a task force report is one of the game's contact building blocks
            // built at runtime – swallow it with the rest of that report. Anything else (other mods' lines) plays.
            return request.Appended && request.Speaker == null;
        if (request.Appended && request.Key != "SplashedTrack")
            return true;
        if (phrase == null)
            return true;

        RosterElement speaker = request.Speaker != null ? roster.Resolve(request.Speaker) : roster.CommandVoice();
        if (speaker == null)
            return true; // an enemy or neutral unit "talking": the player's net doesn't hear it

        int? number = numberKind != null && request.Number >= 0 ? request.Number : (int?)null;
        Transmission t = TransmissionBuilder.Build(speaker, phrase, number);
        if (t == null)
            return true;
        // Lines a unit says about itself in reply to the player: acknowledgements, manoeuvres, sensors, refusals.
        t.Response = request.Speaker != null && t.Priority >= 6;
        Player.Submit(t);
        return true;
    }
}
