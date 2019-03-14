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
  
  Thread clientThread;

  EventWaitHandle waitForServerAddress = new EventWaitHandle(false,EventResetMode.AutoReset);

  IPEndPoint serverAddress;

  byte[] tmpReadBytes = new byte[4096];

  volatile bool running;

  public HierarchicalLogger discoveryLogs;
  public HierarchicalLogger sendLogs;
  public HierarchicalLogger receiveLogs;


  public void Init()
  {
    running = true;
    
    // Event is raised on separate thread so need synchronization
    
    if(clientThread!=null)
      return;

    clientThread = new Thread(ClientLoop);
    clientThread.Start();

  }

  void StartProbe()
  {
    if(probe!=null){
      probe.BeaconsUpdated -= OnBeacons;
    }
    if(discoveryLogs)
      discoveryLogs.Log(HierarchicalLogger.Info, "Starting Probe");

    probe = new Probe("control-app");
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
      probe.Stop();
    }
    running = false;    
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
    }
    
    if(address==null)
      return;

    //probe.BeaconsUpdated -= OnBeacons;
        
    lock(sendQueueLock){
      serverAddress = address; 
    }
    waitForServerAddress.Set();
  }

  private void ClientLoop(object obj)
  {
    StartProbe();
    TcpClient client = null;
    NetworkStream stream = null;
    IPEndPoint address = null;
    try{
      while(running){
        int wait = 1000 / sendRate;
        if( client==null || !client.Connected || !client.Client.Connected ){
          AwaitServerDiscovery(ref address);
          Connect(ref client,ref stream, ref address);
          continue;
        }
      
        try{
          while( sendMessageQueue.Count > 0 && stream.CanWrite ){
            sendLogs.Log(HierarchicalLogger.Info,"sending message");
            byte[] data;
            lock(sendQueueLock){
              data = sendMessageQueue.Dequeue().Data;
            }
            stream.Write(data, 0, data.Length );
            
          }
        }
        catch(IOException e){
          if(discoveryLogs){
            discoveryLogs.Log(HierarchicalLogger.Error,e.ToString());
          }
          continue;
        }

      //Log("client checking for messages");
      int len=0;
      try {
        if( stream.CanRead && stream.DataAvailable && 0!=(len=stream.Read(tmpReadBytes,0,tmpReadBytes.Length)) ){
          receiveLogs.Log(HierarchicalLogger.Info, "reading message");
          int index = 0;
          receiveLogs.Log(HierarchicalLogger.Verbose, System.Text.Encoding.ASCII.GetString(tmpReadBytes,0,len));
          Message.FromStream(tmpReadBytes, ref index, len, messages);
        }
      }
      catch(SocketException e){
        receiveLogs.Log(HierarchicalLogger.Error,e.ToString());
        continue;
      }
      
      //Log("client sleeping");
      Thread.Sleep(5);
    }
    }
    catch(ThreadAbortException e){
      
    }
    catch(Exception e){
      if(receiveLogs)
          receiveLogs.Log(HierarchicalLogger.Error, e.ToString());
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
    discoveryLogs.Log(HierarchicalLogger.Info,"Control App Client stopped");
  }

  void AwaitServerDiscovery(ref IPEndPoint address)
  { 
    // we already have address, so we can exit early   
    if(address!=null){
      return;      
    }

    if(discoveryLogs)
      discoveryLogs.LogFormat(HierarchicalLogger.Info,"Waiting for server discovery");
    waitForServerAddress.WaitOne();
    probe.Stop();
    if(discoveryLogs)
      discoveryLogs.LogFormat(HierarchicalLogger.Info,"Conntect to {0}",serverAddress);

    lock(sendQueueLock){
      address = serverAddress;
    }
      
  }

  void Connect(ref TcpClient client, ref NetworkStream stream, ref IPEndPoint address)
  {
    if( client!=null ){
      client.Close();
      client.Dispose();
    }

    client = new TcpClient();
    try {
      if(discoveryLogs)
        discoveryLogs.LogFormat(HierarchicalLogger.Info,"Attempting connect");
      client.NoDelay = true;
      client.Connect(address);
      stream = client.GetStream();      
    }
    catch(SocketException e){
      //connect failed
      if(discoveryLogs){
        discoveryLogs.Log(HierarchicalLogger.Error,e.ToString());
      }
      if(stream!=null){            
        stream.Dispose();
        stream = null;
      }
      if(client!=null){
        client.Dispose();
        client = null;
      }
      lock(sendQueueLock){
        serverAddress = null;
      }
      
      try{ 
        discoveryLogs.Log(HierarchicalLogger.Info,"restarting probe");
        StartProbe();
      }
      catch(ThreadStateException e2){
        if(discoveryLogs){
          discoveryLogs.Log(HierarchicalLogger.Error,e2.ToString());
        }
        //probe thread was already running?
      }
      Thread.Sleep(500);
    }                
  }
  
}

}