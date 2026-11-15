using Crosswind.Core;

namespace Crosswind.Game
{
    public sealed partial class Hud
    {
        int _overlayWant = -1;
        void NeedOverlay(Want? want) { _overlayWant = want.HasValue ? (int)want.Value : -1; }
        string NeedCoverageLine(Need n) { return ""; }
    }
}
