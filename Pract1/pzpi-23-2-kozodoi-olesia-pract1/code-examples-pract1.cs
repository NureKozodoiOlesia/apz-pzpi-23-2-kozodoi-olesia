public interface ICommand
{
    void Execute();
    void Undo();
}

public class Light
{
    private readonly string _location;

    public Light(string location)
    {
        _location = location;
    }

    public void TurnOn()  => Console.WriteLine($"[Свiтло] {_location}: увiмкнено");
    public void TurnOff() => Console.WriteLine($"[Свiтло] {_location}: вимкнено");
}

public class Television
{
    public void TurnOn()  => Console.WriteLine("[Телевiзор] увiмкнено");
    public void TurnOff() => Console.WriteLine("[Телевiзор] вимкнено");
}

public class LightOnCommand : ICommand
{
    private readonly Light _light;
    public LightOnCommand(Light light) => _light = light;
    public void Execute() => _light.TurnOn();
    public void Undo()    => _light.TurnOff();
}

public class LightOffCommand : ICommand
{
    private readonly Light _light;
    public LightOffCommand(Light light) => _light = light;
    public void Execute() => _light.TurnOff();
    public void Undo()    => _light.TurnOn();
}

public class TvOnCommand : ICommand
{
    private readonly Television _tv;
    public TvOnCommand(Television tv) => _tv = tv;
    public void Execute() => _tv.TurnOn();
    public void Undo()    => _tv.Undo();
}

public class TvOffCommand : ICommand
{
    private readonly Television _tv;
    public TvOffCommand(Television tv) => _tv = tv;
    public void Execute() => _tv.TurnOff();
    public void Undo()    => _tv.TurnOn();
}

public class RemoteControl
{
    private readonly Stack<ICommand> _history = new Stack<ICommand>();

    public void PressButton(ICommand command)
    {
        command.Execute();
        _history.Push(command);
    }

    public void PressUndo()
    {
        if (_history.Count == 0)
        {
            Console.WriteLine("[Пульт] Немає дiй для скасування.");
            return;
        }

        ICommand lastCommand = _history.Pop();
        lastCommand.Undo();
        Console.WriteLine("[Пульт] Дiю скасовано");
    }
}

class Program
{
    static void Main()
    {
        var livingRoomLight = new Light("Вiтальня");
        var bedroomLight    = new Light("Спальня");
        var tv              = new Television();

        ICommand livingRoomLightOn  = new LightOnCommand(livingRoomLight);
        ICommand livingRoomLightOff = new LightOffCommand(livingRoomLight);
        ICommand bedroomLightOn     = new LightOnCommand(bedroomLight);
        ICommand tvOn               = new TvOnCommand(tv);
        ICommand tvOff              = new TvOffCommand(tv);

        var remote = new RemoteControl();

        remote.PressButton(livingRoomLightOn);
        remote.PressButton(tvOn);
        remote.PressButton(bedroomLightOn);

        remote.PressUndo();
        remote.PressUndo();

        remote.PressButton(livingRoomLightOff);
        remote.PressButton(tvOff);

        remote.PressUndo();
        remote.PressUndo();
        remote.PressUndo();
    }
}
