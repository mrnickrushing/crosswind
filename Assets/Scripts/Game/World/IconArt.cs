using System.Collections.Generic;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The interface's picture icons: renders of free 3D models (Resources/UI/Icons, made by Tools/art/icon_models.py;
    /// the models and their authors are listed in CREDITS.md and under EMPIRE). <see cref="Of"/> returns null for a
    /// name with no picture, and the flat glyph is used instead.
    /// </summary>
    public static class IconArt
    {
        static readonly Dictionary<string, Sprite> _made = new Dictionary<string, Sprite>();

        public static Sprite Of(string name)
        {
            if (name == null) return null;
            if (name == "manager") return Portraits.Of("mgr_fuel");
            Sprite s;
            if (!_made.TryGetValue(name, out s)) _made[name] = s = Resources.Load<Sprite>("UI/Icons/" + name);
            return s;
        }
    }
}
