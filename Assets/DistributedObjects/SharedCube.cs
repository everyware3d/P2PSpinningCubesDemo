using System;
using System.Collections.Generic;
using UnityEngine;
using P2PPlugin.Network;

public class SharedCube : P2PNetworkComponent
{
    public static Color[] colorPalette = new Color[] {
        Color.blue,
        Color.green,
        Color.softRed,
        Color.mediumPurple,
        Color.orangeRed,
        Color.beige,
        Color.turquoise,
        Color.softYellow,
        Color.lightCyan
    };
    public static Dictionary<long, SharedCube> allSharedCubes = new Dictionary<long, SharedCube>();
    public static Dictionary<long, Color> assignedColors = new Dictionary<long, Color>();

    /* setAssignedColor: Sets the color of the SharedCube to the color looked up in assignedColors by the peerID

    */
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
    static public void setAssignedColor(SharedCube sc, GameObject go = null, long peerID = 0)
    {
        Color color;
        Renderer rend = (go != null) ? go.GetComponent<Renderer>() : (sc != null) ? sc.GetComponent<Renderer>() : null;
        if (rend == null)
        {
            return;
        }
        long pID = (peerID == 0 && sc != null) ? sc.sourceComputerID : peerID;
        if (!assignedColors.TryGetValue(pID, out color))
            color = Color.grey;
        rend.material.color = color;
    }

    [P2PSkip, HideInInspector]

    private Vector3 _translation;
    public Vector3 translation
    {
        get => _translation;
        set
        {
            gameObject.transform.position = value;
            _translation = value;
        }
    }
    private Vector3 _color;
    public Vector3 color
    {
        get => _color;
        set
        {
            _color = value;
        }
    }
    // NEEDS PUBLIC CONSTRUCTOR
    public SharedCube()
    {
    }
    // AfterDeleteRemote() is only called on instances created from remote computers
    public void AfterDeleteRemote()
    {
        // do not need to check !isLocal, but might be good to double check?
        allSharedCubes.Remove(uniqueID);
        Destroy(gameObject);
    }

    public void AfterInsertRemote()
    {
        // This is instantiation for instances added from a remote computer, the GameObject should be setup
        gameObject.transform.SetParent(AddCubeOnClick.Instance.mainCamera.transform);
        gameObject.transform.position = translation;
        gameObject.SetActive(true);
        setAssignedColor(this);
        allSharedCubes.Add(uniqueID, this);
    }
    // to get the GameObject that has this SharedCube Component
    static public GameObject spawnNewRemoteObject()
    {
        GameObject newGO = Instantiate(AddCubeOnClick.Instance.prefabToSpawn, Vector3.zero, Quaternion.identity);
        return newGO;
    }
}
