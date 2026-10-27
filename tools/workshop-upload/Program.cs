// Updates the mod's Steam Workshop item through the running Steam client – the same calls the game's
// Mods > Create Mod makes for an existing item (CreateModMenuViewModel.SubmitToWorkshop), minus everything this
// update doesn't touch: title, description, tags, preview, visibility, dependencies and the compatibility metadata
// stay as they are on the item. Needs Steam running, signed in with the account that owns the item; no password.
//
//   dotnet run --project tools/workshop-upload -- check  <item id>                    (also lists the page's previews)
//   dotnet run --project tools/workshop-upload -- upload <item id> <content folder> <change note file>
//   dotnet run --project tools/workshop-upload -- video  <item id> <YouTube link or id>  (replaces the page's video)
using System.Text.RegularExpressions;
using Steamworks;

const uint SeaPowerAppId = 1286220;

if (args.Length < 2 || (args[0] == "upload" && args.Length < 4) || (args[0] == "video" && args.Length < 3)
    || !ulong.TryParse(args[1], out ulong itemId))
{
    Console.Error.WriteLine("usage: check <item id> | upload <item id> <content folder> <change note file> | " +
                            "video <item id> <YouTube link or id>");
    return 2;
}

// Outside a Steam launch the API takes the app id from the environment (or steam_appid.txt).
Environment.SetEnvironmentVariable("SteamAppId", SeaPowerAppId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", SeaPowerAppId.ToString());
if (!SteamAPI.Init())
{
    Console.Error.WriteLine("SteamAPI.Init failed: is Steam running and signed in with the account that owns Sea Power?");
    return 1;
}

try
{
    var item = new PublishedFileId_t(itemId);
    var previews = new List<(EItemPreviewType type, string what)>();
    if (!Details(item, out SteamUGCDetails_t details, previews))
        return 1;
    Console.WriteLine($"Item {itemId}: \"{details.m_rgchTitle}\", owner {details.m_ulSteamIDOwner}, " +
                      $"visibility {details.m_eVisibility}, updated {DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeUpdated):u}");
    for (int i = 0; i < previews.Count; i++)
        Console.WriteLine($"  preview {i}: {(previews[i].type == EItemPreviewType.k_EItemPreviewType_YouTubeVideo ? "YouTube" : previews[i].type.ToString())} {previews[i].what}");
    ulong me = SteamUser.GetSteamID().m_SteamID;
    if (details.m_ulSteamIDOwner != me)
    {
        Console.Error.WriteLine($"Signed in as {me}, not the owner of this item - not uploading.");
        return 1;
    }
    if (args[0] == "check")
        return 0;

    if (args[0] == "video")
    {
        // The video takes the old one's place among the previews (in front of the screenshots); no change note,
        // the mod itself didn't change.
        Match m = Regex.Match(args[2], @"(?:v=|youtu\.be/|shorts/|^)([A-Za-z0-9_-]{11})(?:$|[?&#/])");
        if (!m.Success)
        {
            Console.Error.WriteLine($"Not a YouTube link or id: {args[2]}");
            return 2;
        }
        string id = m.Groups[1].Value;
        int at = previews.FindIndex(p => p.type == EItemPreviewType.k_EItemPreviewType_YouTubeVideo);
        UGCUpdateHandle_t video = SteamUGC.StartItemUpdate(new AppId_t(SeaPowerAppId), item);
        bool set = at >= 0 ? SteamUGC.UpdateItemPreviewVideo(video, (uint)at, id) : SteamUGC.AddItemPreviewVideo(video, id);
        if (!set)
        {
            Console.Error.WriteLine("Steam refused the video.");
            return 1;
        }
        Console.WriteLine(at >= 0 ? $"Replacing preview {at} ({previews[at].what}) with {id} ..." : $"Adding {id} ...");
        return Submit(video, null, item) ? 0 : 1;
    }

    string folder = Path.GetFullPath(args[2]);
    string note = File.ReadAllText(args[3]).Trim();
    if (!File.Exists(Path.Combine(folder, "_info.ini")))
    {
        Console.Error.WriteLine($"{folder} has no _info.ini - not a mod folder.");
        return 1;
    }

    UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(SeaPowerAppId), item);
    if (!SteamUGC.SetItemContent(update, folder.Replace("\\", "/")))
    {
        Console.Error.WriteLine("SetItemContent refused the folder.");
        return 1;
    }
    Console.WriteLine($"Uploading {folder} ...");
    return Submit(update, note, item) ? 0 : 1;
}
finally
{
    SteamAPI.Shutdown();
}

static bool Submit(UGCUpdateHandle_t update, string note, PublishedFileId_t item)
{
    SubmitItemUpdateResult_t result = default;
    bool done = false, ioFailure = false;
    var call = CallResult<SubmitItemUpdateResult_t>.Create((r, failed) => { result = r; ioFailure = failed; done = true; });
    call.Set(SteamUGC.SubmitItemUpdate(update, note));
    DateTime nextReport = DateTime.MinValue;
    while (!done)
    {
        SteamAPI.RunCallbacks();
        if (DateTime.Now >= nextReport)
        {
            EItemUpdateStatus status = SteamUGC.GetItemUpdateProgress(update, out ulong processed, out ulong total);
            Console.WriteLine($"  {status} {processed / 1048576.0:0.0} / {total / 1048576.0:0.0} MB");
            nextReport = DateTime.Now.AddSeconds(5);
        }
        Thread.Sleep(100);
    }
    Console.WriteLine($"Result: {result.m_eResult}{(ioFailure ? " (I/O failure)" : "")}");
    if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
        Console.WriteLine("Steam wants the Workshop legal agreement accepted on the item page before it goes live.");
    if (Details(item, out SteamUGCDetails_t details, null))
        Console.WriteLine($"Item now updated {DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeUpdated):u}");
    return result.m_eResult == EResult.k_EResultOK && !ioFailure;
}

static bool Details(PublishedFileId_t item, out SteamUGCDetails_t details, List<(EItemPreviewType type, string what)> previews)
{
    details = default;
    UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { item }, 1);
    SteamUGC.SetReturnAdditionalPreviews(query, previews != null);
    SteamUGCQueryCompleted_t completed = default;
    bool done = false, failed = false;
    var call = CallResult<SteamUGCQueryCompleted_t>.Create((r, f) => { completed = r; failed = f; done = true; });
    call.Set(SteamUGC.SendQueryUGCRequest(query));
    DateTime until = DateTime.Now.AddSeconds(30);
    while (!done && DateTime.Now < until)
    {
        SteamAPI.RunCallbacks();
        Thread.Sleep(100);
    }
    bool ok = done && !failed && completed.m_eResult == EResult.k_EResultOK && completed.m_unNumResultsReturned > 0
              && SteamUGC.GetQueryUGCResult(query, 0, out details) && details.m_eResult == EResult.k_EResultOK;
    if (ok && previews != null)
        for (uint i = 0; i < SteamUGC.GetQueryUGCNumAdditionalPreviews(query, 0); i++)
            if (SteamUGC.GetQueryUGCAdditionalPreview(query, 0, i, out string url, 1024, out string file, 260, out EItemPreviewType type))
                previews.Add((type, type == EItemPreviewType.k_EItemPreviewType_YouTubeVideo || string.IsNullOrEmpty(file) ? url : file));
    SteamUGC.ReleaseQueryUGCRequest(query);
    if (!ok)
        Console.Error.WriteLine($"Could not read item {item.m_PublishedFileId} ({(done ? completed.m_eResult.ToString() : "timeout")}).");
    return ok;
}
