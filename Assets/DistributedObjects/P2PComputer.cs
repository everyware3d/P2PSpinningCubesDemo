using System;
using System.Collections.Generic;
using P2PPlugin.Network;

public class P2PComputer : P2PNetworkObject
{

    public P2PComputer()
    {
        long pID = P2PObject.peerComputerID;
    }
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
        fireP2PComputerChanged(true, this);
    }
    public void AfterDeleteRemote()
    {
        fireP2PComputerChanged(false, this);
    }
}
