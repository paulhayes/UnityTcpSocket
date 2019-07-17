using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BeaconLib;
using UnityEngine;

namespace SocketCommunication {

[CreateAssetMenu]
public class ControlAppServer : MessageEmitter
{
  public event System.Action<object> ClientConnectedEvent;
  public event System.Action<object> ClientDisconnectEvent;
  public const int Port = 7713;
  public float ClientTimeout = 5f;
  public int MaxClients = 100;

  public int sendRate = 100;


#if HierarchicalLogger
  public HierarchicalLogger discoveryLogs;
  public HierarchicalLogger sendLogs;
  public HierarchicalLogger receiveLogs;
#endif
  Beacon beacon;

  TcpListener server;
  Thread serverThread;

  Queue<Message> messages = new Queue<Message>();
  private readonly object queueLock = new object();

  Queue<Message> sendMessageQueue = new Queue<Message>();
  object sendQueueLock = new object();
  List<TcpClient> clients = new List<TcpClient>();
  List<System.DateTime> lastClientMessageTime = new List<System.DateTime>();

 //Queue<TcpClient> clientsConnected = new Queue<TcpClient>();
 //Queue<TcpClient> clientsDisconnected = new Queue<TcpClient>();

  static byte[] tmpMessageBytes = new byte[4096];

  volatile bool running;

  public void Init()
  {
    
#if HierarchicalLogger
    discoveryLogs.LogFormat(HierarchicalLogger.Info,"Starting service discovery on port {0}",Port);
#endif
    beacon = new Beacon("control-app", Port);
    beacon.BeaconData = "My Application Server on " + Dns.GetHostName();
    beacon.Start();
    running = true;
    // ...

    //beacon.Stop();
    InitListener();
  }


  public override bool HasQueuedMessages(){
    return messages.Count != 0;
  }

  public override Message PopMessage(){
    if(messages.Count>0){
      lock (queueLock){
        return messages.Dequeue();
      }
    }
    else {
      return null;
    }
  }

  public void Send(Message message){
    Send(message,null);
  }

  public void Send(Message message, object target){
    message.sender = target;
    lock(sendQueueLock){
      sendMessageQueue.Enqueue(message);
    }
  }

  public void Stop()
  {
    if(beacon != null){
      try{
        beacon.Stop();
      }
      finally {
        beacon.Dispose();
        beacon = null;
      }
    }
    
    running = false;
  }

  public void InitListener()
  {
    serverThread = new Thread(ListenLoop);
    serverThread.Start();
  }

  public void ListenLoop()
  {
    
    try{
      server = new TcpListener(IPAddress.Any,Port);
      server.Start();
      LogInfo("Control App Server started");
      
      while(running){
        int wait = 1000 / sendRate;
        //Log("server loop");
        
        if(clients.Count<MaxClients && server.Pending()){
          var client = server.AcceptTcpClient();        
          clients.Add( client );  
          lastClientMessageTime.Add( System.DateTime.UtcNow );
          ClientConnectedEvent(client);    

          if(clients.Count>=MaxClients){
            server.Stop();
          }    
        }


        //remove disconnected clients
        for(int i=0;i<clients.Count;i++){
          var client = clients[i];
          System.TimeSpan elapsed = System.DateTime.UtcNow-lastClientMessageTime[i];
          bool closed = false;
          if( elapsed.TotalSeconds>ClientTimeout){
              closed = true;
              LogInfo("client timeout, disconnected");
          }
          if(!client.Connected){
            LogInfo("client disconnected");
            closed = true;
          }
            
          if(closed){
            try {        
              ClientDisconnectEvent(client);
              client.Close();      
            } 
            finally {    
              if (client != null)
                client.Dispose();
            }
            clients.RemoveAt(i);     
            lastClientMessageTime.RemoveAt(i);     
            i--;
            if(clients.Count<MaxClients){
              server.Start();
            }
          }
        }
        

        for(int i=0;i<clients.Count;i++){
          var client = clients[i];
          var stream = client.GetStream();    
          int length=0;

          //LogFormat("server checking for messages avail={0}",stream.DataAvailable);
          while(stream.DataAvailable && (length = stream.Read(tmpMessageBytes, 0, tmpMessageBytes.Length))!=0) {
            //create messages
            LogInfo("server reading messages");
            if( WillLogVerbose() ){
              LogVerbose("Message \"{0}\"",Encoding.UTF8.GetString(tmpMessageBytes,0,length) );
            }
            lock(queueLock){
              int index = 0;            
              int messageCount = Message.FromStream(tmpMessageBytes, ref index, length, messages, client);
              if(messageCount>0)
                lastClientMessageTime[i] = System.DateTime.UtcNow;
              LogInfo("server read {0} messages",messageCount);
            }
          }
        }

        while( sendMessageQueue.Count > 0 ){
          LogInfo("sending message to clients");
          byte[] data;
          object target = null;
          lock(sendQueueLock){
            var msg = sendMessageQueue.Dequeue();
            data = msg.Data;          
            target = msg.sender;
          }
          for(int i=0;i<clients.Count;i++){
            if(target!=null && target!=clients[i])
              continue;


#if HierarchicalLogger
            sendLogs.LogFormat(HierarchicalLogger.Verbose, "sending to {0} {1}",i,Encoding.UTF8.GetString(data));
#endif
            var client = clients[i];
            var stream = client.GetStream();  
            
            try {
              
              if(client.Connected && stream!=null && stream.CanWrite )
                stream.Write( data, 0, data.Length );                  
            }
            catch(System.IO.IOException e){

#if HierarchicalLogger
              sendLogs.Log(HierarchicalLogger.Error, e);         
#endif
            }
          }
        }
        
        Thread.Sleep(wait);
      }
    } 
    catch(System.Exception e){
#if HierarchicalLogger
      discoveryLogs.Log(HierarchicalLogger.Error, e); 
#endif
    }

    for(int i=0;i<clients.Count;i++){
      try {
        clients[i].Close();      
      } finally {    
        if (clients[i] != null)
            clients[i].Dispose();
      }
    }
    clients.Clear();
    lastClientMessageTime.Clear();
    server.Stop();
    

    LogInfo("Control App Server stopped");

  }

  void LogInfo(string msg, params object[] args)
  {
    if(args!=null && args.Length>0){
      msg = string.Format(msg,args);
    }
    #if HierarchicalLogger
      discoveryLogs.Log(HierarchicalLogger.Info,msg);
    #elif LogLevelInfo
      Debug.Log(msg);
    #endif

  }

  void LogVerbose(string msg, params object[] args)
  {
    if(args!=null && args.Length>0){
      msg = string.Format(msg,args);
    }
    #if HierarchicalLogger
      discoveryLogs.Log(HierarchicalLogger.Verbose,msg);
    #elif LogLevelVerbose
      Debug.Log(msg);
    #endif
  }

  void LogError(string msg, params object[] args)
  {
    if(args!=null && args.Length>0){
      msg = string.Format(msg,args);
    }
    #if HierarchicalLogger
      discoveryLogs.Log(HierarchicalLogger.Error,msg);
    #else
      Debug.LogError(msg);
    #endif
  }

  void LogWarning(string msg, params object[] args)
  {
    if(args!=null && args.Length>0){
      msg = string.Format(msg,args);
    }
    #if HierarchicalLogger
      discoveryLogs.Log(HierarchicalLogger.Warning,msg);
    #else
      Debug.LogWarning(msg);
    #endif
  }

  bool WillLogVerbose()
  {
    #if HierarchicalLogger
      return discoveryLogs.WillLog(HierarchicalLogger.Verbose);
    #elif LogLevelVerbose
      return true;
    #else
      return false;
    #endif 
  }

  bool WillLogInfo()
  {
    #if HierarchicalLogger
      return discoveryLogs.WillLog(HierarchicalLogger.Info);
    #elif LogLevelInfo
      return true;

    #else
      return false;
    #endif  
  }
    
}

}