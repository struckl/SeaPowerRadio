using SeaPower;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Patches;

/// <summary>
/// A speed set in knots (the speed slider of ships, submarines and aircraft) ends in <c>ObjectSpeed.RaiseSpeedAlert</c>,
/// which only puts up "Speed set to 15 kn" – no voice line. The postfix has the unit say it ("speed one five knots").
/// </summary>
internal static class SpeedOrderPatches
{
    public const string FeatureName = "SpeedKnots";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeapowerUI.ViewModels.ObjectSpeed", "RaiseSpeedAlert", new[] { typeof(ObjectBase), typeof(float) },
            typeof(SpeedOrderPatches), postfix: nameof(RaiseSpeedAlertPostfix));

    private static void RaiseSpeedAlertPostfix(ObjectBase objectBase, float knots) =>
        StockVoiceRouter.SpeedInKnots(objectBase, knots);
}
