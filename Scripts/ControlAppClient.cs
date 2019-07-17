using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BeaconLib;
using UnityEngine;

namespace SocketCommunication {

[CreateAssetMenu]
public class ControlAppClient : MessageEmitter
{
  public int maxMessagesInQueue = 20;

  public int sendRate = 100;

  Probe probe;

  Queue<Message> messages = new Queue<Message>();
  object queueLock = new object();

  Queue<Message> sendMessageQueue = new Queue<Message>();
  object sendQueueLock = new object();

  object addressFoundLock = new object();
  
  Thread clientThread;

  EventWaitHandle waitForServerAddress = new EventWaitHandle(false,EventResetMode.AutoReset);

  volatile IPEndPoint serverAddress;

  byte[] tmpReadBytes = new byte[4096];

  volatile bool running;


#if HierarchicalLogger
  public HierarchicalLogger discoveryLogs;
  public HierarchicalLogger sendLogs;
  public HierarchicalLogger receiveLogs;
#endif

  public enum ClientState {
    Disconnected,
    Discovering,
    Connecting,
    Connected
  }

  public ClientState State {
    protected set;
    get;
  }

  public void Init()
  {
    State = ClientState.Disconnected;
    running = true;
    
    // Event is raised on separate thread so need synchronization
    
    if(clientThread!=null)
      return;

    clientThread = new Thread(ClientLoop);
    clientThread.Start();

  }

  void StartProbe()
  {
    State = ClientState.Discovering;
    
#if HierarchicalLogger
    if(discoveryLogs)
      discoveryLogs.Log(HierarchicalLogger.Info, "Starting Probe");
#endif

    if(probe==null){
      probe = new Probe("control-app");
    }
    /* 
    if(probe!=null){
      try{
        probe.BeaconsUpdated -= OnBeacons;
        probe.Stop();
      }
      finally {
        probe.Dispose();        
      }
    }
    */
    
    probe.Start();
    probe.BeaconsUpdated += OnBeacons;
  }

  public override bool HasQueuedMessages(){
    return messages.Count>0;
  }

  public override Message PopMessage()
  {
    if(HasQueuedMessages()){
      lock (queueLock){
        return messages.Dequeue();
      }
    }
    else {
      return null;
    }
  }

  public void Stop()
  {
    if(probe!=null){
      probe.BeaconsUpdated -= OnBeacons;
      probe.Dispose();
    }
    running = false; 
    
    if(clientThread!=null){
      waitForServerAddress.Set();   
      clientThread.Join(); 
    }
  }

  public void Send(Message message){
    lock(sendQueueLock){
      sendMessageQueue.Enqueue(message);
      if(sendMessageQueue.Count>maxMessagesInQueue){
        sendMessageQueue.Dequeue();        
      }
    }
  }

  private void OnBeacons(IEnumerable<BeaconLocation> beacons)
  {
    Debug.Log("OnBeacons");
    IPEndPoint address = null; 
    foreach(var beacon in beacons){
      Debug.Log("Found beacon");
      Debug.Log(beacon.Address);
      address = beacon.Address;
      break;
    }
    
    if(address==null)
      return;

    //probe.BeaconsUpdated -= OnBeacons;
        
    lock(addressFoundLock){
      serverAddress = address; 
    }
    waitForServerAddress.Set();
  }

  private void ClientLoop(object obj)
  {
    TcpClient client = null;
    NetworkStream stream = null;
    IPEndPoint address = null;
    try{
      while(running){
        int wait = 1000 / sendRate;
        if( client==null || !client.Connected || !client.Client.Connected ){
          AwaitServerDiscovery();
          Connect(ref client,ref stream, ref address);
          continue;
        }

        
      
        try{
          while( sendMessageQueue.Count > 0 && stream.CanWrite ){
#if HierarchicalLogger
            sendLogs.Log(HierarchicalLogger.Info,"sending message");
#endif
            byte[] data;
            lock(sendQueueLock){
              data = sendMessageQueue.Dequeue().Data;
            }
            stream.Write(data, 0, data.Length );
            
          }
        }
        catch(IOException e){
#if HierarchicalLogger
          if(discoveryLogs){
            discoveryLogs.Log(HierarchicalLogger.Error,e.ToString());
          }
#endif
          continue;
        }

      //Log("client checking for messages");
      int len=0;
      try {
        if( stream.CanRead && stream.DataAvailable && 0!=(len=stream.Read(tmpReadBytes,0,tmpReadBytes.Length)) ){
#if HierarchicalLogger
          receiveLogs.Log(HierarchicalLogger.Info, "reading message");
#endif
          int index = 0;
#if HierarchicalLogger
          receiveLogs.Log(HierarchicalLogger.Verbose, System.Text.Encoding.ASCII.GetString(tmpReadBytes,0,len));
#endif
          Message.FromStream(tmpReadBytes, ref index, len, messages);
        }
      }
      catch(SocketException e){
#if HierarchicalLogger
        receiveLogs.Log(HierarchicalLogger.Error,e.ToString());
#endif
        continue;
      }
      
      //Log("client sleeping");
      if(running)
        Thread.Sleep(wait);
    }
    }
    catch(ThreadAbortException e){
      
    }
    catch(Exception e){
#if HierarchicalLogger
      if(receiveLogs)
          receiveLogs.Log(HierarchicalLogger.Error, e.ToString());
#endif
    }
    try {
      if(client!=null && client.Connected){      
        client.Close();
      }
    } 
    finally {    
      if(stream != null)
        stream.Dispose();

      if (client != null)
          client.Dispose();
    }
#if HierarchicalLogger
    discoveryLogs.Log(HierarchicalLogger.Info,"Control App Client stopped");
#endif
    clientThread = null;
  }

  void AwaitServerDiscovery()
  { 
    // we already have address, so we can exit early   
    lock(sendQueueLock){
      if(serverAddress!=null){
        return;      
      }
    }

    try{ 
#if HierarchicalLogger
        discoveryLogs.Log(HierarchicalLogger.Info,"restarting probe");
#endif
        StartProbe();
    }
    catch(ThreadStateException e2){
#if HierarchicalLogger
      discoveryLogs.Log(HierarchicalLogger.Error,e2.ToString());
#endif

      //probe thread was already running?
    }
    
#if HierarchicalLogger
    if(discoveryLogs)
      discoveryLogs.LogFormat(HierarchicalLogger.Info,"Waiting for server discovery");
#endif
    waitForServerAddress.WaitOne();
    probe.Stop();
#if HierarchicalLogger
    if(discoveryLogs)
      discoveryLogs.LogFormat(HierarchicalLogger.Info,"Connect to {0}",serverAddress);
#endif
    
      
  }

  void Connect(ref TcpClient client, ref NetworkStream stream, ref IPEndPoint address)
  {
    if( client!=null ){
      client.Close();
      client.Dispose();
    }

    

    client = new TcpClient();
    try {
#if HierarchicalLogger
      if(discoveryLogs)
        discoveryLogs.LogFormat(HierarchicalLogger.Info,"Attempting connect");
#endif
      client.NoDelay = true;
      lock(addressFoundLock){
        address = serverAddress;
      }
      if(address==null){
        return;
      }
      State = ClientState.Connecting;
      client.Connect(address);
      stream = client.GetStream();  
      State = ClientState.Connected;    
    }
    catch(SocketException e){
      State = ClientState.Disconnected;

      //connect failed
#if HierarchicalLogger
      if(discoveryLogs){
        discoveryLogs.Log(HierarchicalLogger.Error,e.ToString());
      }
#endif
      if(stream!=null){            
        stream.Dispose();
        stream = null;
      }
      if(client!=null){
        client.Dispose();
        client = null;
      }
      lock(addressFoundLock){
        serverAddress = null;        
      }
      address = null;
            
      Thread.Sleep(500);
    }                
  }
  
}

}