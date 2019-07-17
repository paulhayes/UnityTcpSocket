using System.Collections;
using System.Collections.Generic;
using SocketCommunication;
using UnityEngine;

public class DemoTrackMessage 
{
    public const string type = "Track";

    public string key;

    public Message CreateMessage()
    {
        return Message.Create(type,key);
    }

    public void Parse(Message msg)
    {
        key = msg.GetValue<string>(0);
    }

    public bool MessageIsType(Message msg)
    {
        return type==msg.GetMessageType();
    }
}
