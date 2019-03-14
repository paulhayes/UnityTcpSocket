using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SocketCommunication {
public class TestDataSender : MonoBehaviour 
{
  public ControlAppServer server;

  public ControlAppClient client;

  void Update(){
    if(server){
      if(server.HasQueuedMessages()){
        Message message = server.PopMessage();
        Debug.Log("server received message\n    "+message.ToString());
        Debug.Log(message.GetMessageType());
        if(message.GetMessageType()==Message.Track){
          Debug.Log( message.GetValue<int>(0) );
          Debug.Log( message.GetValue<int>(1) );
          Debug.Log( message.GetValue<float>(2) );
        }
      }
      server.Send(Message.Create("Instruction",GetHashCode().ToString(),UnityEngine.Random.Range(0,1000).ToString(),"another test"));
    }

    if(client){
      if(client.HasQueuedMessages()){
        Message message = client.PopMessage();
        Debug.Log("client received message\n    "+message.ToString());        
      }

      client.Send(Message.Create("Track",GetHashCode(),UnityEngine.Random.Range(0,1000).ToString(),Time.time));     

    }
  }
}
}