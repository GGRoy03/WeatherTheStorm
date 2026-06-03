using System;
using UnityEngine;

namespace WeatherTheStorm.Audio
{
    public enum GameAudio
    {
        Unknown       = 0,
        MissileLaunch = 1,
        MissileHit    = 2,
    }

    [System.Serializable]
    public struct GameAudioItem
    {
        public AudioClip Clip;
        public float     Volume;
    }

    [CreateAssetMenu(menuName = "Audio/Registry")]
    public class AudioRegistry : ScriptableObject, ISerializationCallbackReceiver
    {
        [System.Serializable]
        public struct AudioEntry
        {
            public GameAudio     Type;
            public GameAudioItem Item;
        }

        [Header("Audio Table")]
        [SerializeField] private AudioEntry[] m_AudioEntries;

        public GameAudioItem GetMatchingAudioClip(GameAudio Type)
        {
            var result = m_AudioEntries[(int)Type].Item;
            return result;
        }

        public void OnBeforeSerialize()
        {
            //
            // NOTE:
            // x) I don't know if this is stupid or not, but it allows us to use the same structure
            //    in the editor and at runtime. We basically force each entry of the table to have
            //    the "right" enum. We assume that enums are sequentials. The reason we do a != 
            //    check is because otherwise when we remove enums, it wouldn't remove the field.
            //    The downside is that, when the enum is changed we have to relink every single clip.
            //    There's probably a way, but I'll only fix it once it becomes too annoying to deal with.
            //

            GameAudio[] audioTypes = (GameAudio[])Enum.GetValues(typeof(GameAudio));
            if(audioTypes.Length != m_AudioEntries.Length)
            {
                m_AudioEntries = new AudioEntry[audioTypes.Length];
            }
            foreach(var audioType in audioTypes)
            {
                m_AudioEntries[(int)audioType].Type = audioType;
            }
        }

        public void OnAfterDeserialize()
        {

        }
    }
}
