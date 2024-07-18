using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSNetScanners;

internal sealed class TcpWorker : WorkerBase<TcpInput, Output, TcpResult>
{
    protected override CancellationToken Token { get => _cancellation.Token; }

    protected override Task Worker { get; }

    private readonly int _timeout;

    private readonly Cancellation _cancellation;

    internal TcpWorker(int throttle, int timeout) : base(throttle)
    {
        _timeout = timeout;
        _cancellation = new Cancellation();
        Worker = Task.Run(Start, Token);
    }

    protected override async Task Start()
    {
        List<Task<TcpResult>> tasks = [];
        while (!InputQueue.IsCompleted)
        {
            if (InputQueue.TryTake(out TcpInput input, 0, Token))
            {
                tasks.Add(TcpResult.CreateAsync(
                    input: input,
                    cancelTask: _cancellation.Task,
                    timeout: _timeout));
            }

            if (tasks.Count == _throttle)
            {
                Task<TcpResult> result = await WaitOne(tasks);
                await ProcessTaskAsync(result);
            }
        }

        while (tasks.Count > 0)
        {
            Task<TcpResult> result = await WaitOne(tasks);
            await ProcessTaskAsync(result);
        }

        OutputQueue.CompleteAdding();
    }

    protected override async Task ProcessTaskAsync(Task<TcpResult> task)
    {
        try
        {
            TcpResult result = await task;
            OutputQueue.Add(Output.CreateSuccess(result), Token);
        }
        catch (Exception exception)
        {
            ErrorRecord error = exception.CreateProcessing(task);
            OutputQueue.Add(Output.CreateError(error), Token);
        }
    }

    internal override void Cancel()
    {
        throw new System.NotImplementedException();
    }
}
