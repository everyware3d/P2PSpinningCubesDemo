using System;
using System.Collections.Generic;
using UnityEngine;
using P2PPlugin.Network;

public class SharedCube : P2PNetworkComponent
{
    // by default, fields and properties are distributed
    // translation is a property with a backing field _translation to demonstrate property distribution
    // backing field is defined private, not distributed (P2PSkip), and is shown in the 
    // inspector (SerializeField) as read-only (P2PReadOnly)
    [P2PSkip]
    [SerializeField]
    [P2PReadOnly]
    private Vector3 _translation;
    public Vector3 translation
    {
        get => _translation;
        private set
        {
            gameObject.transform.position = value;  // when translation is set (from local or remote), update the GameObject position
            _translation = value;
        }
    }
    public void SetTranslation(Vector3 t)
    {
        translation = t; // translation.set is private to show translation in inspector as read-only
    }

    /*  Triggers AfterInsertRemote() and AfterDeleteRemote()) called when a SharedCube instance is
        inserted or deleted from a remote computer. If the related data structures want to be used for all
        instances, then these methods should be called when the local instances are created or deleted. */
    public void AfterInsertRemote()
    {
        gameObject.transform.SetParent(P2PSharedCubeInteractionHandler.Instance.mainCamera.transform);
        gameObject.transform.position = translation;
        gameObject.SetActive(true);
        setAssignedColorToCube(this);
        allSharedCubes.Add(uniqueID, this);
    }
    public void AfterDeleteRemote()
    {
        allSharedCubes.Remove(uniqueID);
        Destroy(gameObject);  // remove the GameObject when the SharedCube instance is deleted remotely
    }

    // GameObject that has this SharedCube Component, which is instantiated when remote SharedCube instances are created
    static public GameObject spawnNewRemoteObject()
    {
        GameObject newGO = Instantiate(P2PSharedCubeInteractionHandler.Instance.prefabToSpawn, Vector3.zero, Quaternion.identity);
        return newGO;
    }
    /* Data structures to store all SharedCubes and color assignments */
    public static Dictionary<long, SharedCube> allSharedCubes = new Dictionary<long, SharedCube>();
    public static Dictionary<long, Color> assignedColors = new Dictionary<long, Color>();

    /* setAssignedColor: Helper functions to set color to look up in assignedColors by the peerID */
    static public void setAssignedColorToRenderer(Renderer rend, long peerID)
    {
        Color color;
        if (!assignedColors.TryGetValue(peerID, out color))
            color = Color.grey;
        rend.material.color = color;
    }
    static public void setAssignedColorToCube(SharedCube sc)
    {
        Renderer rend = sc?.GetComponent<Renderer>();
        if (rend == null) return;
        setAssignedColorToRenderer(rend, sc.sourceComputerID);
    }
    static public void setAssignedColorToGameObject(GameObject go, long peerID)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        setAssignedColorToRenderer(rend, peerID);
    }

    /* Needs a public constructor with no arguments for remote instantiation */
    public SharedCube()
    {
    }
}
