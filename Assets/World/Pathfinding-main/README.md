# Pathfinding

Pathfinding - using Unity Navmesh, ECS and Burst.

This fork represents self-sufficient
module that can be used with Unity Entities and NavMesh.

### Authoring pathfinder

In order to add all necessary components to authoring object
simply add `PathfinderAuthoring` component.

Specify agentID and minimal query distance and it's ready to be baked.

### Finding path

To find a path you need to get `PathfinderAspect` 
of relevant entity.

For example:
```csharp
var path = SystemAPI.GetAspect<PathfinderAspect>(entity);
path.FindPath(pathFrom, pathTo);
```

After query is done dynamic buffer `PathBuffer` will be filled with
direct path position. `Pathfinder` component will also get it's
`pathStatus` field updated.