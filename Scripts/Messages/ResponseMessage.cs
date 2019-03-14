using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {

public class ResponseMessage : KeyMessage, IMessageData
{
  const string type = "Response";
  public const string loadWorkshop = "load_workshop";
  public string result;
  public string message;


  public ResponseMessage(string key): base(type,key)
  {

  }

  public void Parse(Message msg)
  {
    if(!matchByKey)
      key = msg.GetValue<string>(0);
    result = msg.GetValue<string>(1);
    message = msg.GetValue<string>(2);    
  }

  public Message CreateMessage()
  {
    return Message.Create(type,key,result,message);
  }
}

}