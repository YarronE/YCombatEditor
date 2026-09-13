using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Latest intent per key. Reading never consumes; only a committed action removes intent.</summary>
    public sealed class ActionInputBuffer
    {
        readonly Dictionary<KeyCode, float> entries = new Dictionary<KeyCode, float>();
        public int Count => entries.Count;
        public void Capture(KeyCode key, float time) { if (key != KeyCode.None) entries[key] = time; }
        public bool Contains(KeyCode key, float now, float lifetime)
        {
            if (!entries.TryGetValue(key, out float time)) return false;
            if (now < time || now - time > Mathf.Max(0, lifetime)) { entries.Remove(key); return false; }
            return true;
        }
        public void Consume(KeyCode key) => entries.Remove(key);
        public void Clear() => entries.Clear();
    }
}
