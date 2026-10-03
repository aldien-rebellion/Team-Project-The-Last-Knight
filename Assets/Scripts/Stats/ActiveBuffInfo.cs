using System;
using UnityEngine;

namespace TheLastKnight.Stats
{
    [Serializable]
    public struct ActiveBuffInfo
    {
        public string id;
        public string name;
        public string category;
        public string description;
        public string formattedTime;
        public float remainingSeconds;
        public float totalDuration;
        public Sprite icon;
        public bool isDebuff;
        public Color themeColor;
    }
}
