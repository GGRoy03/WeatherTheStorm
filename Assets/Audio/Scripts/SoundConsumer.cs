using Unity.Collections;
using Unity.Mathematics;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Audio;

namespace WeatherTheStorm.Audio
{
    public class SoundConsumer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioRegistry m_AudioRegistry;

        [Header("Pooling")]
        [SerializeField] private int           m_AudioPoolSize;
                         private int           m_AudioPoolIndex;
                         private AudioSource[] m_AudioPool;

        private void Start()
        {
            //
            // NOTE:
            // This doesn't really need an allocation, but couldn't figure out how to make it use
            // the audio pool size which should sort of be a constant?
            //

            m_AudioPool = new AudioSource[m_AudioPoolSize];

            for(int PoolIdx = 0; PoolIdx < m_AudioPoolSize; ++PoolIdx)
            {
                GameObject gameObject = new GameObject("AudioSource_" + PoolIdx);
                gameObject.transform.parent = transform;

                m_AudioPool[PoolIdx] = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Update()
        {
            SpatialSoundRequest spatialSoundRequest;
            while(SoundProducer.TryDequeueSpatialSoundRequest(out spatialSoundRequest))
            {
                AudioSource source = GetNextAudioSource();
                if(source != null)
                {
                    GameAudioItem item = m_AudioRegistry.GetMatchingAudioClip(spatialSoundRequest.Type);
                    source.transform.position = spatialSoundRequest.Position;
                    source.clip               = item.Clip;
                    source.volume             = item.Volume;

                    source.Play();
                }
            }
        }

        private AudioSource GetNextAudioSource()
        {
            //
            // TODO:
            // Should we check if we exceeded the audio source ring buffer for the frame?
            // I don't quite get how audio sources really work.
            //

            int         currentIndex = m_AudioPoolIndex;
            AudioSource result       = m_AudioPool[currentIndex];

            m_AudioPoolIndex = (currentIndex + 1) % m_AudioPoolSize;

            return result;
        }
    }
}