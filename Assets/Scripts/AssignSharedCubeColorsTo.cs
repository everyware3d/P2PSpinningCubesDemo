using System.Collections.Generic;
using P2PPlugin.Network;
using UnityEngine;

public class AssignSharedCubeColorsTo : MonoBehaviour
{
    public static Color[] colorPalette = new Color[] {
        Color.blue,
        Color.softGreen,
        Color.softRed,
        Color.mediumPurple,
        Color.softYellow,
        Color.beige,
        Color.turquoise,
        Color.lightCyan
    };
    public enum ColorAssignmentEnum
    {
        CreationTime = 0,
        //        PeerID
    }

    ColorAssignmentEnum colorAssignmentState = ColorAssignmentEnum.CreationTime;
    public ColorAssignmentEnum ColorAssignment
    {
        get { return colorAssignmentState; }
        set
        {
            if (value != colorAssignmentState)
            {
                colorAssignmentState = value;
                ColorAssignmentStateUpdated();
            }
        }
    }
    void ColorAssignmentStateUpdated() {
        bool isCreationTime = colorAssignmentState == ColorAssignmentEnum.CreationTime;
        if (localComputer.getInserted() != isCreationTime)
        {
            if (isCreationTime)
            {
                localComputer.Insert();
                localComputer.AfterInsertRemote();
            }
            else
            {
                localComputer.Delete();
                localComputer.AfterDeleteRemote();

            }
            localComputerInserted = isCreationTime;
        }
    }
    public class P2PComputerComparer : IComparer<P2PComputer>
    {
        public int Compare(P2PComputer x, P2PComputer y)
        {
            int result = x.timeCreated.CompareTo(y.timeCreated);
            if (result == 0)
            {
                return x.sourceComputerID.CompareTo(y.sourceComputerID);
            }
            return result;
        }
    }
    public static SortedSet<P2PComputer> computersInCreationOrder = new SortedSet<P2PComputer>(new P2PComputerComparer());

    static public void reloadAssignedColors()
    {
        SharedCube.assignedColors.Clear();
        int idx = 0;
        foreach (P2PComputer p2pIns in computersInCreationOrder)
        {
            SharedCube.assignedColors.Add(p2pIns.sourceComputerID, colorPalette[idx % colorPalette.Length]);
            idx++;
        }
        foreach (SharedCube sharedCube in SharedCube.allSharedCubes.Values)
        {
            SharedCube.setAssignedColorToCube(sharedCube);
        }
        // sets outline color
        SharedCube.setAssignedColorToGameObject(P2PSharedCubeInteractionHandler.Instance.outlineForColor, P2PObject.peerComputerID);
    }
    private P2PComputer localComputer = null;
    private bool localComputerInserted = false;
    void Start()
    {
        localComputer = new P2PComputer();
        P2PComputer.addP2PChangeListener((addOrRemove, p2pIns) =>
        {
            // always maintain computersInCreationOrder regardless of state, low overhead
            if (addOrRemove)
                computersInCreationOrder.Add(p2pIns);
            else
                computersInCreationOrder.Remove(p2pIns);

            reloadAssignedColors();
        });
    }

    public void Update()
    {
        if (P2PObject.instantiated)
        {
            ColorAssignmentStateUpdated();
        }
    }
}
