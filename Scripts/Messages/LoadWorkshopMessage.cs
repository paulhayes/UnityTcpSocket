namespace SocketCommunication {

public class LoadWorkshopMessage : RequestMessage 
{
  public string workshopId;

  public LoadWorkshopMessage(string workshopId=null) : base("load_workshop")
  {
    this.workshopId = workshopId;
  }

  public override void Parse(Message msg)
  {
    base.Parse(msg);
    workshopId = msg.GetValue<string>(1);
  }

  public override Message CreateMessage()
  {
    return Message.Create(type,key,workshopId);
  }
}

}