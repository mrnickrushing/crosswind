using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>
    /// The save is written by Unity's JsonUtility, which silently skips any field whose class isn't [Serializable]:
    /// the terminal's furniture was once lost at every launch that way. Every class the save holds must be marked.
    /// </summary>
    public class SaveTests
    {
        [Test]
        public void EverythingTheSaveHoldsIsSerializable()
        {
            var bad = new List<string>();
            var seen = new HashSet<Type>();
            Check(typeof(GameState), "GameState", bad, seen);
            Assert.IsEmpty(bad, "not [Serializable], so never saved: " + string.Join(", ", bad));
        }

        static void Check(Type t, string path, List<string> bad, HashSet<Type> seen)
        {
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || !seen.Add(t)) return;
            if (t.IsArray) { Check(t.GetElementType(), path + "[]", bad, seen); return; }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) { Check(t.GetGenericArguments()[0], path + "[]", bad, seen); return; }
            if (t.Namespace != null && t.Namespace.StartsWith("System")) return;
            if (!t.IsSerializable) bad.Add(path + " (" + t.Name + ")");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (!f.IsNotSerialized) Check(f.FieldType, path + "." + f.Name, bad, seen);
        }
    }
}
