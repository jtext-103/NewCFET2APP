using Jtext103.CFET2.Core;
using Jtext103.CFET2.Core.Attributes;
using Jtext103.CFET2.Core.Event;
using Jtext103.CFET2.Core.Sample;

// Loaded only by integration tests through the standard application's existing loader.
public sealed class StandardHostDevice : Thing
{
    [Cfet2Status] public int Value => 1;
    private int quietReads;
    [Cfet2Status, Cfet2NoEvent] public int Quiet { get { System.Threading.Interlocked.Increment(ref quietReads); return 7; } }
    [Cfet2Status, Cfet2NoEvent] public int QuietReads => quietReads;
    [Cfet2Method] public int Publish(int value)
    {
        MyHub.Publish(new EventArg(GetPathFor(nameof(Value)), "changed", new Status<int>(value)));
        return value;
    }
}
