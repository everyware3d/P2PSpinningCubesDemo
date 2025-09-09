using System;
using System.Collections.Generic;
using P2PPlugin.Network;

public class P2PComputer : P2PNetworkObject
{

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

    public P2PComputer()
    {
        long pID = P2PObject.peerComputerID;
    }
    public static SortedSet<P2PComputer> computersInCreationOrder = new SortedSet<P2PComputer>(new P2PComputerComparer());
    public long timeCreated = DateTimeOffset.Now.ToUnixTimeMilliseconds(); //only set for source, then distributed


    static private Dictionary<int, Action<bool, P2PComputer>> p2pComputerChanged = new Dictionary<int, Action<bool, P2PComputer>>();
    static private int maxKeyForPeers = 0;
    static public void fireP2PComputerChanged(bool addOrRemoved, P2PComputer p2pComputer)
    {
        foreach (Action<bool, P2PComputer> callback in p2pComputerChanged.Values)
        {
            callback(addOrRemoved, p2pComputer);
        }
    }
    static public int addP2PChangeListener(Action<bool, P2PComputer> callback)
    {
        int key = ++maxKeyForPeers;
        p2pComputerChanged.Add(key, callback);
        return key;
    }
    static public int removeP2PChangeListener(int key)
    {
        p2pComputerChanged.Remove(key);
        return key;
    }


    public void AfterInsertRemote()
    {
        computersInCreationOrder.Add(this);
        fireP2PComputerChanged(true, this);
    }
    public void AfterDeleteRemote()
    {
        computersInCreationOrder.Remove(this);
        fireP2PComputerChanged(false, this);
    }
}
