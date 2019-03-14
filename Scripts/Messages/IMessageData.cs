using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {
public interface IMessageData 
{
  bool MessageIsType(Message msg);
  void Parse(Message msg);

  Message CreateMessage();

}

}