namespace WinecloudsStudio.Modules.ScreenDetection.Core;

public enum DetectionState
{
    Unknown,
    Absent,
    Present
}

public sealed class DetectionStateMachine
{
    private readonly int _presentFrames;
    private readonly int _absentFrames;
    private int _hits;
    private int _misses;

    public DetectionState State { get; private set; } = DetectionState.Unknown;

    public DetectionStateMachine(int presentFrames = 3, int absentFrames = 3)
    {
        if (presentFrames < 1) throw new ArgumentOutOfRangeException(nameof(presentFrames));
        if (absentFrames < 1) throw new ArgumentOutOfRangeException(nameof(absentFrames));
        _presentFrames = presentFrames;
        _absentFrames = absentFrames;
    }

    public bool Push(bool matched)
    {
        if (matched)
        {
            _hits++;
            _misses = 0;
            if (State != DetectionState.Present && _hits >= _presentFrames)
            {
                State = DetectionState.Present;
                return true;
            }
        }
        else
        {
            _misses++;
            _hits = 0;
            if (State != DetectionState.Absent && _misses >= _absentFrames)
                State = DetectionState.Absent;
        }

        return false;
    }
}
