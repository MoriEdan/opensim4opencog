
using Apache.Qpid.Buffer;
using System.Text;

namespace Apache.Qpid.Framing
{
  ///
  /// This class is autogenerated
  /// Do not modify.
  ///
  /// @author Code Generator Script by robert.j.greig@jpmorgan.com
  public class StreamQosBody : AMQMethodBody , IEncodableAMQDataBlock
  {
    public const int CLASS_ID = 80; 	
    public const int METHOD_ID = 10; 	

    public uint PrefetchSize;    
    public ushort PrefetchCount;    
    public uint ConsumeRate;    
    public bool Global;    
     

    protected override ushort Clazz
    {
        get
        {
            return 80;
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
        4 /*PrefetchSize*/+
            2 /*PrefetchCount*/+
            4 /*ConsumeRate*/+
            1 /*Global*/		 
        ;
         
    }
    }

    protected override void WriteMethodPayload(ByteBuffer buffer)
    {
        buffer.Put(PrefetchSize);
            buffer.Put(PrefetchCount);
            buffer.Put(ConsumeRate);
            EncodingUtils.WriteBooleans(buffer, new bool[]{Global});
            		 
    }

    protected override void PopulateMethodBodyFromBuffer(ByteBuffer buffer)
    {
        PrefetchSize = buffer.GetUInt32();
        PrefetchCount = buffer.GetUInt16();
        ConsumeRate = buffer.GetUInt32();
        bool[] bools = EncodingUtils.ReadBooleans(buffer);Global = bools[0];
        		 
    }

    public override string ToString()
    {
        StringBuilder buf = new StringBuilder(base.ToString());
        buf.Append(" PrefetchSize: ").Append(PrefetchSize);
        buf.Append(" PrefetchCount: ").Append(PrefetchCount);
        buf.Append(" ConsumeRate: ").Append(ConsumeRate);
        buf.Append(" Global: ").Append(Global);
         
        return buf.ToString();
    }

    public static AMQFrame CreateAMQFrame(ushort channelId, uint PrefetchSize, ushort PrefetchCount, uint ConsumeRate, bool Global)
    {
        StreamQosBody body = new StreamQosBody();
        body.PrefetchSize = PrefetchSize;
        body.PrefetchCount = PrefetchCount;
        body.ConsumeRate = ConsumeRate;
        body.Global = Global;
        		 
        AMQFrame frame = new AMQFrame();
        frame.Channel = channelId;
        frame.BodyFrame = body;
        return frame;
    }
} 
}