using UnityEngine;

namespace SocketCommunication {

[CreateAssetMenu]
public abstract class MessageEmitter : ScriptableObject
{
  public abstract Message PopMessage();
  public abstract bool HasQueuedMessages();

}

}