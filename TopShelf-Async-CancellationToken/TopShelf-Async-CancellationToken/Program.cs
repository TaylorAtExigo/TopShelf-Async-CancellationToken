using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;
public class Program
{
    public static void Main()
    {
        HostFactory.Run(x =>
        {
            x.Service<TaskManager>(s =>
            {
                s.ConstructUsing(name => new TaskManager());
                s.WhenStarted(tc => tc.Start());
                s.WhenStopped(tc => tc.Stop());
            });
            x.RunAsLocalSystem();

            x.SetDescription("Sample cancelation token use!");
            x.SetDisplayName("CancelationToken Example");
            x.SetServiceName("CancelationToken Example");
        });

        Console.WriteLine("Press any key to continue...");
        Console.ReadLine();
    }
}

public interface ITaskExceptionHandler
{
    void HandleException(Exception ex);
}

public interface ITask
{
    TimeSpan Delay { get; set; }
    ITaskExceptionHandler ExceptionHandler { get; set; }
    void Execute(CancellationToken token);
}

public class ConsoleExceptionHandler : ITaskExceptionHandler
{
    public void HandleException(Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}

public class TaskManager
{
    readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    bool _isRunning;

    public TaskManager()
    {
        ManagedTasks.Add(new TownCrier());
        ManagedTasks.Add(new TheWolf());
        ManagedTasks.Add(new LazyTownCrier());
        ManagedTasks.Add(new BorkenCrier());
    }

    public List<ITask> ManagedTasks
    {
        get { return _managedTasks; }
        set
        {
            _managedTasks = value ?? new List<ITask>();
        }
    }
    private List<ITask> _managedTasks = new List<ITask>();

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        Task.WaitAll(this.Tasks.ToArray());
    }

    public async void Start()
    {
        if (this.ManagedTasks.Count > 0)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("You may not call the Start() method while there are still tasks running.");
            }

            _isRunning = true;

            // Reset our list of tasks
            this.Tasks.Clear();

            foreach (var task in this.ManagedTasks)
            {
                StartNewThread(task.Execute, task.Delay, task.ExceptionHandler);
            }

            // Wait for all the tasks to complete
            await Task.WhenAll(this.Tasks);

            // Reset our flag
            _isRunning = false;
        }
    }

    private List<Task> Tasks
    {
        get { return _tasks; }
        set
        {
            _tasks = value ?? new List<Task>();
        }
    }
    private List<Task> _tasks = new List<Task>();

    private void StartNewThread(Action<CancellationToken> taskMethod, TimeSpan delay, ITaskExceptionHandler exceptionHandler)
    {
        if (taskMethod == null) { throw new ArgumentNullException("a taskMethod is requested"); }
        if (delay == null) { throw new ArgumentNullException("delay is required"); }
        else if (delay.CompareTo(TimeSpan.Zero) == delay.CompareTo(TimeSpan.MaxValue)) { throw new ArgumentOutOfRangeException("delay needs to be valid"); }

        Tasks.Add(Task.Factory.StartNew(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    taskMethod(_cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    if (exceptionHandler != null)
                    {
                        exceptionHandler.HandleException(ex);
                    }
                }

                await Task.Delay(delay, _cancellationTokenSource.Token);
            }
        }));
    }

}

public class TownCrier : ITask
{
    internal string _displayText;
    public TimeSpan Delay { get; set; }
    public ITaskExceptionHandler ExceptionHandler { get; set; }

    public TownCrier()
    {
        ExceptionHandler = new ConsoleExceptionHandler();
        Delay = TimeSpan.FromSeconds(10);
        _displayText = "It is {0} and all is well";
    }

    public void Execute(CancellationToken token)
    {
        Console.WriteLine(_displayText, DateTime.Now);
    }
}

public sealed class TheWolf : TownCrier
{
    public TheWolf()
    {
        ExceptionHandler = new ConsoleExceptionHandler();
        Delay = TimeSpan.FromMinutes(2);
        _displayText = "It is {0} and THE MOTHERF**KING WOLF IS HERE";
    }
}

public sealed class LazyTownCrier : TownCrier
{
    public LazyTownCrier()
    {
        ExceptionHandler = new ConsoleExceptionHandler();
        Delay = TimeSpan.FromSeconds(30);
        _displayText = "It is {0} and I am for sure doing my job and checking every 10 seconds";
    }
}

public sealed class BorkenCrier : ITask
{
    internal string _displayText;
    public TimeSpan Delay { get; set; }
    public ITaskExceptionHandler ExceptionHandler { get; set; }

    public BorkenCrier()
    {
        ExceptionHandler = new ConsoleExceptionHandler();
        Delay = TimeSpan.FromMinutes(1);
        _displayText = null;
    }
    public void Execute(CancellationToken token)
    {
        for (int i = 0; i < 1000; i++)
        {
            if (token.IsCancellationRequested) { Console.WriteLine("savely exited {0}", DateTime.Now); return; }
        }

        throw new Exception(string.Format("It is {0} and I broke", DateTime.Now));
    }
}