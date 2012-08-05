
using Apache.Qpid.Buffer;
using System.Text;

namespace Apache.Qpid.Framing
{
  ///
  /// This class is autogenerated
  /// Do not modify.
  ///
  /// @author Code Generator Script by robert.j.greig@jpmorgan.com
  public class AccessRequestOkBody : AMQMethodBody , IEncodableAMQDataBlock
  {
    public const int CLASS_ID = 30; 	
    public const int METHOD_ID = 11; 	

    public ushort Ticket;    
     

    protected override ushort Clazz
    {
        get
        {
            return 30;
        }
    }
   
    protected override ushort Method
    {
        get
        {
            return 11;
        }
    }

    protected override uint BodySize
    {
    get
    {
        
        return (uint)
        2 /*Ticket*/		 
        ;
         
    }
    }

    protected override void WriteMethodPayload(ByteBuffer buffer)
    {
        buffer.Put(Ticket);
            		 
    }

    protected override void PopulateMethodBodyFromBuffer(ByteBuffer buffer)
    {
        Ticket = buffer.GetUInt16();
        		 
    }

    public override string ToString()
    {
        StringBuilder buf = new StringBuilder(base.ToString());
        buf.Append(" Ticket: ").Append(Ticket);
         
        return buf.ToString();
    }

    public static AMQFrame CreateAMQFrame(ushort channelId, ushort Ticket)
    {
        AccessRequestOkBody body = new AccessRequestOkBody();
        body.Ticket = Ticket;
        		 
        AMQFrame frame = new AMQFrame();
        frame.Channel = channelId;
        frame.BodyFrame = body;
        return frame;
    }
} 
}