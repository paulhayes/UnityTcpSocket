namespace SocketCommunication {

public abstract class KeyMessage 
{
  
  readonly string type;
  public string key;


  protected bool matchByKey;

  public KeyMessage(string type,string key=null)
  {
    this.type = type;
    matchByKey = (key!=null);    
    if(matchByKey)
      this.key = key;
  }

  public bool MessageIsType(Message msg)
  {
    return type==msg.GetMessageType() && ( !matchByKey || key == msg.GetValue<string>(0) );
  }


}

}