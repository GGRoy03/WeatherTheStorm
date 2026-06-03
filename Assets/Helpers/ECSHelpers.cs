using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace WeatherTheStorm.Helpers
{
    public static class MonoToECS
    {
        //
        // TODO: I kind of wanted to cache queries as this seems to be the recommended approach,
        //       though the only sane way would be to use some form of hashmap? If I were to use
        //       one I'd have to actually check first that the query building overhead is higher
        //       than the hashmap overhead.
        //

        public static T GetSingletonFromMainWorld<T>() where T : unmanaged, IComponentData
        {
            T result = default(T);

            World targetWorld = World.DefaultGameObjectInjectionWorld;
            if (targetWorld != null)
            {

                EntityManager entityManager = targetWorld.EntityManager;
                EntityQuery   entityQuery   = entityManager.CreateEntityQuery(typeof(T));

                if(entityQuery.HasSingleton<T>())
                {
                    result = entityQuery.GetSingleton<T>();
                }
            }

            return result;
        }
    }
}