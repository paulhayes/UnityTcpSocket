namespace SocketCommunication {

public class RequestMessage : KeyMessage, IMessageData
{  
  public const string PlayWorkshop = "play_workshop";
  public const string StopWorkshop = "stop_workshop";
  public const string GotoWorkshopSection = "goto_section";
  public const string RewindWorkshop = "rewind_workshop";
  public const string CenterYRotation = "center_y_rotation";
  public const string RegisterAsChair = "register_as_chair";

  public const string ClearAnswerStats = "clear_question_stats";  
  protected const string type = "Request";
  
  public int section;
  
  public RequestMessage(string key=null, int section = 0): base(type,key)
  {
    this.section = section;
  }

  public virtual void Parse(Message msg)
  {
    if(!matchByKey)
      key = msg.GetValue<string>(0);
    if(key==GotoWorkshopSection){
      section = msg.GetValue<int>(1);
    }
  }

  public virtual Message CreateMessage()
  {
    if(key==GotoWorkshopSection){
      return Message.Create(type,key,section);
    }
    return Message.Create(type,key);
  }
}

}