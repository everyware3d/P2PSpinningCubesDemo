using UnityEngine;

public interface P2PInteractionHandler
{
    static public P2PInteractionHandler Instance;
    public GameObject getParentOfSpawnedGOs(); // parent GameObject to hold all spawned SharedCube instances
    public GameObject getPrefabToSpawn();     // prefab GameObject created when clicked on an empty space, has SharedCube component
    public GameObject getOutlineForColor();   // screen stabilized object that shows the current user's color for cubes

}
