
using Apache.Qpid.Buffer;
using System.Text;

namespace Apache.Qpid.Framing
{
  ///
  /// This class is autogenerated
  /// Do not modify.
  ///
  /// @author Code Generator Script by robert.j.greig@jpmorgan.com
  public class ExchangeDeclareBody : AMQMethodBody , IEncodableAMQDataBlock
  {
    public const int CLASS_ID = 40; 	
    public const int METHOD_ID = 10; 	

    public ushort Ticket;    
    public string Exchange;    
    public string Type;    
    public bool Passive;    
    public bool Durable;    
    public bool AutoDelete;    
    public bool Internal;    
    public bool Nowait;    
    public FieldTable Arguments;    
     

    protected override ushort Clazz
    {
        get
        {
            return 40;
        }
    }
   
    protected override ushort Method
    {
        get
        {
            return 10;
        }
    }

    protected override uint BodySize
    {
    get
    {
        
        return (uint)
        2 /*Ticket*/+
            (uint)EncodingUtils.EncodedShortStringLength(Exchange)+
            (uint)EncodingUtils.EncodedShortStringLength(Type)+
            1 /*Passive*/+
            0 /*Durable*/+
            0 /*AutoDelete*/+
            0 /*Internal*/+
            0 /*Nowait*/+
            (uint)EncodingUtils.EncodedFieldTableLength(Arguments)		 
        ;
         
    }
    }

    protected override void WriteMethodPayload(ByteBuffer buffer)
    {
        buffer.Put(Ticket);
            EncodingUtils.WriteShortStringBytes(buffer, Exchange);
            EncodingUtils.WriteShortStringBytes(buffer, Type);
            EncodingUtils.WriteBooleans(buffer, new bool[]{Passive, Durable, AutoDelete, Internal, Nowait});
            EncodingUtils.WriteFieldTableBytes(buffer, Arguments);
            		 
    }

    protected override void PopulateMethodBodyFromBuffer(ByteBuffer buffer)
    {
        Ticket = buffer.GetUInt16();
        Exchange = EncodingUtils.ReadShortString(buffer);
        Type = EncodingUtils.ReadShortString(buffer);
        bool[] bools = EncodingUtils.ReadBooleans(buffer);Passive = bools[0];
        Durable = bools[1];
        AutoDelete = bools[2];
        Internal = bools[3];
        Nowait = bools[4];
        Arguments = EncodingUtils.ReadFieldTable(buffer);
        		 
    }

    public override string ToString()
    {
        StringBuilder buf = new StringBuilder(base.ToString());
        buf.Append(" Ticket: ").Append(Ticket);
        buf.Append(" Exchange: ").Append(Exchange);
        buf.Append(" Type: ").Append(Type);
        buf.Append(" Passive: ").Append(Passive);
        buf.Append(" Durable: ").Append(Durable);
        buf.Append(" AutoDelete: ").Append(AutoDelete);
        buf.Append(" Internal: ").Append(Internal);
        buf.Append(" Nowait: ").Append(Nowait);
        buf.Append(" Arguments: ").Append(Arguments);
         
        return buf.ToString();
    }

    public static AMQFrame CreateAMQFrame(ushort channelId, ushort Ticket, string Exchange, string Type, bool Passive, bool Durable, bool AutoDelete, bool Internal, bool Nowait, FieldTable Arguments)
    {
        ExchangeDeclareBody body = new ExchangeDeclareBody();
        body.Ticket = Ticket;
        body.Exchange = Exchange;
        body.Type = Type;
        body.Passive = Passive;
        body.Durable = Durable;
        body.AutoDelete = AutoDelete;
        body.Internal = Internal;
        body.Nowait = Nowait;
        body.Arguments = Arguments;
        		 
        AMQFrame frame = new AMQFrame();
        frame.Channel = channelId;
        frame.BodyFrame = body;
        return frame;
    }
} 
}
