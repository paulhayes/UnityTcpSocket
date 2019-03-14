using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {

[CreateAssetMenu]
public class MessageDispatcher : ScriptableObject
{
  [SerializeField]
  MessageEmitter[] messageEmitters;

  public event MessageHandler UnrecognisedMessageEvent;

  List<IMessageData> messageTypes = new List<IMessageData>();
  List<MessageDataHandler> listeners = new List<MessageDataHandler>();

  public delegate void MessageDataHandler(IMessageData messageData, object sender=null);
  public delegate void MessageHandler(Message message, object sender=null);

  public void AddMessageHandler( IMessageData messageType, MessageDataHandler action)
  {    
    messageTypes.Add( messageType );
    listeners.Add(action);
  }

  public void RemoveMessageHandler(MessageDataHandler onTrackMessage)
  {
    int index = listeners.IndexOf(onTrackMessage);
    if(index>=0){
      listeners.RemoveAt(index);
      messageTypes.RemoveAt(index);
    }
  }

  public void Update()
  {
    foreach(var emitter in messageEmitters){
      ProcessMessages(emitter);
    }
  }

  void ProcessMessages(MessageEmitter messageEmitter)
  {
    while( messageEmitter.HasQueuedMessages() ){
      var msg = messageEmitter.PopMessage();
      int len = messageTypes.Count;
      bool matched = false;
      for(int i=0;i<len;i++){
        if(messageTypes[i].MessageIsType(msg)){
          messageTypes[i].Parse(msg);
          listeners[i].Invoke(messageTypes[i], msg.sender);
          matched = true;           
        }
      }

      if(matched)
        continue;

      if(UnrecognisedMessageEvent!=null){
        UnrecognisedMessageEvent.Invoke(msg);
      }
    }

  }


  void OnDisable()
  {
    listeners.Clear();
  }

}

}