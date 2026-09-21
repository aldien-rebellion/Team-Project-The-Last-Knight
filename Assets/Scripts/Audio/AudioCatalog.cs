using UnityEngine;

namespace TheLastKnight.Audio
{
    [CreateAssetMenu(menuName = "The Last Knight/Audio Catalog")]
    public class AudioCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry { public string id; public AudioClip clip; }
        public Entry[] clips = new Entry[0];
        public AudioClip Find(string id)
        {
            foreach (var entry in clips) if (entry.id == id) return entry.clip;
            return null;
        }
    }
}
