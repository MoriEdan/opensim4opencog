
using Apache.Qpid.Buffer;
using System.Text;

namespace Apache.Qpid.Framing
{
  ///
  /// This class is autogenerated
  /// Do not modify.
  ///
  /// @author Code Generator Script by robert.j.greig@jpmorgan.com
  public class ConnectionTuneOkBody : AMQMethodBody , IEncodableAMQDataBlock
  {
    public const int CLASS_ID = 10; 	
    public const int METHOD_ID = 31; 	

    public ushort ChannelMax;    
    public uint FrameMax;    
    public ushort Heartbeat;    
     

    protected override ushort Clazz
    {
        get
        {
            return 10;
        }
    }
   
    protected override ushort Method
    {
        get
        {
            return 31;
        }
    }

    protected override uint BodySize
    {
    get
    {
        
        return (uint)
        2 /*ChannelMax*/+
            4 /*FrameMax*/+
            2 /*Heartbeat*/		 
        ;
         
    }
    }

    protected override void WriteMethodPayload(ByteBuffer buffer)
    {
        buffer.Put(ChannelMax);
            buffer.Put(FrameMax);
            buffer.Put(Heartbeat);
            		 
    }

    protected override void PopulateMethodBodyFromBuffer(ByteBuffer buffer)
    {
        ChannelMax = buffer.GetUInt16();
        FrameMax = buffer.GetUInt32();
        Heartbeat = buffer.GetUInt16();
        		 
    }

    public override string ToString()
    {
        StringBuilder buf = new StringBuilder(base.ToString());
        buf.Append(" ChannelMax: ").Append(ChannelMax);
        buf.Append(" FrameMax: ").Append(FrameMax);
        buf.Append(" Heartbeat: ").Append(Heartbeat);
         
        return buf.ToString();
    }

    public static AMQFrame CreateAMQFrame(ushort channelId, ushort ChannelMax, uint FrameMax, ushort Heartbeat)
    {
        ConnectionTuneOkBody body = new ConnectionTuneOkBody();
        body.ChannelMax = ChannelMax;
        body.FrameMax = FrameMax;
        body.Heartbeat = Heartbeat;
        		 
        AMQFrame frame = new AMQFrame();
        frame.Channel = channelId;
        frame.BodyFrame = body;
        return frame;
    }
} 
}
