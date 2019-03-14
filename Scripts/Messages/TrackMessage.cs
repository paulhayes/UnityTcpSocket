namespace SocketCommunication {

public class TrackMessage : KeyMessage, IMessageData
{
  public const string QuestionAnswer = "question_answer";

  const string type = "Track";

  public string deviceId;
  public string participantId;
  public string workshopId;
  public int questionIndex;
  public int questionAnswerIndex;
  public bool correct;

  public TrackMessage(string key): base(type,key)
  {

  }

  public Message CreateMessage()
  {
    return Message.Create(type,key,deviceId,participantId,workshopId,questionIndex,questionAnswerIndex,correct?"correct":"incorrect");
  }

  public void Parse(Message msg)
  {
    key = msg.GetValue<string>(0);
    deviceId = msg.GetValue<string>(1);
    participantId = msg.GetValue<string>(2);
    workshopId = msg.GetValue<string>(3);
    questionIndex = msg.GetValue<int>(4);
    questionAnswerIndex = msg.GetValue<int>(5);
    correct = msg.GetValue<string>(6) == "correct";
  }
}

}