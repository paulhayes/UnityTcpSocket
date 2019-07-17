using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace SocketCommunication {
public class DispatcherDemo : MonoBehaviour 
{
  [SerializeField]
  ControlAppClient client;

  [SerializeField]
  ControlAppServer server;

  [SerializeField]
  MessageDispatcher messageDispatcher;

  public int toSend = 1000;
  public int recieved;

  void Start(){
    if(server)
      server.Init();

    if(client)
      client.Init();

    
    //messageDispatcher.AddMessageHandler(new RequestMessage(), OnRequestMessage);
    //messageDispatcher.AddMessageHandler(new TrackMessage(TrackMessage.QuestionAnswer), OnTrackMessage);
    messageDispatcher.UnrecognisedMessageEvent += OnUnrecognisedMessage;
  }



  void Update()
  {
    messageDispatcher.Update();
    if(toSend>=3){
      //client.Send((new RequestMessage("previous_workshop")).CreateMessage());
      //client.Send((new ResponseMessage("load_workshop")).CreateMessage());
      
      /* 
      client.Send((new TrackMessage(TrackMessage.QuestionAnswer){
        key = "question_answer",
        deviceId = "{device id}",
        participantId = "{participant id}",
        workshopId = "{workshop id}",
        questionAnswerIndex = 1,
        correct = true
      }).CreateMessage());
      */
      toSend -= 3;

      }
  }

  private void OnRequestMessage(IMessageData obj, object sender=null)
  {
    //var data = obj as RequestMessage;
    //Debug.LogFormat("Request message recieved key={0}",data.key);
    recieved++;
  }

  private void OnTrackMessage(IMessageData obj, object sender=null)
  {
    //var data = obj as TrackMessage;
    //Debug.LogFormat("Track message recieved key={0}",data.key);
    recieved++;
  }

  private void OnUnrecognisedMessage(Message obj, object sender=null)
  {
    Debug.LogFormat("UnrecognisedMessage type={0}",obj.GetMessageType());
    recieved++;
  }
 
}

}