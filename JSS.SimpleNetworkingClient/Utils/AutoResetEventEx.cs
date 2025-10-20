using System;
using System.Threading;

namespace JSS.SimpleNetworkingClient.Utils;

/// <summary>
/// AutoResetEvent that has support to pass on an exception to the waiting thread
/// </summary>
public class AutoResetEventEx: EventWaitHandle
{
    private Exception _thrownException;
    
    public AutoResetEventEx(bool initialState) : base(initialState, EventResetMode.AutoReset) { }
    
    public bool Set(Exception exception)
    {
        _thrownException = exception;
        return base.Set();
    }

    /// <summary>Blocks the current thread until the current instance receives a signal, using a <see cref="T:System.TimeSpan" /> to specify the time interval.</summary>
    /// <param name="timeout">A <see cref="T:System.TimeSpan" /> that represents the number of milliseconds to wait, or a <see cref="T:System.TimeSpan" /> that represents -1 milliseconds to wait indefinitely.</param>
    /// <returns>
    /// <see langword="true" /> if the current instance receives a signal; otherwise, <see langword="false" />.</returns>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    ///         <paramref name="timeout" /> is a negative number other than -1 milliseconds, which represents an infinite time-out.
    /// -or-
    /// <paramref name="timeout" /> is greater than <see cref="F:System.Int32.MaxValue" />.</exception>
    /// <exception cref="T:System.Threading.AbandonedMutexException">The wait completed because a thread exited without releasing a mutex. This exception is not thrown on Windows 98 or Windows Millennium Edition.</exception>
    /// <exception cref="T:System.InvalidOperationException">The current instance is a transparent proxy for a <see cref="T:System.Threading.WaitHandle" /> in another application domain.</exception>
    public override bool WaitOne(TimeSpan timeout)
    {
        var waitResult = base.WaitOne(timeout);
        
        if (_thrownException != null)
            throw _thrownException;
        
        return waitResult;   
    }

    /// <summary>Blocks the current thread until the current instance receives a signal.</summary>
    /// <returns>
    /// <see langword="true" /> if the current instance receives a signal; otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="T:System.ThreadStateException">The thread that invokes this method is not in a valid state to execute this operation.</exception>
    /// <exception cref="Exception">An exception was previously set and is thrown when this method is called.</exception>
    public override bool WaitOne(int millisecondsTimeout)
    {
        var waitResult = base.WaitOne(millisecondsTimeout);
        
        if (_thrownException != null)
            throw _thrownException;
        
        return waitResult;
    }
    
    public override bool WaitOne()
    {
        var waitResult = base.WaitOne(10_000);
        
        if (_thrownException != null)
            throw _thrownException;
        
        return waitResult;
    }
}