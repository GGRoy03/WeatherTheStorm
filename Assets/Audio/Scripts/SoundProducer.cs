using Unity.Mathematics;
using Unity.Collections;

using UnityEngine;

namespace WeatherTheStorm.Audio
{
    public struct SpatialSoundRequest
    {
        public GameAudio Type;
        public float3    Position;
    }

    public static class SoundProducer
    {
        private static NativeQueue<SpatialSoundRequest>                m_SpatialSoundQueue;
        private static NativeQueue<SpatialSoundRequest>.ParallelWriter m_SpatialSoundWriter;

        public static void PlaySpatialSound(SpatialSoundRequest request)
        {
            //
            // TODO:
            // x) Possibly not thread-safe? I think this could leak memory, how do we make it
            //    safe without an initialization routine?
            //
            
            if(!m_SpatialSoundQueue.IsCreated)
            {
                m_SpatialSoundQueue  = new NativeQueue<SpatialSoundRequest>(Allocator.Persistent);
                m_SpatialSoundWriter = m_SpatialSoundQueue.AsParallelWriter();
            }

            m_SpatialSoundWriter.Enqueue(request);
        }

        public static bool TryDequeueSpatialSoundRequest(out SpatialSoundRequest request)
        {
            bool result = false;

            if(m_SpatialSoundQueue.IsCreated)
            {
                result = m_SpatialSoundQueue.TryDequeue(out request);
            }
            else
            {
                request.Type     = GameAudio.Unknown;
                request.Position = float3.zero;
            }

            return result;
        }
    }
}