using Orleans;
using Orleans.Hosting;

using IHost? host = new HostBuilder()
    .UseOrleans(builder => builder.UseLocalhostClustering())
    .Build();

await host.StartAsync();

var grainFac = host.Services.GetRequiredService<IGrainFactory>();

var testGrain = grainFac.GetGrain<IHelloGrain>("test");
var whisperGrain = grainFac.GetGrain<IHelloGrain>("whisper");

var result = await testGrain.SayHello("World.");
Console.WriteLine("result: {0}", result);

await testGrain.WhisperTo("Can you hear me?", "whisper");

await host.StopAsync();

public interface IHelloGrain : IGrainWithStringKey
{
    Task<string> SayHello(string greeting);

    Task WhisperTo(string message, string Grain);
}

public class HelloGrain : Grain, IHelloGrain
{
    private readonly IGrainFactory _grainFactory;

    public HelloGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public Task<string> SayHello(string greeting) => Task.FromResult($"From {this.GetPrimaryKeyString()}: Hello, {greeting}");
    public async Task WhisperTo(string message, string grain)
    {
        IHelloGrain? forwardGrain = _grainFactory.GetGrain<IHelloGrain>(grain);
        var result = await forwardGrain.SayHello(message);
        Console.WriteLine("Whisper: {0}", result);
    }
}