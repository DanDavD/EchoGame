using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Safety net for the NavMesh. The sandbox builder normally bakes and saves the NavMesh into the scene,
/// but if it is missing or empty at runtime this builds it on the fly before enemies start.
/// Put it on the same GameObject as the NavMeshSurface.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
[DefaultExecutionOrder(-200)] // Start() runs before EnemyBrain.Start()
public class NavMeshBootstrap : MonoBehaviour
{
    private void Start()
    {
        // By Start(), all enabled surfaces have registered their data, so an empty NavMesh really is empty.
        if (NavMesh.CalculateTriangulation().vertices.Length > 0) return;

        Debug.Log("[NavMeshBootstrap] No NavMesh found, building at runtime.", this);
        GetComponent<NavMeshSurface>().BuildNavMesh();
    }
}
