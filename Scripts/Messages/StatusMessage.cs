using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {

public class StatusMessage : IMessageData
{
  const string type = "Status";

  public string deviceId;
  public string workshopId;
  public int battery;
  public string state;
  public string message;

  public int section;

  public bool MessageIsType(Message msg)
  {
    return msg.GetMessageType()==type;
  }

  public void Parse(Message msg)
  {
    deviceId = msg.GetValue<string>(0);
    battery = msg.GetValue<int>(1);
    workshopId = msg.GetValue<string>(2);
    message = msg.GetValue<string>(3) ?? string.Empty;
    state = msg.GetValue<string>(4) ?? string.Empty;
    section = msg.GetValue<int>(5);
  }


  public Message CreateMessage()
  {
    return Message.Create(
      type,
      deviceId,
      battery,
      workshopId,
      message,
      state,
      section
    );
  }

  
}

}