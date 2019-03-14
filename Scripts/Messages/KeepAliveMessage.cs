using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {
public class KeepAliveMessage : IMessageData
{
  protected const string type = "KeepAlive";
  public Message CreateMessage()
  {
    return Message.Create(type);
  }

  public bool MessageIsType(Message msg)
  {
    return type==msg.GetMessageType();
  }

  public void Parse(Message msg)
  {
    
  }
}

}