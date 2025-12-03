using Unity.Entities;
using UnityEngine;

// Register UnityEngine components we attach at runtime so EntityManager is aware of them.
[assembly: RegisterUnityEngineComponentType(typeof(Mesh))]
