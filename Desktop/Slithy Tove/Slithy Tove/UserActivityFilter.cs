//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               User Activity Filter
//===============================================
namespace Slithy_Tove;

// Tracks keyboard and mouse activity so the app can decide when to auto-lock.
internal sealed class UserActivityFilter : IMessageFilter
{
    public DateTimeOffset LastActivityUtc { get; private set; } = DateTimeOffset.UtcNow;

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg is >= 0x0100 and <= 0x0109 ||
            message.Msg is >= 0x0200 and <= 0x020E)
        {
            LastActivityUtc = DateTimeOffset.UtcNow;
        }
        return false;
    }
}

