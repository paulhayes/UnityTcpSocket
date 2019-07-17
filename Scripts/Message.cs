using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SocketCommunication {

public class Message 
{
  public const string None = "None";

  byte[] messageData; 

  public object sender;

  public Message(byte[] bytes, object sender=null)
  {
    messageData = bytes;
    this.sender = sender;
  }

  public string GetMessageType(){
    int valueStartIndex = 0;
    int valueEndIndex = Array.IndexOf<byte>(messageData,(byte)':')-1;
    if(valueEndIndex<=0){
      return None;
    }

    return Encoding.UTF8.GetString(messageData,valueStartIndex,valueEndIndex-valueStartIndex+1);
  }

  public T GetValue<T>(int index)
  {
    int valueStartIndex = 0;
    int valueEndIndex = 0;
    int pos = Array.IndexOf<byte>(messageData,(byte)':');
    int targetIndex = index;
    if(pos==-1){
      return default(T);
    }
    pos++;
    valueStartIndex = pos;
    while(index>=0){
      if(pos>=messageData.Length){
        break;
      }
      valueEndIndex = Array.IndexOf<byte>(messageData,(byte)',',pos);
      if(valueEndIndex==-1){
        valueEndIndex = messageData.Length-1;
      }
      else {
        valueEndIndex--;
      }
      pos=valueEndIndex+2;
      if(valueEndIndex>=0 && messageData[valueEndIndex]=='\\'){
        continue;
      }
      
      if(index>0){
        valueStartIndex = pos;
      }
      index--;
      
    }

    if(valueStartIndex>valueEndIndex || valueEndIndex>=messageData.Length){
      return default(T);
    }

    TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
    T value = default(T);
    string valueStr = Encoding.UTF8.GetString(messageData,valueStartIndex,valueEndIndex-valueStartIndex+1);
    //string msg = Encoding.UTF8.GetString(messageData);
    //Debug.LogFormat("msg value {0} decoded \"{1}\"\n{2}",targetIndex,str,msg);
        
      try{
        value = (T)converter.ConvertFromString(null,CultureInfo.InvariantCulture, valueStr);
      }
      catch(System.Exception e){
        Debug.LogErrorFormat("Failed to parse message part. exoected {0} got \"{1}\"\n"+
          "complete message={2}",typeof(T),valueStr,Encoding.UTF8.GetString(messageData));
      }
      
      return value;
    }

    public override string ToString()
    {
      return Encoding.UTF8.GetString(messageData);
    } 

    /* public static Message FromString(string messageStr){
      return new Message(Encoding.UTF8.GetBytes(messageStr));
    }*/

  internal static StringBuilder stringBuilder = new StringBuilder(4096);

  public static Message Create(string message, params object[] values){
    stringBuilder.Length = 0;
    stringBuilder.Append(message);
    stringBuilder.Append(':');
    for(int i=0;i<values.Length;){
      if(values[i]!=null)
        stringBuilder.Append(values[i]);
      i++;
      if(i<values.Length){
        stringBuilder.Append(',');
      }
    }
    stringBuilder.Append("\r\n");
    return new Message(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
  }

  public static int FromStream(byte[] bytes, ref int messageStartIndex, int length, Queue<Message> queue, object sender = null){
    int pos = messageStartIndex;
    int messageEndIndex;
    int count = 0;
    while( (messageEndIndex = Array.IndexOf<byte>(bytes,(byte)'\r',pos,length-pos) ) != -1 ){

      if((messageEndIndex+1)<length && bytes[messageEndIndex+1]!='\n'){ 
        pos = messageEndIndex+1;
        continue;
      }      
      
      queue.Enqueue( new Message(SubArray(bytes,messageStartIndex,messageEndIndex-messageStartIndex), sender ) );
      count++;
      
      pos = messageStartIndex = messageEndIndex+2;
      if(pos>=length){
        break;
      }
    }

    return count;
  }

  public byte[] Data {
    get {
      return messageData;
    }
  }

  static byte[] SubArray(byte[] data, int index, int length)
  {
      byte[] result = new byte[length];
      Array.Copy(data, index, result, 0, length);
      return result;
  }
}

}